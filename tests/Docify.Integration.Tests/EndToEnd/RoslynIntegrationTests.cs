using Docify.Core.Analyzers;
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
        var analyzer = new RoslynAnalyzer(mockLogger.Object);
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
        var analyzer = new RoslynAnalyzer(mockLogger.Object);
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
}
