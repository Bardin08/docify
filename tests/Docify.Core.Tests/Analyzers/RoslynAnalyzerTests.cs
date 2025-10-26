using Docify.Core.Analyzers;
using Docify.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace Docify.Core.Tests.Analyzers;

/// <summary>
/// Unit tests for RoslynAnalyzer.
/// Note: Most RoslynAnalyzer functionality is tested via integration tests in Docify.Integration.Tests
/// to avoid MSBuildLocator conflicts in unit test scenarios.
/// </summary>
public class RoslynAnalyzerTests
{
    // Most tests moved to Integration Tests due to MSBuildLocator initialization challenges
    // in xUnit parallel test execution.

    [Fact]
    public void RoslynAnalyzer_Constructor_ValidatesLogger()
    {
        // Arrange
        var mockSymbolExtractor = new Mock<ISymbolExtractor>();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new RoslynAnalyzer(null!, mockSymbolExtractor.Object));
    }

    [Fact]
    public void RoslynAnalyzer_Constructor_ValidatesSymbolExtractor()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<RoslynAnalyzer>>();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new RoslynAnalyzer(mockLogger.Object, null!));
    }
}
