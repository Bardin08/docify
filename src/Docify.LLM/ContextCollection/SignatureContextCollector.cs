using Docify.Core.Models;
using Docify.LLM.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;

namespace Docify.LLM.ContextCollection;

/// <summary>
/// Collects signature and type information for API symbols using Roslyn semantic analysis.
/// </summary>
public class SignatureContextCollector(ILogger<SignatureContextCollector> logger) : IContextCollector
{
    /// <inheritdoc/>
    public async Task<ApiContext> CollectContext(
        ApiSymbol symbol,
        Compilation compilation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(symbol);
        ArgumentNullException.ThrowIfNull(compilation);

        var roslynSymbol = await GetRoslynSymbol(symbol, compilation, cancellationToken).ConfigureAwait(false);

        if (roslynSymbol == null)
            throw new InvalidOperationException($"Could not find Roslyn symbol for {symbol.FullyQualifiedName}");

        var parameterTypes = ExtractParameterTypes(roslynSymbol);
        var returnType = ExtractReturnType(roslynSymbol);
        var inheritanceHierarchy = ExtractInheritanceHierarchy(roslynSymbol);
        var relatedTypes = ExtractRelatedTypes(roslynSymbol, parameterTypes, returnType);
        var xmlDocComments = ExtractXmlDocumentation(roslynSymbol);
        var tokenEstimate = EstimateTokenCount(parameterTypes, returnType, inheritanceHierarchy, xmlDocComments);

        logger.LogDebug("Collected signature context for {ApiName}", symbol.FullyQualifiedName);

        return new ApiContext
        {
            ApiSymbolId = symbol.Id,
            ParameterTypes = parameterTypes,
            ReturnType = returnType,
            InheritanceHierarchy = inheritanceHierarchy,
            RelatedTypes = relatedTypes,
            XmlDocComments = xmlDocComments,
            TokenEstimate = tokenEstimate
        };
    }

    private async Task<ISymbol?> GetRoslynSymbol(
        ApiSymbol symbol,
        Compilation compilation,
        CancellationToken cancellationToken)
    {
        foreach (var syntaxTree in compilation.SyntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            var root = await syntaxTree.GetRootAsync(cancellationToken).ConfigureAwait(false);

            var nodes = root.DescendantNodes();
            foreach (var node in nodes)
            {
                var nodeSymbol = semanticModel.GetDeclaredSymbol(node, cancellationToken);
                if (nodeSymbol != null && GetFullyQualifiedName(nodeSymbol) == symbol.FullyQualifiedName)
                    return nodeSymbol;
            }
        }

        return null;
    }

