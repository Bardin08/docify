using System.Text.Json;
using Docify.CLI.Formatters;
using Docify.Core.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace Docify.CLI.Tests.Formatters;

public class JsonReportFormatterTests
{
    private readonly JsonReportFormatter _formatter;

    public JsonReportFormatterTests()
    {
        var mockLogger = new Mock<ILogger<JsonReportFormatter>>();
        _formatter = new JsonReportFormatter(mockLogger.Object);
    }

    [Fact]
    public void Format_WithValidResult_ReturnsValidJson()
    {
        // Arrange
        var result = CreateTestAnalysisResult();

        // Act
        var output = _formatter.Format(result);

        // Assert
        var parsed = () => JsonDocument.Parse(output);
        parsed.ShouldNotThrow();
    }

    [Fact]
    public void Format_WithValidResult_ContainsAllRequiredFields()
    {
        // Arrange
        var result = CreateTestAnalysisResult();

        // Act
        var output = _formatter.Format(result);
        var doc = JsonDocument.Parse(output);
        var root = doc.RootElement;

        // Assert
        root.TryGetProperty("projectName", out var projectName).ShouldBeTrue();
        projectName.GetString().ShouldBe("TestProject");

        root.TryGetProperty("totalApis", out var totalApis).ShouldBeTrue();
        totalApis.GetInt32().ShouldBe(1);

        root.TryGetProperty("undocumentedCount", out var undocumentedCount).ShouldBeTrue();
        undocumentedCount.GetInt32().ShouldBe(1);

        root.TryGetProperty("coveragePercentage", out var coveragePercentage).ShouldBeTrue();
        coveragePercentage.GetDecimal().ShouldBe(0);
    }

    [Fact]
    public void Format_WithUndocumentedApis_ContainsApiArray()
    {
        // Arrange
        var result = CreateTestAnalysisResult();

        // Act
        var output = _formatter.Format(result);
        var doc = JsonDocument.Parse(output);
        var root = doc.RootElement;

        // Assert
        root.TryGetProperty("undocumentedApis", out var apis).ShouldBeTrue();
        apis.GetArrayLength().ShouldBe(1);

        var firstApi = apis[0];
        firstApi.TryGetProperty("fullyQualifiedName", out var fqn).ShouldBeTrue();
        fqn.GetString().ShouldBe("TestNamespace.TestClass.MyMethod");

        firstApi.TryGetProperty("signature", out var signature).ShouldBeTrue();
        signature.GetString().ShouldBe("public void MyMethod()");

        firstApi.TryGetProperty("filePath", out var filePath).ShouldBeTrue();
        filePath.GetString().ShouldBe("/test/TestClass.cs");

        firstApi.TryGetProperty("lineNumber", out var lineNumber).ShouldBeTrue();
        lineNumber.GetInt32().ShouldBe(10);

        firstApi.TryGetProperty("namespace", out var ns).ShouldBeTrue();
        ns.GetString().ShouldBe("TestNamespace");
    }

    [Fact]
    public void Format_ProducesProperlyFormattedJson()
    {
        // Arrange
        var result = CreateTestAnalysisResult();

        // Act
        var output = _formatter.Format(result);

        // Assert
        output.ShouldContain("\n"); // Should be indented
        output.ShouldContain("\"projectName\""); // Should use camelCase
        output.ShouldContain("\"undocumentedCount\""); // Should use camelCase
    }

    [Fact]
    public void Format_CanRoundTrip()
    {
        // Arrange
        var result = CreateTestAnalysisResult();

        // Act
        var output = _formatter.Format(result);
        var deserialized = JsonSerializer.Deserialize<ReportDto>(output, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // Assert
        deserialized.ShouldNotBeNull();
        deserialized.ProjectName.ShouldBe("TestProject");
        deserialized.TotalApis.ShouldBe(1);
        deserialized.UndocumentedCount.ShouldBe(1);
        deserialized.UndocumentedApis.Count.ShouldBe(1);
    }

    [Fact]
    public void Format_WithNullResult_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => _formatter.Format(null!));
    }

    private static AnalysisResult CreateTestAnalysisResult()
    {
        var apis = new List<ApiSymbol>
        {
            new()
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
            }
        };

        var summary = new DocumentationSummary
        {
            TotalApis = 1,
            DocumentedCount = 0,
            UndocumentedCount = 1,
            PartiallyDocumentedCount = 0,
            CoveragePercentage = 0
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
