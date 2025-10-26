using Docify.CLI.Formatters;
using Docify.Core.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace Docify.CLI.Tests.Formatters;

public class TextReportFormatterTests
{
    private readonly TextReportFormatter _formatter;

    public TextReportFormatterTests()
    {
        var mockLogger = new Mock<ILogger<TextReportFormatter>>();
        _formatter = new TextReportFormatter(mockLogger.Object);
    }

    [Fact]
    public void Format_WithUndocumentedApis_ContainsProjectNameAndStats()
    {
        // Arrange
        var result = CreateTestAnalysisResult();

        // Act
        var output = _formatter.Format(result);

        // Assert
        output.ShouldContain("TestProject");
        output.ShouldContain("Total APIs:");
        output.ShouldContain("Coverage:");
    }

    [Fact]
    public void Format_WithUndocumentedApis_GroupsByNamespace()
    {
        // Arrange
        var result = CreateTestAnalysisResult();

        // Act
        var output = _formatter.Format(result);

        // Assert
        output.ShouldContain("Namespace: TestNamespace");
    }

    [Fact]
    public void Format_WithUndocumentedApis_GroupsByClass()
    {
        // Arrange
        var result = CreateTestAnalysisResult();

        // Act
        var output = _formatter.Format(result);

        // Assert
        output.ShouldContain("Type: TestClass");
    }

    [Fact]
    public void Format_WithUndocumentedApis_IncludesFilePathAndLineNumber()
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
