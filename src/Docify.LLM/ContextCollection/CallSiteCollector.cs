using Docify.Core.Models;
using Docify.LLM.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.Extensions.Logging;

namespace Docify.LLM.ContextCollection;

/// <summary>
/// Collects usage examples (call sites) for API symbols using Roslyn's FindReferences API.
/// </summary>
public class CallSiteCollector(ILogger<CallSiteCollector> logger) : ICallSiteCollector
{
    /// <inheritdoc/>
    public async Task<List<CallSiteInfo>> CollectCallSites(
        ApiSymbol symbol,
        Compilation compilation,
        int maxExamples = 5,
        int contextLines = 3,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(symbol);
        ArgumentNullException.ThrowIfNull(compilation);

        var roslynSymbol = await GetRoslynSymbol(symbol, compilation, cancellationToken);
        if (roslynSymbol == null)
        {
            logger.LogDebug("Could not find Roslyn symbol for {ApiName}, skipping call site collection",
                symbol.FullyQualifiedName);
            return [];
        }

        var solution = CreateSolutionFromCompilation(compilation);
        var references = await SymbolFinder.FindReferencesAsync(
            roslynSymbol,
            solution,
            cancellationToken);

        var callSiteLocations = new List<Location>();
        foreach (var reference in references)
            foreach (var location in reference.Locations)
            {
                // Filter out the definition location
                if (!location.Location.IsInSource || location.IsImplicit)
                    continue;

                // Only include actual call sites (not definition)
                if (location.Location.SourceTree != null)
                    callSiteLocations.Add(location.Location);
            }

        if (callSiteLocations.Count == 0)
        {
            logger.LogDebug("No call sites found for {ApiName}", symbol.FullyQualifiedName);
            return [];
        }

        logger.LogDebug("Found {Count} call sites for {ApiName}", callSiteLocations.Count, symbol.FullyQualifiedName);

        // Prioritize diverse usage patterns if we have more than maxExamples
        var selectedLocations = callSiteLocations.Count <= maxExamples
            ? callSiteLocations
            : PrioritizeDiverseCallSites(callSiteLocations, maxExamples);

        if (selectedLocations.Count < callSiteLocations.Count)
            logger.LogDebug("Selected {Count} diverse usage examples from {TotalFound} call sites",
                selectedLocations.Count, callSiteLocations.Count);

        var callSites = new List<CallSiteInfo>();
        foreach (var location in selectedLocations)
        {
            var callSiteInfo = await ExtractCallSiteContext(location, contextLines, cancellationToken);
            if (callSiteInfo != null) callSites.Add(callSiteInfo);
        }

        return callSites;
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

    private string GetFullyQualifiedName(ISymbol symbol)
    {
        return symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat
            .WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted));
    }

    private Solution CreateSolutionFromCompilation(Compilation compilation)
    {
        // Create an ad-hoc workspace with MEF host services for C# language support
        var host = MefHostServices.Create(MefHostServices.DefaultAssemblies);
        var workspace = new AdhocWorkspace(host);
        var projectInfo = ProjectInfo.Create(
            ProjectId.CreateNewId(),
            VersionStamp.Create(),
            "TempProject",
            "TempProject",
            LanguageNames.CSharp,
            compilationOptions: compilation.Options,
            metadataReferences: compilation.References);

        var project = workspace.AddProject(projectInfo);

        foreach (var syntaxTree in compilation.SyntaxTrees)
        {
            var text = syntaxTree.GetText();
            project = project.AddDocument(
                syntaxTree.FilePath ?? "Unknown.cs",
                text,
                filePath: syntaxTree.FilePath).Project;
        }

        return project.Solution;
    }

    private List<Location> PrioritizeDiverseCallSites(List<Location> locations, int maxExamples)
    {
        // Score each location based on diversity criteria
        var scoredLocations = locations.Select(loc => new
        {
            Location = loc,
            Score = CalculateDiversityScore(loc, locations)
        }).OrderByDescending(x => x.Score)
          .Take(maxExamples)
          .Select(x => x.Location)
          .ToList();

        return scoredLocations;
    }

    private double CalculateDiversityScore(Location location, List<Location> allLocations)
    {
        double score = 0;

        // Prefer unique file paths (higher score for files with fewer call sites)
        var filePath = location.SourceTree?.FilePath ?? "";
        var callSitesInSameFile = allLocations.Count(l => l.SourceTree?.FilePath == filePath);
        score += 100.0 / callSitesInSameFile;

        // Prefer different line numbers (spread across file)
        var lineSpan = location.GetLineSpan();
        score += lineSpan.StartLinePosition.Line * 0.01;

        return score;
    }

    private async Task<CallSiteInfo?> ExtractCallSiteContext(
        Location location,
        int contextLines,
        CancellationToken cancellationToken)
    {
        if (location.SourceTree == null)
            return null;

        var text = await location.SourceTree.GetTextAsync(cancellationToken);
        var lineSpan = location.GetLineSpan();
        var callLine = lineSpan.StartLinePosition.Line;

        var startLine = Math.Max(0, callLine - contextLines);
        var endLine = Math.Min(text.Lines.Count - 1, callLine + contextLines);

        var contextBefore = new List<string>();
        for (var i = startLine; i < callLine; i++)
            contextBefore.Add(text.Lines[i].ToString());

        var callExpression = text.Lines[callLine].ToString();

        var contextAfter = new List<string>();
        for (var i = callLine + 1; i <= endLine; i++)
            contextAfter.Add(text.Lines[i].ToString());

        return new CallSiteInfo
        {
            FilePath = location.SourceTree.FilePath ?? "Unknown",
            LineNumber = callLine + 1, // 1-based line numbering
            ContextBefore = contextBefore,
            CallExpression = callExpression,
            ContextAfter = contextAfter
        };
    }
}
