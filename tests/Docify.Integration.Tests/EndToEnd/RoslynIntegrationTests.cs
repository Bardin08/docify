using Docify.Core.Analyzers;
using Docify.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace Docify.Integration.Tests.EndToEnd;

public class RoslynIntegrationTests
{
    [Fact]
    public async Task AnalyzeProject_SimpleLibrary_ReturnsValidAnalysisResult()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<RoslynAnalyzer>>();
        var mockSymbolExtractorLogger = new Mock<ILogger<SymbolExtractor>>();
        var symbolExtractor = new SymbolExtractor(mockSymbolExtractorLogger.Object);
        var analyzer = new RoslynAnalyzer(mockLogger.Object, symbolExtractor);
        var projectPath = Path.GetFullPath("../../../../samples/SimpleLibrary/SimpleLibrary.csproj");

        // Skip test if sample project doesn't exist (e.g., in CI without sample)
        if (!File.Exists(projectPath))
        {
            // Inform test framework to skip
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
        var symbolExtractor = new SymbolExtractor(mockSymbolExtractorLogger.Object);
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
        var symbolExtractor = new SymbolExtractor(mockSymbolExtractorLogger.Object);
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
}
