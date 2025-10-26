using System.Text.Json;
using Docify.CLI.Formatters;
using Docify.Core.Analyzers;
using Docify.Core.Models;
using Docify.LLM.ContextCollection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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
            api.SymbolType == SymbolType.Class &&
            api.FullyQualifiedName.Contains("Calculator"));
        calculatorClass.ShouldNotBeNull();

        // Verify we have Add method
        var addMethod = result.PublicApis.FirstOrDefault(api =>
            api.SymbolType == SymbolType.Method &&
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

    [Fact]
    public async Task FormatReport_TextFormat_ProducesValidOutput()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<RoslynAnalyzer>>();
        var mockSymbolExtractorLogger = new Mock<ILogger<SymbolExtractor>>();
        var mockDetectorLogger = new Mock<ILogger<DocumentationDetector>>();
        var mockFormatterLogger = new Mock<ILogger<TextReportFormatter>>();
        var documentationDetector = new DocumentationDetector(mockDetectorLogger.Object);
        var symbolExtractor = new SymbolExtractor(mockSymbolExtractorLogger.Object, documentationDetector);
        var analyzer = new RoslynAnalyzer(mockLogger.Object, symbolExtractor);
        var formatter = new TextReportFormatter(mockFormatterLogger.Object);
        var projectPath = Path.GetFullPath("../../../../samples/SimpleLibrary/SimpleLibrary.csproj");

        // Skip test if sample project doesn't exist
        if (!File.Exists(projectPath))
        {
            return;
        }

        // Act
        var result = await analyzer.AnalyzeProject(projectPath);
        var report = formatter.Format(result);

        // Assert
        report.ShouldNotBeNullOrWhiteSpace();
        report.ShouldContain("Documentation Coverage Report:");
        report.ShouldContain("Summary:");
        report.ShouldContain("Total APIs:");
        report.ShouldContain("Coverage:");
    }

    [Fact]
    public async Task FormatReport_JsonFormat_ProducesValidJson()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<RoslynAnalyzer>>();
        var mockSymbolExtractorLogger = new Mock<ILogger<SymbolExtractor>>();
        var mockDetectorLogger = new Mock<ILogger<DocumentationDetector>>();
        var mockFormatterLogger = new Mock<ILogger<JsonReportFormatter>>();
        var documentationDetector = new DocumentationDetector(mockDetectorLogger.Object);
        var symbolExtractor = new SymbolExtractor(mockSymbolExtractorLogger.Object, documentationDetector);
        var analyzer = new RoslynAnalyzer(mockLogger.Object, symbolExtractor);
        var formatter = new JsonReportFormatter(mockFormatterLogger.Object);
        var projectPath = Path.GetFullPath("../../../../samples/SimpleLibrary/SimpleLibrary.csproj");

        // Skip test if sample project doesn't exist
        if (!File.Exists(projectPath))
        {
            return;
        }

        // Act
        var result = await analyzer.AnalyzeProject(projectPath);
        var report = formatter.Format(result);

        // Assert
        report.ShouldNotBeNullOrWhiteSpace();
        var parsed = () => JsonDocument.Parse(report);
        parsed.ShouldNotThrow();

        var doc = JsonDocument.Parse(report);
        var root = doc.RootElement;
        root.TryGetProperty("projectName", out _).ShouldBeTrue();
        root.TryGetProperty("totalApis", out _).ShouldBeTrue();
        root.TryGetProperty("undocumentedCount", out _).ShouldBeTrue();
        root.TryGetProperty("coveragePercentage", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task FormatReport_MarkdownFormat_ProducesValidMarkdown()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<RoslynAnalyzer>>();
        var mockSymbolExtractorLogger = new Mock<ILogger<SymbolExtractor>>();
        var mockDetectorLogger = new Mock<ILogger<DocumentationDetector>>();
        var mockFormatterLogger = new Mock<ILogger<MarkdownReportFormatter>>();
        var documentationDetector = new DocumentationDetector(mockDetectorLogger.Object);
        var symbolExtractor = new SymbolExtractor(mockSymbolExtractorLogger.Object, documentationDetector);
        var analyzer = new RoslynAnalyzer(mockLogger.Object, symbolExtractor);
        var formatter = new MarkdownReportFormatter(mockFormatterLogger.Object);
        var projectPath = Path.GetFullPath("../../../../samples/SimpleLibrary/SimpleLibrary.csproj");

        // Skip test if sample project doesn't exist
        if (!File.Exists(projectPath))
        {
            return;
        }

        // Act
        var result = await analyzer.AnalyzeProject(projectPath);
        var report = formatter.Format(result);

        // Assert
        report.ShouldNotBeNullOrWhiteSpace();
        report.ShouldContain("# Documentation Coverage Report:");
        report.ShouldContain("## Summary");
        report.ShouldContain("|"); // Table syntax
    }

    [Fact]
    public async Task CollectContext_WithRealProject_ExtractsMethodSignature()
    {
        // Arrange
        var mockAnalyzerLogger = new Mock<ILogger<RoslynAnalyzer>>();
        var mockSymbolExtractorLogger = new Mock<ILogger<SymbolExtractor>>();
        var mockDetectorLogger = new Mock<ILogger<DocumentationDetector>>();
        var mockCollectorLogger = new Mock<ILogger<SignatureContextCollector>>();
        var documentationDetector = new DocumentationDetector(mockDetectorLogger.Object);
        var symbolExtractor = new SymbolExtractor(mockSymbolExtractorLogger.Object, documentationDetector);
        var analyzer = new RoslynAnalyzer(mockAnalyzerLogger.Object, symbolExtractor);
        var collector = new SignatureContextCollector(mockCollectorLogger.Object);
        var projectPath = Path.GetFullPath("../../../../samples/SimpleLibrary/SimpleLibrary.csproj");

        // Skip test if sample project doesn't exist
        if (!File.Exists(projectPath))
        {
            return;
        }

        // Act
        var result = await analyzer.AnalyzeProject(projectPath);
        var addMethod = result.PublicApis.FirstOrDefault(api =>
            api.SymbolType == SymbolType.Method &&
            api.Signature.Contains("Add"));

        addMethod.ShouldNotBeNull();

        var context = await collector.CollectContext(addMethod, result.Compilation);

        // Assert
        context.ShouldNotBeNull();
        context.ApiSymbolId.ShouldBe(addMethod.Id);
        context.ParameterTypes.ShouldNotBeEmpty();
        context.ReturnType.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CollectContext_WithGenericMethod_CapturesTypeParameters()
    {
        // Arrange - create in-memory compilation with generic method
        var mockCollectorLogger = new Mock<ILogger<SignatureContextCollector>>();
        var collector = new SignatureContextCollector(mockCollectorLogger.Object);

        var code = @"
            namespace TestNamespace
            {
                public class GenericClass
                {
                    public T GenericMethod<T>(T value) where T : class => value;
                }
            }";

        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            [syntaxTree],
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
            ]);

        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var root = await syntaxTree.GetRootAsync();
        var methodSymbol = root.DescendantNodes()
            .Select(node => semanticModel.GetDeclaredSymbol(node))
            .OfType<IMethodSymbol>()
            .FirstOrDefault(m => m.Name == "GenericMethod");

        methodSymbol.ShouldNotBeNull();

        var fullyQualifiedName = methodSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat
            .WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted));

        var apiSymbol = new ApiSymbol
        {
            Id = Guid.NewGuid().ToString(),
            SymbolType = SymbolType.Method,
            FullyQualifiedName = fullyQualifiedName,
            FilePath = "test.cs",
            LineNumber = 5,
            Signature = methodSymbol.ToDisplayString(),
            AccessModifier = "public",
            IsStatic = false,
            HasDocumentation = false,
            DocumentationStatus = DocumentationStatus.Undocumented
        };

        // Act
        var context = await collector.CollectContext(apiSymbol, compilation);

        // Assert
        context.ParameterTypes.ShouldContain(p => p.Contains("Type parameter") && p.Contains("class"));
    }

    [Fact]
    public async Task CollectContext_WithInheritance_CapturesBaseTypeAndInterfaces()
    {
        // Arrange - create in-memory compilation with inheritance
        var mockCollectorLogger = new Mock<ILogger<SignatureContextCollector>>();
        var collector = new SignatureContextCollector(mockCollectorLogger.Object);

        var code = @"
            namespace TestNamespace
            {
                public interface ITestInterface { }
                public class BaseClass { }
                public class DerivedClass : BaseClass, ITestInterface
                {
                    public void TestMethod() { }
                }
            }";

        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            [syntaxTree],
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
            ]);

        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var root = await syntaxTree.GetRootAsync();
        var methodSymbol = root.DescendantNodes()
            .Select(node => semanticModel.GetDeclaredSymbol(node))
            .OfType<IMethodSymbol>()
            .FirstOrDefault(m => m.Name == "TestMethod");

        methodSymbol.ShouldNotBeNull();

        var fullyQualifiedName = methodSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat
            .WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted));

        var apiSymbol = new ApiSymbol
        {
            Id = Guid.NewGuid().ToString(),
            SymbolType = SymbolType.Method,
            FullyQualifiedName = fullyQualifiedName,
            FilePath = "test.cs",
            LineNumber = 6,
            Signature = methodSymbol.ToDisplayString(),
            AccessModifier = "public",
            IsStatic = false,
            HasDocumentation = false,
            DocumentationStatus = DocumentationStatus.Undocumented
        };

        // Act
        var context = await collector.CollectContext(apiSymbol, compilation);

        // Assert
        context.InheritanceHierarchy.ShouldContain(h => h.Contains("BaseClass"));
        context.InheritanceHierarchy.ShouldContain(h => h.Contains("ITestInterface"));
    }
}
