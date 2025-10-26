using Docify.Core.Analyzers;
using Docify.Core.Interfaces;
using Docify.Core.Models;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Xunit;

namespace Docify.Integration.Tests.EndToEnd;

public class RoslynIntegrationTests
{
    [Fact]
    public async Task AnalyzeProject_SimpleLibrary_ReturnsValidAnalysisResult()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<RoslynAnalyzer>>();
        var mockSymbolExtractorLogger = new Mock<ILogger<SymbolExtractor>>();
        var mockDetectorLogger = new Mock<ILogger<DocumentationDetector>>();
        var documentationDetector = new DocumentationDetector(mockDetectorLogger.Object);
        var symbolExtractor = new SymbolExtractor(mockSymbolExtractorLogger.Object, documentationDetector);
        var analyzer = new RoslynAnalyzer(mockLogger.Object, symbolExtractor);
        var projectPath = Path.GetFullPath("../../../../samples/SimpleLibrary/SimpleLibrary.csproj");

        // Skip test if sample project doesn't exist (e.g., in CI without sample)
        if (!File.Exists(projectPath))
        {
            return;
        }

        // Act
        var result = await analyzer.AnalyzeProject(projectPath);

        // Assert
        result.ShouldNotBeNull();
        result.ProjectPath.ShouldBe(projectPath);
        result.TotalDocuments.ShouldBeGreaterThan(0);
        result.Compilation.ShouldNotBeNull();
        result.SyntaxTrees.ShouldNotBeEmpty();

        // Verify Calculator.cs is included
        result.SyntaxTrees.Any(st => st.FilePath.Contains("Calculator.cs")).ShouldBeTrue();

        // Log success
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Analysis complete")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task AnalyzeProject_SimpleLibrary_NoCompilationErrors()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<RoslynAnalyzer>>();
        var mockSymbolExtractorLogger = new Mock<ILogger<SymbolExtractor>>();
        var mockDetectorLogger = new Mock<ILogger<DocumentationDetector>>();
        var documentationDetector = new DocumentationDetector(mockDetectorLogger.Object);
        var symbolExtractor = new SymbolExtractor(mockSymbolExtractorLogger.Object, documentationDetector);
        var analyzer = new RoslynAnalyzer(mockLogger.Object, symbolExtractor);
        var projectPath = Path.GetFullPath("../../../../samples/SimpleLibrary/SimpleLibrary.csproj");

        // Skip test if sample project doesn't exist
        if (!File.Exists(projectPath))
        {
            return;
        }

        // Act
        var result = await analyzer.AnalyzeProject(projectPath);

        // Assert
        result.HasErrors.ShouldBeFalse();
        result.DiagnosticMessages.ShouldBeEmpty();
    }

    [Fact]
    public async Task AnalyzeProject_SimpleLibrary_ExtractsExpectedPublicApis()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<RoslynAnalyzer>>();
        var mockSymbolExtractorLogger = new Mock<ILogger<SymbolExtractor>>();
        var mockDetectorLogger = new Mock<ILogger<DocumentationDetector>>();
        var documentationDetector = new DocumentationDetector(mockDetectorLogger.Object);
        var symbolExtractor = new SymbolExtractor(mockSymbolExtractorLogger.Object, documentationDetector);
        var analyzer = new RoslynAnalyzer(mockLogger.Object, symbolExtractor);
        var projectPath = Path.GetFullPath("../../../../samples/SimpleLibrary/SimpleLibrary.csproj");

        // Skip test if sample project doesn't exist
        if (!File.Exists(projectPath))
        {
            return;
        }

        // Act
        var result = await analyzer.AnalyzeProject(projectPath);

        // Assert
        result.PublicApis.ShouldNotBeNull();
        result.PublicApis.ShouldNotBeEmpty();

        // Verify we have a Calculator class
        var calculatorClass = result.PublicApis.FirstOrDefault(api =>
            api.SymbolType == Docify.Core.Models.SymbolType.Class &&
            api.FullyQualifiedName.Contains("Calculator"));
        calculatorClass.ShouldNotBeNull();

        // Verify we have Add method
        var addMethod = result.PublicApis.FirstOrDefault(api =>
            api.SymbolType == Docify.Core.Models.SymbolType.Method &&
            api.Signature.Contains("Add"));
        addMethod.ShouldNotBeNull();

        // Verify all symbols have required properties
        foreach (var api in result.PublicApis)
        {
            api.Id.ShouldNotBeNullOrWhiteSpace();
            api.FullyQualifiedName.ShouldNotBeNullOrWhiteSpace();
            api.FilePath.ShouldNotBeNullOrWhiteSpace();
            api.LineNumber.ShouldBeGreaterThan(0);
            api.Signature.ShouldNotBeNullOrWhiteSpace();
            api.AccessModifier.ShouldNotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public async Task AnalyzeProject_SimpleLibrary_DetectsDocumentationStatus()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<RoslynAnalyzer>>();
        var mockSymbolExtractorLogger = new Mock<ILogger<SymbolExtractor>>();
        var mockDetectorLogger = new Mock<ILogger<DocumentationDetector>>();
        var documentationDetector = new DocumentationDetector(mockDetectorLogger.Object);
        var symbolExtractor = new SymbolExtractor(mockSymbolExtractorLogger.Object, documentationDetector);
        var analyzer = new RoslynAnalyzer(mockLogger.Object, symbolExtractor);
        var projectPath = Path.GetFullPath("../../../../samples/SimpleLibrary/SimpleLibrary.csproj");

        // Skip test if sample project doesn't exist
        if (!File.Exists(projectPath))
        {
            return;
        }

        // Act
        var result = await analyzer.AnalyzeProject(projectPath);

        // Assert
        result.ShouldNotBeNull();
        result.PublicApis.ShouldNotBeEmpty();

        // Verify documentation summary is populated
        result.DocumentationSummary.ShouldNotBeNull();
        result.DocumentationSummary.TotalApis.ShouldBe(result.PublicApis.Count);

        // Verify all symbols have documentation properties set
        foreach (var api in result.PublicApis)
        {
            // DocumentationStatus should be set to one of the valid values
            api.DocumentationStatus.ShouldBeOneOf(
                DocumentationStatus.Undocumented,
                DocumentationStatus.PartiallyDocumented,
                DocumentationStatus.Documented,
                DocumentationStatus.Stale);

            // HasDocumentation should match status
            if (api.DocumentationStatus == DocumentationStatus.Undocumented)
            {
                api.HasDocumentation.ShouldBeFalse();
            }
            else
            {
                api.HasDocumentation.ShouldBeTrue();
            }
        }

        // Verify summary counts match actual symbols
        var documentedCount = result.PublicApis.Count(a => a.DocumentationStatus == DocumentationStatus.Documented);
        var undocumentedCount = result.PublicApis.Count(a => a.DocumentationStatus == DocumentationStatus.Undocumented);
        var partiallyDocumentedCount = result.PublicApis.Count(a => a.DocumentationStatus == DocumentationStatus.PartiallyDocumented);

        result.DocumentationSummary.DocumentedCount.ShouldBe(documentedCount);
        result.DocumentationSummary.UndocumentedCount.ShouldBe(undocumentedCount);
        result.DocumentationSummary.PartiallyDocumentedCount.ShouldBe(partiallyDocumentedCount);

        // Verify coverage percentage is calculated correctly
        var expectedCoverage = result.PublicApis.Count > 0
            ? Math.Round((decimal)documentedCount / result.PublicApis.Count * 100, 2)
            : 0m;
        result.DocumentationSummary.CoveragePercentage.ShouldBe(expectedCoverage);
    }
}
