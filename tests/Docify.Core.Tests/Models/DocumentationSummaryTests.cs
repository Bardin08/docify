using Docify.Core.Models;
using Shouldly;

namespace Docify.Core.Tests.Models;

public class DocumentationSummaryTests
{
    [Fact]
    public void Calculate_MixedDocumentationStatuses_ReturnsCorrectCounts()
    {
        // Arrange
        var symbols = new List<ApiSymbol>
        {
            CreateSymbol(DocumentationStatus.Documented),
            CreateSymbol(DocumentationStatus.Documented),
            CreateSymbol(DocumentationStatus.Undocumented),
            CreateSymbol(DocumentationStatus.PartiallyDocumented),
            CreateSymbol(DocumentationStatus.Documented)
        };

        // Act
        var summary = DocumentationSummary.Calculate(symbols);

        // Assert
        summary.TotalApis.ShouldBe(5);
        summary.DocumentedCount.ShouldBe(3);
        summary.UndocumentedCount.ShouldBe(1);
        summary.PartiallyDocumentedCount.ShouldBe(1);
        summary.CoveragePercentage.ShouldBe(60.00m);
    }

    [Fact]
    public void Calculate_ZeroApis_ReturnsZeroCountsAndCoverage()
    {
        // Arrange
        var symbols = new List<ApiSymbol>();

        // Act
        var summary = DocumentationSummary.Calculate(symbols);

        // Assert
        summary.TotalApis.ShouldBe(0);
        summary.DocumentedCount.ShouldBe(0);
        summary.UndocumentedCount.ShouldBe(0);
        summary.PartiallyDocumentedCount.ShouldBe(0);
        summary.CoveragePercentage.ShouldBe(0m);
    }

    [Fact]
    public void Calculate_AllDocumented_Returns100PercentCoverage()
    {
        // Arrange
        var symbols = new List<ApiSymbol>
        {
            CreateSymbol(DocumentationStatus.Documented),
            CreateSymbol(DocumentationStatus.Documented),
            CreateSymbol(DocumentationStatus.Documented)
        };

        // Act
        var summary = DocumentationSummary.Calculate(symbols);

        // Assert
        summary.TotalApis.ShouldBe(3);
        summary.DocumentedCount.ShouldBe(3);
        summary.UndocumentedCount.ShouldBe(0);
        summary.PartiallyDocumentedCount.ShouldBe(0);
        summary.CoveragePercentage.ShouldBe(100.00m);
    }

    [Fact]
    public void Calculate_AllUndocumented_Returns0PercentCoverage()
    {
        // Arrange
        var symbols = new List<ApiSymbol>
        {
            CreateSymbol(DocumentationStatus.Undocumented),
            CreateSymbol(DocumentationStatus.Undocumented),
            CreateSymbol(DocumentationStatus.Undocumented)
        };

        // Act
        var summary = DocumentationSummary.Calculate(symbols);

        // Assert
        summary.TotalApis.ShouldBe(3);
        summary.DocumentedCount.ShouldBe(0);
        summary.UndocumentedCount.ShouldBe(3);
        summary.PartiallyDocumentedCount.ShouldBe(0);
        summary.CoveragePercentage.ShouldBe(0.00m);
    }

    [Fact]
    public void Calculate_PartiallyDocumentedOnly_DoesNotCountTowardsCoverage()
    {
        // Arrange
        var symbols = new List<ApiSymbol>
        {
            CreateSymbol(DocumentationStatus.PartiallyDocumented),
            CreateSymbol(DocumentationStatus.PartiallyDocumented)
        };

        // Act
        var summary = DocumentationSummary.Calculate(symbols);

        // Assert
        summary.TotalApis.ShouldBe(2);
        summary.DocumentedCount.ShouldBe(0);
        summary.UndocumentedCount.ShouldBe(0);
        summary.PartiallyDocumentedCount.ShouldBe(2);
        summary.CoveragePercentage.ShouldBe(0.00m);
    }

    [Fact]
    public void Calculate_CoveragePercentageRounding_RoundsToTwoDecimalPlaces()
    {
        // Arrange - 1 documented out of 3 = 33.333...%
        var symbols = new List<ApiSymbol>
        {
            CreateSymbol(DocumentationStatus.Documented),
            CreateSymbol(DocumentationStatus.Undocumented),
            CreateSymbol(DocumentationStatus.Undocumented)
        };

        // Act
        var summary = DocumentationSummary.Calculate(symbols);

        // Assert
        summary.CoveragePercentage.ShouldBe(33.33m);
    }

    [Fact]
    public void Calculate_NullSymbolsList_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => DocumentationSummary.Calculate(null!));
    }

    private static ApiSymbol CreateSymbol(DocumentationStatus status)
    {
        return new ApiSymbol
        {
            Id = Guid.NewGuid().ToString(),
            SymbolType = SymbolType.Class,
            FullyQualifiedName = "TestNamespace.TestClass",
            FilePath = "/test/path.cs",
            LineNumber = 1,
            Signature = "public class TestClass",
            AccessModifier = "Public",
            IsStatic = false,
            HasDocumentation = status != DocumentationStatus.Undocumented,
            DocumentationStatus = status
        };
    }
}
