using Docify.Core.Models;
using Docify.LLM.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;

namespace Docify.LLM.ContextCollection;

/// <summary>
/// Collects signature and type information for API symbols using Roslyn semantic analysis.
/// </summary>
public class SignatureContextCollector(
    ILogger<SignatureContextCollector> logger,
    ICallSiteCollector callSiteCollector,
    IStalenessDetector? stalenessDetector = null) : IContextCollector
{
    private static readonly SymbolDisplayFormat _typeDisplayFormat = SymbolDisplayFormat.MinimallyQualifiedFormat
        .WithMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier | SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    /// <inheritdoc/>
    public async Task<ApiContext> CollectContext(
        ApiSymbol symbol,
        Compilation compilation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(symbol);
        ArgumentNullException.ThrowIfNull(compilation);

        var roslynSymbol = await GetRoslynSymbol(symbol, compilation, cancellationToken);

        if (roslynSymbol == null)
            throw new InvalidOperationException($"Could not find Roslyn symbol for {symbol.FullyQualifiedName}");

        var parameterTypes = ExtractParameterTypes(roslynSymbol);
        var returnType = ExtractReturnType(roslynSymbol);
        var inheritanceHierarchy = ExtractInheritanceHierarchy(roslynSymbol);
        var relatedTypes = ExtractRelatedTypes(roslynSymbol, parameterTypes, returnType);
        var xmlDocComments = ExtractXmlDocumentation(roslynSymbol);

        // Collect call sites (usage examples)
        var callSites = await callSiteCollector.CollectCallSites(symbol, compilation, cancellationToken: cancellationToken);

        // Extract implementation body
        var implementationBody = ExtractImplementationBody(roslynSymbol);
        var isImplementationTruncated = false;

        if (implementationBody != null)
        {
            var bodyTokenCount = implementationBody.Length / 4;
            if (bodyTokenCount > 500)
            {
                implementationBody = implementationBody[..2000] + "\n[... implementation truncated for token budget ...]";
                isImplementationTruncated = true;
            }
        }

        // Analyze internal method calls
        var calledMethods = AnalyzeInternalMethodCalls(roslynSymbol, compilation);
        var calledMethodsDocs = CollectCalledMethodsDocumentation(symbol, calledMethods);

        var tokenEstimate = EstimateTokenCount(parameterTypes, returnType, inheritanceHierarchy, xmlDocComments, callSites, implementationBody, calledMethodsDocs);

        logger.LogDebug("Collected signature context for {ApiName}", symbol.FullyQualifiedName);

        return new ApiContext
        {
            ApiSymbolId = symbol.Id,
            ParameterTypes = parameterTypes,
            ReturnType = returnType,
            InheritanceHierarchy = inheritanceHierarchy,
            RelatedTypes = relatedTypes,
            XmlDocComments = xmlDocComments,
            TokenEstimate = tokenEstimate,
            CallSites = callSites,
            ImplementationBody = implementationBody,
            CalledMethodsDocumentation = calledMethodsDocs,
            IsImplementationTruncated = isImplementationTruncated
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
            var root = await syntaxTree.GetRootAsync(cancellationToken);

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

    private string GetFullyQualifiedName(ISymbol symbol) => symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    private List<string> ExtractParameterTypes(ISymbol symbol)
    {
        var parameters = new List<string>();

        switch (symbol)
        {
            case IMethodSymbol methodSymbol:
            {
                var parametersList = methodSymbol.Parameters
                    .Select(x => $"{x.Type.ToDisplayString(_typeDisplayFormat)} {x.Name}");

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
                    var paramType = param.Type.ToDisplayString(_typeDisplayFormat);
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
            IMethodSymbol methodSymbol => methodSymbol.ReturnType.ToDisplayString(_typeDisplayFormat),
            IPropertySymbol propertySymbol => propertySymbol.Type.ToDisplayString(_typeDisplayFormat),
            _ => null
        };
    }

    private List<string> ExtractInheritanceHierarchy(ISymbol symbol)
    {
        var hierarchy = new List<string>();

        // For type symbols (classes, interfaces), extract their own inheritance hierarchy
        if (symbol is INamedTypeSymbol namedTypeSymbol)
        {
            // Traverse base types
            var currentType = namedTypeSymbol.BaseType;
            while (currentType != null && currentType.SpecialType != SpecialType.System_Object)
            {
                hierarchy.Add(currentType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                currentType = currentType.BaseType;
            }

            // Add implemented interfaces
            hierarchy.AddRange(
                namedTypeSymbol.Interfaces
                    .Select(interfaceType => interfaceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
        }
        // For members, extract the containing type's hierarchy
        else if (symbol.ContainingType != null)
        {
            var currentType = symbol.ContainingType;

            // Traverse base types
            while (currentType?.BaseType != null && currentType.BaseType.SpecialType != SpecialType.System_Object)
            {
                hierarchy.Add(currentType.BaseType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                currentType = currentType.BaseType;
            }

            // Add implemented interfaces
            currentType = symbol.ContainingType;
            foreach (var interfaceType in currentType.Interfaces)
                hierarchy.Add(interfaceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
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

    private int EstimateTokenCount(List<string> parameters, string? returnType, List<string> hierarchy, string? xmlDocs, List<CallSiteInfo> callSites, string? implementationBody, List<CalledMethodDoc> calledMethodsDocs)
    {
        // Simple heuristic: character count / 4 (rough approximation for English text tokens)
        var charCount = 0;

        charCount += parameters.Sum(p => p.Length);
        charCount += returnType?.Length ?? 0;
        charCount += hierarchy.Sum(h => h.Length);
        charCount += xmlDocs?.Length ?? 0;

        // Include call site context in token estimate
        foreach (var callSite in callSites)
        {
            charCount += callSite.FilePath.Length;
            charCount += callSite.CallExpression.Length;
            charCount += callSite.ContextBefore.Sum(line => line.Length);
            charCount += callSite.ContextAfter.Sum(line => line.Length);
        }

        // Include implementation body
        charCount += implementationBody?.Length ?? 0;

        // Include called methods documentation
        foreach (var calledMethodDoc in calledMethodsDocs)
        {
            charCount += calledMethodDoc.MethodName.Length;
            charCount += calledMethodDoc.XmlDocumentation.Length;
        }

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

        if (!string.IsNullOrWhiteSpace(context.XmlDocComments))
        {
            sb.AppendLine("Related Documentation:");
            foreach (var line in context.XmlDocComments.Split('\n'))
                sb.AppendLine($"  > {line.TrimEnd()}");

            sb.AppendLine();
        }

        // Add usage examples
        if (context.CallSites.Count > 0)
        {
            sb.AppendLine("Usage Examples:");
            foreach (var callSite in context.CallSites)
            {
                sb.AppendLine($"  File: {callSite.FilePath}:{callSite.LineNumber}");

                // Context before
                foreach (var line in callSite.ContextBefore)
                {
                    sb.AppendLine($"  > {line}");
                }

                // Call expression (highlight it)
                sb.AppendLine($"  > {callSite.CallExpression}  // <-- call site");

                // Context after
                foreach (var line in callSite.ContextAfter)
                {
                    sb.AppendLine($"  > {line}");
                }

                sb.AppendLine();
            }
        }

        // Add implementation body
        if (!string.IsNullOrWhiteSpace(context.ImplementationBody))
        {
            sb.AppendLine("Implementation:");
            foreach (var line in context.ImplementationBody.Split('\n'))
                sb.AppendLine($"  > {line.TrimEnd()}");

            if (context.IsImplementationTruncated)
                sb.AppendLine("  > [Note: Implementation truncated for token budget]");

            sb.AppendLine();
        }

        if (context.CalledMethodsDocumentation.Count <= 0)
            return sb.ToString().TrimEnd();

        sb.AppendLine("Called Methods Documentation:");
        foreach (var calledMethod in context.CalledMethodsDocumentation)
        {
            sb.AppendLine($"  Called method '{calledMethod.MethodName}':");
            foreach (var line in calledMethod.XmlDocumentation.Split('\n'))
                sb.AppendLine($"    > {line.TrimEnd()}");
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    private string? ExtractImplementationBody(ISymbol symbol)
    {
        var syntaxReference = symbol.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxReference == null)
            return null;

        var syntaxNode = syntaxReference.GetSyntax();

        return syntaxNode switch
        {
            MethodDeclarationSyntax methodDecl => ExtractMethodBody(methodDecl),
            PropertyDeclarationSyntax propertyDecl => ExtractPropertyBody(propertyDecl),
            IndexerDeclarationSyntax indexerDecl => ExtractIndexerBody(indexerDecl),
            _ => null
        };
    }

    private string? ExtractMethodBody(MethodDeclarationSyntax methodDecl)
    {
        if (methodDecl.Body != null)
            return methodDecl.Body.ToFullString();

        if (methodDecl.ExpressionBody != null)
            return methodDecl.ExpressionBody.ToFullString();

        return null;
    }

    private string? ExtractPropertyBody(PropertyDeclarationSyntax propertyDecl)
    {
        if (propertyDecl.ExpressionBody != null)
            return propertyDecl.ExpressionBody.ToFullString();

        if (propertyDecl.AccessorList != null)
            return propertyDecl.AccessorList.ToFullString();

        return null;
    }

    private string? ExtractIndexerBody(IndexerDeclarationSyntax indexerDecl)
    {
        if (indexerDecl.ExpressionBody != null)
            return indexerDecl.ExpressionBody.ToFullString();

        if (indexerDecl.AccessorList != null)
            return indexerDecl.AccessorList.ToFullString();

        return null;
    }

    private List<IMethodSymbol> AnalyzeInternalMethodCalls(ISymbol symbol, Compilation compilation)
    {
        var calledMethods = new List<IMethodSymbol>();

        var syntaxReference = symbol.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxReference == null)
            return calledMethods;

        var syntaxNode = syntaxReference.GetSyntax();
        var bodySyntax = GetBodySyntax(syntaxNode);

        if (bodySyntax == null)
            return calledMethods;

        var semanticModel = compilation.GetSemanticModel(syntaxReference.SyntaxTree);
        var invocations = bodySyntax.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var symbolInfo = semanticModel.GetSymbolInfo(invocation);
            if (symbolInfo.Symbol is IMethodSymbol methodSymbol)
            {
                // Filter to only include methods from same project (exclude external libraries)
                if (SymbolEqualityComparer.Default.Equals(methodSymbol.ContainingAssembly, symbol.ContainingAssembly))
                {
                    calledMethods.Add(methodSymbol);
                }
            }
        }

        return calledMethods;
    }

    private SyntaxNode? GetBodySyntax(SyntaxNode syntaxNode)
    {
        return syntaxNode switch
        {
            MethodDeclarationSyntax methodDecl => (SyntaxNode?)methodDecl.Body ?? methodDecl.ExpressionBody,
            PropertyDeclarationSyntax propertyDecl => (SyntaxNode?)propertyDecl.AccessorList ?? propertyDecl.ExpressionBody,
            IndexerDeclarationSyntax indexerDecl => (SyntaxNode?)indexerDecl.AccessorList ?? indexerDecl.ExpressionBody,
            _ => null
        };
    }

    private List<CalledMethodDoc> CollectCalledMethodsDocumentation(ApiSymbol symbol, List<IMethodSymbol> calledMethods)
    {
        var calledMethodsDocs = new List<CalledMethodDoc>();

        if (stalenessDetector == null)
        {
            logger.LogDebug("Git unavailable, treating all called method documentation as fresh");
        }

        foreach (var methodSymbol in calledMethods)
        {
            var xmlDoc = methodSymbol.GetDocumentationCommentXml();
            if (string.IsNullOrWhiteSpace(xmlDoc))
                continue;

            // Check staleness if detector available
            bool isFresh = true;
            if (stalenessDetector != null)
            {
                // Create temporary ApiSymbol for called method to check staleness
                var calledMethodSymbol = new ApiSymbol
                {
                    Id = Guid.NewGuid().ToString(),
                    SymbolType = Core.Models.SymbolType.Method,
                    FullyQualifiedName = GetFullyQualifiedName(methodSymbol),
                    FilePath = methodSymbol.Locations.FirstOrDefault()?.SourceTree?.FilePath ?? string.Empty,
                    LineNumber = methodSymbol.Locations.FirstOrDefault()?.GetLineSpan().StartLinePosition.Line ?? 0,
                    Signature = methodSymbol.ToDisplayString(),
                    AccessModifier = methodSymbol.DeclaredAccessibility.ToString(),
                    IsStatic = methodSymbol.IsStatic,
                    HasDocumentation = !string.IsNullOrWhiteSpace(xmlDoc),
                    DocumentationStatus = DocumentationStatus.Documented
                };

                var stalenessResult = stalenessDetector.DetectStaleDocumentation(calledMethodSymbol);
                isFresh = !stalenessResult.IsStale;
            }

            // Only include fresh documentation
            if (isFresh)
            {
                calledMethodsDocs.Add(new CalledMethodDoc
                {
                    MethodName = GetFullyQualifiedName(methodSymbol),
                    XmlDocumentation = xmlDoc,
                    IsFresh = true
                });
            }
        }

        return calledMethodsDocs;
    }
}
