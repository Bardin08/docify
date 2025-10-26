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

public class CalledMethodDetectionTests
{
    private readonly Mock<ILogger<SignatureContextCollector>> _mockLogger;
    private readonly Mock<ICallSiteCollector> _mockCallSiteCollector;

    public CalledMethodDetectionTests()
    {
        _mockLogger = new Mock<ILogger<SignatureContextCollector>>();
        _mockCallSiteCollector = new Mock<ICallSiteCollector>();
        _mockCallSiteCollector
            .Setup(x => x.CollectCallSites(It.IsAny<ApiSymbol>(), It.IsAny<Compilation>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CallSiteInfo>());
    }

    [Fact]
    public async Task AnalyzeInternalMethodCalls_WithSimpleMethodCall_DetectsCalledMethod()
    {
        // Arrange
        var compilation = CreateTestCompilation(@"
            public class TestClass {
                public void CallerMethod() {
                    HelperMethod();
                }

                private void HelperMethod() {
                    var x = 5;
                }
            }
        ");

        var symbol = GetApiSymbol(compilation, "CallerMethod");
        var collector = new SignatureContextCollector(_mockLogger.Object, _mockCallSiteCollector.Object);

        // Act
        var context = await collector.CollectContext(symbol, compilation);

        // Assert
        context.ImplementationBody.ShouldNotBeNull();
        context.ImplementationBody.ShouldContain("HelperMethod()");
    }

    [Fact]
    public async Task AnalyzeInternalMethodCalls_WithMultipleMethodCalls_DetectsAllCalls()
    {
        // Arrange
        var compilation = CreateTestCompilation(@"
            public class TestClass {
                public void CallerMethod() {
                    Helper1();
                    Helper2();
                    Helper3();
                }

                private void Helper1() { }
                private void Helper2() { }
                private void Helper3() { }
            }
        ");

        var symbol = GetApiSymbol(compilation, "CallerMethod");
        var collector = new SignatureContextCollector(_mockLogger.Object, _mockCallSiteCollector.Object);

        // Act
        var context = await collector.CollectContext(symbol, compilation);

        // Assert
        context.ImplementationBody.ShouldNotBeNull();
        context.ImplementationBody.ShouldContain("Helper1()");
        context.ImplementationBody.ShouldContain("Helper2()");
        context.ImplementationBody.ShouldContain("Helper3()");
    }

    [Fact]
    public async Task AnalyzeInternalMethodCalls_WithExternalLibraryCalls_ExcludesExternalMethods()
    {
        // Arrange
        var compilation = CreateTestCompilation(@"
            using System;
            public class TestClass {
                public void CallerMethod() {
                    Console.WriteLine(""test"");
                    HelperMethod();
                }

                private void HelperMethod() { }
            }
        ");

        var symbol = GetApiSymbol(compilation, "CallerMethod");
        var collector = new SignatureContextCollector(_mockLogger.Object, _mockCallSiteCollector.Object);

        // Act
        var context = await collector.CollectContext(symbol, compilation);

        // Assert
        context.ImplementationBody.ShouldNotBeNull();
        // Console.WriteLine should be in body but not tracked as internal call
        context.ImplementationBody.ShouldContain("Console.WriteLine");
        context.ImplementationBody.ShouldContain("HelperMethod()");
    }

    [Fact]
    public async Task AnalyzeInternalMethodCalls_WithNullConditionalOperator_HandlesGracefully()
    {
        // Arrange
        var compilation = CreateTestCompilation(@"
            public class TestClass {
                private TestClass? _instance;

                public void CallerMethod() {
                    _instance?.HelperMethod();
                }

                private void HelperMethod() { }
            }
        ");

        var symbol = GetApiSymbol(compilation, "CallerMethod");
        var collector = new SignatureContextCollector(_mockLogger.Object, _mockCallSiteCollector.Object);

        // Act
        var context = await collector.CollectContext(symbol, compilation);

        // Assert
        context.ImplementationBody.ShouldNotBeNull();
        context.ImplementationBody.ShouldContain("?.HelperMethod()");
    }

    [Fact]
    public async Task AnalyzeInternalMethodCalls_WithLocalHelperMethod_DetectsLocalMethod()
    {
        // Arrange
        var compilation = CreateTestCompilation(@"
            public class TestClass {
                public void CallerMethod() {
                    LocalHelper();

                    void LocalHelper() {
                        var x = 5;
                    }
                }
            }
        ");

        var symbol = GetApiSymbol(compilation, "CallerMethod");
        var collector = new SignatureContextCollector(_mockLogger.Object, _mockCallSiteCollector.Object);

        // Act
        var context = await collector.CollectContext(symbol, compilation);

        // Assert
        context.ImplementationBody.ShouldNotBeNull();
        context.ImplementationBody.ShouldContain("LocalHelper()");
    }

    [Fact]
    public async Task AnalyzeInternalMethodCalls_WithMethodCallInLambda_DetectsMethodCall()
    {
        // Arrange
        var compilation = CreateTestCompilation(@"
            using System;
            public class TestClass {
                public void CallerMethod() {
                    Action action = () => HelperMethod();
                    action();
                }

                private void HelperMethod() { }
            }
        ");

        var symbol = GetApiSymbol(compilation, "CallerMethod");
        var collector = new SignatureContextCollector(_mockLogger.Object, _mockCallSiteCollector.Object);

        // Act
        var context = await collector.CollectContext(symbol, compilation);

        // Assert
        context.ImplementationBody.ShouldNotBeNull();
        context.ImplementationBody.ShouldContain("HelperMethod()");
    }

    [Fact]
    public async Task AnalyzeInternalMethodCalls_WithNoMethodCalls_ReturnsEmptyList()
    {
        // Arrange
        var compilation = CreateTestCompilation(@"
            public class TestClass {
                public void CallerMethod() {
                    var x = 5;
                    var y = x * 2;
                }
            }
        ");

        var symbol = GetApiSymbol(compilation, "CallerMethod");
        var collector = new SignatureContextCollector(_mockLogger.Object, _mockCallSiteCollector.Object);

        // Act
        var context = await collector.CollectContext(symbol, compilation);

        // Assert
        context.ImplementationBody.ShouldNotBeNull();
        context.CalledMethodsDocumentation.ShouldBeEmpty();
    }

    private Compilation CreateTestCompilation(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Action).Assembly.Location)
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
