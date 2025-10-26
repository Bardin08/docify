using Docify.Core.Interfaces;
using Docify.Core.Models;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace Docify.Core.Analyzers;

/// <summary>
/// Extracts public and protected API symbols from Roslyn compilations.
/// </summary>
public class SymbolExtractor(ILogger<SymbolExtractor> logger, IDocumentationDetector documentationDetector)
    : ISymbolExtractor
{
    /// <summary>
    /// Extracts all public and protected API symbols from the specified compilation.
    /// </summary>
    public async Task<List<ApiSymbol>> ExtractPublicSymbols(Compilation compilation)
    {
        ArgumentNullException.ThrowIfNull(compilation);

        var symbols = new List<ApiSymbol>();
        var syntaxTreeCount = compilation.SyntaxTrees.Count();

        logger.LogInformation("Extracting symbols from {TreeCount} syntax trees", syntaxTreeCount);

        await Task.Run(() =>
        {
            foreach (var syntaxTree in compilation.SyntaxTrees)
            {
                var semanticModel = compilation.GetSemanticModel(syntaxTree);
                var root = syntaxTree.GetRoot();

                // Walk the syntax tree and extract symbols
                var visitor = new SymbolVisitor(semanticModel, symbols, documentationDetector);
                visitor.Visit(root);
            }
        });

        logger.LogInformation("Extracted {SymbolCount} public symbols", symbols.Count);

        return symbols;
    }

    /// <summary>
    /// Syntax walker that discovers public and protected symbols.
    /// </summary>
    private class SymbolVisitor(
        SemanticModel semanticModel,
        List<ApiSymbol> symbols,
        IDocumentationDetector documentationDetector)
        : Microsoft.CodeAnalysis.CSharp.CSharpSyntaxWalker
    {
        public override void VisitClassDeclaration(Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax node)
        {
            ProcessTypeDeclaration(node, SymbolType.Class);
            base.VisitClassDeclaration(node);
        }

        public override void VisitStructDeclaration(Microsoft.CodeAnalysis.CSharp.Syntax.StructDeclarationSyntax node)
        {
            ProcessTypeDeclaration(node, SymbolType.Struct);
            base.VisitStructDeclaration(node);
        }

        public override void VisitInterfaceDeclaration(
            Microsoft.CodeAnalysis.CSharp.Syntax.InterfaceDeclarationSyntax node)
        {
            ProcessTypeDeclaration(node, SymbolType.Interface);
            base.VisitInterfaceDeclaration(node);
        }

        public override void VisitEnumDeclaration(Microsoft.CodeAnalysis.CSharp.Syntax.EnumDeclarationSyntax node)
        {
            ProcessTypeDeclaration(node, SymbolType.Enum);
            base.VisitEnumDeclaration(node);
        }

        public override void VisitDelegateDeclaration(
            Microsoft.CodeAnalysis.CSharp.Syntax.DelegateDeclarationSyntax node)
        {
            ProcessMemberDeclaration(node, SymbolType.Delegate);
            base.VisitDelegateDeclaration(node);
        }

        public override void VisitMethodDeclaration(Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax node)
        {
            ProcessMemberDeclaration(node, SymbolType.Method);
            base.VisitMethodDeclaration(node);
        }

        public override void VisitPropertyDeclaration(
            Microsoft.CodeAnalysis.CSharp.Syntax.PropertyDeclarationSyntax node)
        {
            ProcessMemberDeclaration(node, SymbolType.Property);
            base.VisitPropertyDeclaration(node);
        }

        public override void VisitIndexerDeclaration(Microsoft.CodeAnalysis.CSharp.Syntax.IndexerDeclarationSyntax node)
        {
            ProcessMemberDeclaration(node, SymbolType.Indexer);
            base.VisitIndexerDeclaration(node);
        }

        public override void VisitEventDeclaration(Microsoft.CodeAnalysis.CSharp.Syntax.EventDeclarationSyntax node)
        {
            ProcessMemberDeclaration(node, SymbolType.Event);
            base.VisitEventDeclaration(node);
        }

        public override void VisitEventFieldDeclaration(
            Microsoft.CodeAnalysis.CSharp.Syntax.EventFieldDeclarationSyntax node)
        {
            foreach (var variable in node.Declaration.Variables)
            {
                var symbol = semanticModel.GetDeclaredSymbol(variable);
                if (symbol != null && IsPublicOrProtected(symbol))
                    symbols.Add(CreateApiSymbol(variable, symbol, SymbolType.Event));
            }

            base.VisitEventFieldDeclaration(node);
        }

        private void ProcessTypeDeclaration(Microsoft.CodeAnalysis.CSharp.Syntax.BaseTypeDeclarationSyntax node,
            SymbolType symbolType)
        {
            var symbol = semanticModel.GetDeclaredSymbol(node);
            if (symbol is INamedTypeSymbol typeSymbol && IsPublicOrProtected(typeSymbol))
                symbols.Add(CreateApiSymbol(node, typeSymbol, symbolType));
        }

        private void ProcessMemberDeclaration(Microsoft.CodeAnalysis.CSharp.Syntax.MemberDeclarationSyntax node,
            SymbolType symbolType)
        {
            var symbol = semanticModel.GetDeclaredSymbol(node);
            if (symbol != null && IsPublicOrProtected(symbol))
                symbols.Add(CreateApiSymbol(node, symbol, symbolType));
        }

        private static bool IsPublicOrProtected(ISymbol symbol)
        {
            return symbol.DeclaredAccessibility is Accessibility.Public
                or Accessibility.Protected
                or Accessibility.ProtectedOrInternal;
        }

        private ApiSymbol CreateApiSymbol(SyntaxNode node, ISymbol symbol, SymbolType symbolType)
        {
            var location = node.GetLocation();
            var lineSpan = location.GetLineSpan();
            var filePath = lineSpan.Path;
            var lineNumber = lineSpan.StartLinePosition.Line + 1;

            var fullyQualifiedName = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var signature = symbol.ToDisplayString();
            var accessModifier = symbol.DeclaredAccessibility.ToString();
            var isStatic = symbol.IsStatic;

            var documentationStatus = documentationDetector.DetectDocumentationStatus(symbol);
            var hasDocumentation = documentationStatus != DocumentationStatus.Undocumented;

            return new ApiSymbol
            {
                Id = Guid.NewGuid().ToString(),
                SymbolType = symbolType,
                FullyQualifiedName = fullyQualifiedName,
                FilePath = filePath,
                LineNumber = lineNumber,
                Signature = signature,
                AccessModifier = accessModifier,
                IsStatic = isStatic,
                HasDocumentation = hasDocumentation,
                DocumentationStatus = documentationStatus
            };
        }
    }
}
