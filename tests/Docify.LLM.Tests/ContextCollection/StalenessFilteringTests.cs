using Docify.Core.Models;
using Docify.LLM.Abstractions;
using Docify.LLM.ContextCollection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Xunit;

namespace Docify.LLM.Tests.ContextCollection;

public class StalenessFilteringTests
{
    private readonly Mock<ILogger<SignatureContextCollector>> _mockLogger;
    private readonly Mock<ICallSiteCollector> _mockCallSiteCollector;
    private readonly Mock<IStalenessDetector> _mockStalenessDetector;

    public StalenessFilteringTests()
    {
        _mockLogger = new Mock<ILogger<SignatureContextCollector>>();
        _mockCallSiteCollector = new Mock<ICallSiteCollector>();
        _mockStalenessDetector = new Mock<IStalenessDetector>();

        _mockCallSiteCollector
            .Setup(x => x.CollectCallSites(It.IsAny<ApiSymbol>(), It.IsAny<Compilation>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CallSiteInfo>());
    }

    [Fact]
    public async Task CollectCalledMethodsDocumentation_WithFreshDocumentation_IncludesInContext()
    {
        // Arrange
        var compilation = CreateTestCompilation(@"
            public class TestClass {
                public void CallerMethod() {
                    HelperMethod();
                }

                /// <summary>This is fresh documentation.</summary>
                private void HelperMethod() { }
            }
        ");

        _mockStalenessDetector
            .Setup(x => x.DetectStaleDocumentation(It.IsAny<ApiSymbol>()))
            .Returns(new StalenessResult { IsStale = false });

        var symbol = GetApiSymbol(compilation, "CallerMethod");
        var collector = new SignatureContextCollector(_mockLogger.Object, _mockCallSiteCollector.Object, _mockStalenessDetector.Object);

        // Act
        var context = await collector.CollectContext(symbol, compilation);

        // Assert
        context.CalledMethodsDocumentation.ShouldNotBeEmpty();
        context.CalledMethodsDocumentation.Count.ShouldBe(1);
        context.CalledMethodsDocumentation[0].IsFresh.ShouldBeTrue();
        context.CalledMethodsDocumentation[0].XmlDocumentation.ShouldContain("This is fresh documentation");
    }

    [Fact]
    public async Task CollectCalledMethodsDocumentation_WithStaleDocumentation_ExcludesFromContext()
    {
        // Arrange
        var compilation = CreateTestCompilation(@"
            public class TestClass {
                public void CallerMethod() {
                    HelperMethod();
                }

                /// <summary>This is stale documentation.</summary>
                private void HelperMethod() { }
            }
        ");

        _mockStalenessDetector
            .Setup(x => x.DetectStaleDocumentation(It.IsAny<ApiSymbol>()))
            .Returns(new StalenessResult
            {
                IsStale = true,
                Severity = StalenessSeverity.Critical
            });

        var symbol = GetApiSymbol(compilation, "CallerMethod");
        var collector = new SignatureContextCollector(_mockLogger.Object, _mockCallSiteCollector.Object, _mockStalenessDetector.Object);

        // Act
        var context = await collector.CollectContext(symbol, compilation);

        // Assert
        context.CalledMethodsDocumentation.ShouldBeEmpty();
    }

    [Fact]
    public async Task CollectCalledMethodsDocumentation_WhenStalenessDetectorIsNull_TreatsAllAsFresh()
    {
        // Arrange
        var compilation = CreateTestCompilation(@"
            public class TestClass {
                public void CallerMethod() {
                    HelperMethod();
                }

                /// <summary>Documentation without staleness check.</summary>
                private void HelperMethod() { }
            }
        ");

        var symbol = GetApiSymbol(compilation, "CallerMethod");
        var collector = new SignatureContextCollector(_mockLogger.Object, _mockCallSiteCollector.Object, stalenessDetector: null);

        // Act
        var context = await collector.CollectContext(symbol, compilation);

        // Assert
        context.CalledMethodsDocumentation.ShouldNotBeEmpty();
        context.CalledMethodsDocumentation.Count.ShouldBe(1);
        context.CalledMethodsDocumentation[0].IsFresh.ShouldBeTrue();
        context.CalledMethodsDocumentation[0].XmlDocumentation.ShouldContain("Documentation without staleness check");

        // Verify debug log was called
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Git unavailable")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task CollectCalledMethodsDocumentation_WithMultipleMethods_FiltersMixedStaleness()
    {
        // Arrange
        var compilation = CreateTestCompilation(@"
            public class TestClass {
                public void CallerMethod() {
                    FreshHelper();
                    StaleHelper();
                    AnotherFreshHelper();
                }

                /// <summary>Fresh documentation 1.</summary>
                private void FreshHelper() { }

                /// <summary>Stale documentation.</summary>
                private void StaleHelper() { }

                /// <summary>Fresh documentation 2.</summary>
                private void AnotherFreshHelper() { }
            }
        ");

        _mockStalenessDetector
            .Setup(x => x.DetectStaleDocumentation(It.Is<ApiSymbol>(s => s.FullyQualifiedName.Contains("FreshHelper"))))
            .Returns(new StalenessResult { IsStale = false });

        _mockStalenessDetector
            .Setup(x => x.DetectStaleDocumentation(It.Is<ApiSymbol>(s => s.FullyQualifiedName.Contains("StaleHelper"))))
            .Returns(new StalenessResult { IsStale = true, Severity = StalenessSeverity.Warning });

        _mockStalenessDetector
            .Setup(x => x.DetectStaleDocumentation(It.Is<ApiSymbol>(s => s.FullyQualifiedName.Contains("AnotherFreshHelper"))))
            .Returns(new StalenessResult { IsStale = false });

        var symbol = GetApiSymbol(compilation, "CallerMethod");
        var collector = new SignatureContextCollector(_mockLogger.Object, _mockCallSiteCollector.Object, _mockStalenessDetector.Object);

        // Act
        var context = await collector.CollectContext(symbol, compilation);

        // Assert
        context.CalledMethodsDocumentation.Count.ShouldBe(2);
        context.CalledMethodsDocumentation.ShouldAllBe(doc => doc.IsFresh);
        context.CalledMethodsDocumentation.ShouldNotContain(doc => doc.XmlDocumentation.Contains("Stale documentation"));
    }

    [Fact]
    public async Task CollectCalledMethodsDocumentation_WithUndocumentedMethod_SkipsMethod()
    {
        // Arrange
        var compilation = CreateTestCompilation(@"
            public class TestClass {
                public void CallerMethod() {
                    UndocumentedHelper();
                }

                private void UndocumentedHelper() { }
            }
        ");

        var symbol = GetApiSymbol(compilation, "CallerMethod");
        var collector = new SignatureContextCollector(_mockLogger.Object, _mockCallSiteCollector.Object, _mockStalenessDetector.Object);

        // Act
        var context = await collector.CollectContext(symbol, compilation);

        // Assert
        context.CalledMethodsDocumentation.ShouldBeEmpty();
        _mockStalenessDetector.Verify(x => x.DetectStaleDocumentation(It.IsAny<ApiSymbol>()), Times.Never);
    }

    private Compilation CreateTestCompilation(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location)
        };

        return CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private ApiSymbol GetApiSymbol(Compilation compilation, string symbolName)
    {
        var semanticModel = compilation.GetSemanticModel(compilation.SyntaxTrees.First());
        var root = compilation.SyntaxTrees.First().GetRoot();

        var symbol = root.DescendantNodes()
            .Select(node => semanticModel.GetDeclaredSymbol(node))
            .FirstOrDefault(s => s?.Name == symbolName);

        symbol.ShouldNotBeNull($"Symbol {symbolName} not found");

        var location = symbol.Locations.First();
        return new ApiSymbol
        {
            Id = Guid.NewGuid().ToString(),
            SymbolType = SymbolType.Method,
            FullyQualifiedName = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat
                .WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted)),
            FilePath = location.SourceTree?.FilePath ?? "Test.cs",
            LineNumber = location.GetLineSpan().StartLinePosition.Line,
            Signature = symbol.ToDisplayString(),
            AccessModifier = symbol.DeclaredAccessibility.ToString(),
            IsStatic = symbol.IsStatic,
            HasDocumentation = false,
            DocumentationStatus = DocumentationStatus.Undocumented
        };
    }
}
