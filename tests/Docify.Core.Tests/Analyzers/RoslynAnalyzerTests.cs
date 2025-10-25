using Docify.Core.Analyzers;
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
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new RoslynAnalyzer(null!));
    }
}
