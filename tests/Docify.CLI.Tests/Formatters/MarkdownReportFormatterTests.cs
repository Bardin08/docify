using Docify.CLI.Formatters;
using Docify.Core.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace Docify.CLI.Tests.Formatters;

public class MarkdownReportFormatterTests
{
    private readonly MarkdownReportFormatter _formatter;

    public MarkdownReportFormatterTests()
    {
        var mockLogger = new Mock<ILogger<MarkdownReportFormatter>>();
        _formatter = new MarkdownReportFormatter(mockLogger.Object);
    }

    [Fact]
    public void Format_WithUndocumentedApis_ContainsMarkdownTableSyntax()
    {
        // Arrange
        var result = CreateTestAnalysisResult();

        // Act
        var output = _formatter.Format(result);

        // Assert
        output.ShouldContain("|");
        output.ShouldContain("---");
    }

    [Fact]
    public void Format_WithUndocumentedApis_IncludesSummarySection()
    {
        // Arrange
        var result = CreateTestAnalysisResult();

        // Act
        var output = _formatter.Format(result);

        // Assert
        output.ShouldContain("# Documentation Coverage Report:");
        output.ShouldContain("## Summary");
        output.ShouldContain("**Total APIs:**");
        output.ShouldContain("**Coverage:**");
    }

    [Fact]
    public void Format_WithUndocumentedApis_HasCorrectTableColumns()
    {
        // Arrange
        var result = CreateTestAnalysisResult();

        // Act
        var output = _formatter.Format(result);

        // Assert
        output.ShouldContain("| Namespace | Type | Member | Signature | Location |");
    }

    [Fact]
    public void Format_WithUndocumentedApis_LocationUsesCorrectFormat()
    {
        // Arrange
        var result = CreateTestAnalysisResult();

        // Act
        var output = _formatter.Format(result);

        // Assert
        output.ShouldContain("/test/TestClass.cs:10");
    }

    [Fact]
    public void Format_WithNoUndocumentedApis_ShowsSuccessMessage()
    {
        // Arrange
        var result = CreateTestAnalysisResult(withUndocumentedApis: false);

        // Act
        var output = _formatter.Format(result);

        // Assert
        output.ShouldContain("No undocumented APIs found");
    }

    [Fact]
    public void Format_VerifiesTableAlignmentCharacters()
    {
        // Arrange
        var result = CreateTestAnalysisResult();

        // Act
        var output = _formatter.Format(result);

        // Assert
        var lines = output.Split('\n');
        var tableHeaderLine = lines.First(l => l.Contains("| Namespace |"));
        var alignmentLine = lines.First(l => l.Contains("|---"));

        tableHeaderLine.ShouldNotBeNullOrEmpty();
        alignmentLine.ShouldNotBeNullOrEmpty();
        alignmentLine.Count(c => c == '-').ShouldBeGreaterThan(10);
    }

    [Fact]
    public void Format_WithNullResult_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => _formatter.Format(null!));
    }

    private static AnalysisResult CreateTestAnalysisResult(bool withUndocumentedApis = true)
    {
        var apis = new List<ApiSymbol>();

        if (withUndocumentedApis)
        {
            apis.Add(new ApiSymbol
            {
                Id = "1",
                SymbolType = SymbolType.Method,
                FullyQualifiedName = "TestNamespace.TestClass.MyMethod",
                FilePath = "/test/TestClass.cs",
                LineNumber = 10,
                Signature = "public void MyMethod()",
                AccessModifier = "Public",
                IsStatic = false,
                HasDocumentation = false,
                DocumentationStatus = DocumentationStatus.Undocumented
            });
        }

        var summary = new DocumentationSummary
        {
            TotalApis = 1,
            DocumentedCount = withUndocumentedApis ? 0 : 1,
            UndocumentedCount = withUndocumentedApis ? 1 : 0,
            PartiallyDocumentedCount = 0,
            CoveragePercentage = withUndocumentedApis ? 0 : 100
        };

        return new AnalysisResult
        {
            ProjectPath = "/test/TestProject.csproj",
            TotalDocuments = 1,
            Compilation = CSharpCompilation.Create("Test"),
            SyntaxTrees = Array.Empty<SyntaxTree>(),
            HasErrors = false,
            DiagnosticMessages = Array.Empty<string>(),
            PublicApis = apis,
            DocumentationSummary = summary
        };
    }
}