    private string GetFullyQualifiedName(ISymbol symbol)
    {
        return symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat
            .WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted));
    }

    private List<string> ExtractParameterTypes(ISymbol symbol)
    {
        var parameters = new List<string>();

        switch (symbol)
        {
            case IMethodSymbol methodSymbol:
            {
                var parametersList = methodSymbol.Parameters
                    .Select(x => $"{x.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} {x.Name}");

                parameters.AddRange(parametersList);

                // Handle generic type parameters with constraints
                if (methodSymbol.TypeParameters.Length > 0)
                {
                    var genericParams = methodSymbol.TypeParameters
                        .Select(typeParam => new { typeParam, constraints = GetTypeParameterConstraints(typeParam) })
                        .Where(t => !string.IsNullOrEmpty(t.constraints))
                        .Select(t => $"Type parameter: {t.typeParam.Name} {t.constraints}");

                    parameters.AddRange(genericParams);
                }

                break;
            }
            case IPropertySymbol { IsIndexer: true } propertySymbol:
            {
                foreach (var param in propertySymbol.Parameters)
                {
                    var paramType = param.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                    parameters.Add($"{paramType} {param.Name}");
                }

                break;
            }
        }

        return parameters;
    }

    private string GetTypeParameterConstraints(ITypeParameterSymbol typeParam)
    {
        var constraints = new List<string>();

        if (typeParam.HasReferenceTypeConstraint)
            constraints.Add("class");
        if (typeParam.HasValueTypeConstraint)
            constraints.Add("struct");
        if (typeParam.HasUnmanagedTypeConstraint)
            constraints.Add("unmanaged");
        if (typeParam.HasNotNullConstraint)
            constraints.Add("notnull");

        constraints.AddRange(
            typeParam.ConstraintTypes
                .Select(constraintType =>
                    constraintType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat))
        );

        if (typeParam.HasConstructorConstraint)
            constraints.Add("new()");

        return constraints.Count > 0 ? $"where {typeParam.Name} : {string.Join(", ", constraints)}" : string.Empty;
    }

    private string? ExtractReturnType(ISymbol symbol)
    {
        return symbol switch
        {
            IMethodSymbol { ReturnsVoid: true } => null,
            IMethodSymbol methodSymbol => methodSymbol.ReturnType.ToDisplayString(SymbolDisplayFormat
                .MinimallyQualifiedFormat),
            IPropertySymbol propertySymbol => propertySymbol.Type.ToDisplayString(SymbolDisplayFormat
                .MinimallyQualifiedFormat),
            _ => null
        };
    }

    private List<string> ExtractInheritanceHierarchy(ISymbol symbol)
    {
        var hierarchy = new List<string>();

        if (symbol.ContainingType != null)
        {
            var currentType = symbol.ContainingType;

            // Traverse base types
            while (currentType?.BaseType != null && currentType.BaseType.SpecialType != SpecialType.System_Object)
            {
                hierarchy.Add(currentType.BaseType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat
                    .WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted)));
                currentType = currentType.BaseType;
            }

            // Add implemented interfaces
            currentType = symbol.ContainingType;
            foreach (var interfaceType in currentType.Interfaces)
                hierarchy.Add(interfaceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat
                    .WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted)));
        }

        return hierarchy;
    }

    private List<string> ExtractRelatedTypes(ISymbol symbol, List<string> parameterTypes, string? returnType)
    {
        var relatedTypes = new HashSet<string>();

        // Extract types from parameters
        foreach (var param in parameterTypes)
        {
            var typeName = param.Split(' ').FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(typeName) && !typeName.StartsWith("Type parameter:"))
                relatedTypes.Add(typeName);
        }

        // Add return type
        if (!string.IsNullOrWhiteSpace(returnType)) relatedTypes.Add(returnType);

        // Extract generic type arguments if present
        if (symbol is IMethodSymbol methodSymbol)
            foreach (var typeArg in methodSymbol.TypeArguments)
                relatedTypes.Add(typeArg.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));

        return relatedTypes.ToList();
    }

    private string? ExtractXmlDocumentation(ISymbol symbol)
    {
        var docs = new List<string>();

        // Check for overridden base method documentation
        if (symbol is IMethodSymbol methodSymbol && methodSymbol.OverriddenMethod != null)
        {
            var baseDoc = methodSymbol.OverriddenMethod.GetDocumentationCommentXml();
            if (!string.IsNullOrWhiteSpace(baseDoc)) docs.Add($"Base method documentation:\n{baseDoc}");
        }

        // Check for interface implementation documentation
        if (symbol is IMethodSymbol method)
        {
            foreach (var interfaceMethod in method.ExplicitInterfaceImplementations)
            {
                var interfaceDoc = interfaceMethod.GetDocumentationCommentXml();
                if (!string.IsNullOrWhiteSpace(interfaceDoc))
                    docs.Add(
                        $"Interface method documentation ({interfaceMethod.ContainingType.Name}):\n{interfaceDoc}");
            }

            // Also check implicit interface implementations
            var containingType = method.ContainingType;
            if (containingType != null)
                foreach (var @interface in containingType.AllInterfaces)
                foreach (var interfaceMember in @interface.GetMembers())
                    if (interfaceMember is IMethodSymbol interfaceMethod &&
                        method.Name == interfaceMethod.Name &&
                        ParameterTypesMatch(method.Parameters, interfaceMethod.Parameters))
                    {
                        var interfaceDoc = interfaceMethod.GetDocumentationCommentXml();
                        if (!string.IsNullOrWhiteSpace(interfaceDoc))
                            docs.Add(
                                $"Interface method documentation ({@interface.Name}.{interfaceMethod.Name}):\n{interfaceDoc}");
                    }
        }

        return docs.Count > 0 ? string.Join("\n\n", docs) : null;
    }

    private bool ParameterTypesMatch(ImmutableArray<IParameterSymbol> params1, ImmutableArray<IParameterSymbol> params2)
    {
        if (params1.Length != params2.Length)
            return false;

        for (var i = 0; i < params1.Length; i++)
            if (!SymbolEqualityComparer.Default.Equals(params1[i].Type, params2[i].Type))
                return false;

        return true;
    }

    private int EstimateTokenCount(List<string> parameters, string? returnType, List<string> hierarchy, string? xmlDocs)
    {
        // Simple heuristic: character count / 4 (rough approximation for English text tokens)
        var charCount = 0;

        charCount += parameters.Sum(p => p.Length);
        charCount += returnType?.Length ?? 0;
        charCount += hierarchy.Sum(h => h.Length);
        charCount += xmlDocs?.Length ?? 0;

        return charCount / 4;
    }

    /// <summary>
    /// Serializes the API context to JSON format optimized for LLM input.
    /// </summary>
    public string SerializeToJson(ApiContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var options = new JsonSerializerOptions
        {
            WriteIndented = false, PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        return JsonSerializer.Serialize(context, options);
    }

    /// <summary>
    /// Formats the API context as plain text optimized for LLM readability.
    /// </summary>
    public string FormatAsPlainText(ApiContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var sb = new StringBuilder();

        sb.AppendLine($"API Symbol: {context.ApiSymbolId}");
        sb.AppendLine();

        if (context.ParameterTypes.Count > 0)
        {
            sb.AppendLine("Parameters:");
            foreach (var param in context.ParameterTypes) sb.AppendLine($"  - {param}");

            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(context.ReturnType))
        {
            sb.AppendLine($"Return Type: {context.ReturnType}");
            sb.AppendLine();
        }

        if (context.InheritanceHierarchy.Count > 0)
        {
            sb.AppendLine("Inheritance:");
            foreach (var type in context.InheritanceHierarchy)
                sb.AppendLine($"  - {type}");

            sb.AppendLine();
        }

        if (context.RelatedTypes.Count > 0)
        {
            sb.AppendLine("Related Types:");
            foreach (var type in context.RelatedTypes)
                sb.AppendLine($"  - {type}");

            sb.AppendLine();
        }

        if (string.IsNullOrWhiteSpace(context.XmlDocComments))
            return sb.ToString().TrimEnd();

        sb.AppendLine("Related Documentation:");
        foreach (var line in context.XmlDocComments.Split('\n'))
            sb.AppendLine($"  > {line.TrimEnd()}");

        return sb.ToString().TrimEnd();
    }
}
