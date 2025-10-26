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

public class ImplementationBodyExtractionTests
{
    private readonly Mock<ILogger<SignatureContextCollector>> _mockLogger;
    private readonly Mock<ICallSiteCollector> _mockCallSiteCollector;

    public ImplementationBodyExtractionTests()
    {
        _mockLogger = new Mock<ILogger<SignatureContextCollector>>();
        _mockCallSiteCollector = new Mock<ICallSiteCollector>();
        _mockCallSiteCollector
            .Setup(x => x.CollectCallSites(It.IsAny<ApiSymbol>(), It.IsAny<Compilation>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CallSiteInfo>());
    }

    [Fact]
    public async Task ExtractImplementationBody_WithSimpleMethod_ReturnsMethodBody()
    {
        // Arrange
        var compilation = CreateTestCompilation(@"
            public class TestClass {
                public void TestMethod() {
                    var x = 5;
                    Console.WriteLine(x);
                }
            }
        ");

        var symbol = GetApiSymbol(compilation, "TestMethod");
        var collector = new SignatureContextCollector(_mockLogger.Object, _mockCallSiteCollector.Object);

        // Act
        var context = await collector.CollectContext(symbol, compilation);

        // Assert
        context.ImplementationBody.ShouldNotBeNull();
        context.ImplementationBody.ShouldContain("var x = 5");
        context.ImplementationBody.ShouldContain("Console.WriteLine(x)");
        context.IsImplementationTruncated.ShouldBeFalse();
    }

    [Fact]
    public async Task ExtractImplementationBody_WithGenericMethod_ReturnsMethodBody()
    {
        // Arrange
        var compilation = CreateTestCompilation(@"
            public class TestClass {
                public T GenericMethod<T>(T input) where T : class {
                    return input;
                }
            }
        ");

        var symbol = GetApiSymbol(compilation, "GenericMethod");
        var collector = new SignatureContextCollector(_mockLogger.Object, _mockCallSiteCollector.Object);

        // Act
        var context = await collector.CollectContext(symbol, compilation);

        // Assert
        context.ImplementationBody.ShouldNotBeNull();
        context.ImplementationBody.ShouldContain("return input");
    }

    [Fact]
    public async Task ExtractImplementationBody_WithAsyncMethod_ReturnsMethodBody()
    {
        // Arrange
        var compilation = CreateTestCompilation(@"
            using System.Threading.Tasks;
            public class TestClass {
                public async Task<int> AsyncMethod() {
                    await Task.Delay(100);
                    return 42;
                }
            }
        ");

        var symbol = GetApiSymbol(compilation, "AsyncMethod");
        var collector = new SignatureContextCollector(_mockLogger.Object, _mockCallSiteCollector.Object);

        // Act
        var context = await collector.CollectContext(symbol, compilation);

        // Assert
        context.ImplementationBody.ShouldNotBeNull();
        context.ImplementationBody.ShouldContain("await Task.Delay(100)");
        context.ImplementationBody.ShouldContain("return 42");
    }

    [Fact]
    public async Task ExtractImplementationBody_WithPropertyGetter_ReturnsGetterBody()
    {
        // Arrange
        var compilation = CreateTestCompilation(@"
            public class TestClass {
                private int _value = 10;
                public int TestProperty {
                    get { return _value * 2; }
                    set { _value = value; }
                }
            }
        ");

        var symbol = GetApiSymbol(compilation, "TestProperty");
        var collector = new SignatureContextCollector(_mockLogger.Object, _mockCallSiteCollector.Object);

        // Act
        var context = await collector.CollectContext(symbol, compilation);

        // Assert
        context.ImplementationBody.ShouldNotBeNull();
        context.ImplementationBody.ShouldContain("get");
        context.ImplementationBody.ShouldContain("return _value * 2");
        context.ImplementationBody.ShouldContain("set");
    }

    [Fact]
    public async Task ExtractImplementationBody_WithExpressionBodiedProperty_ReturnsExpressionBody()
    {
        // Arrange
        var compilation = CreateTestCompilation(@"
            public class TestClass {
                private int _value = 10;
                public int TestProperty => _value * 2;
            }
        ");

        var symbol = GetApiSymbol(compilation, "TestProperty");
        var collector = new SignatureContextCollector(_mockLogger.Object, _mockCallSiteCollector.Object);

        // Act
        var context = await collector.CollectContext(symbol, compilation);

        // Assert
        context.ImplementationBody.ShouldNotBeNull();
        context.ImplementationBody.ShouldContain("_value * 2");
    }

    [Fact]
    public async Task ExtractImplementationBody_WithIndexer_ReturnsIndexerBody()
    {
        // Arrange
        var compilation = CreateTestCompilation(@"
            public class TestClass {
                private int[] _data = new int[10];
                public int this[int index] {
                    get { return _data[index]; }
                    set { _data[index] = value; }
                }
            }
        ");

        var symbol = GetApiSymbol(compilation, "this[]");
        var collector = new SignatureContextCollector(_mockLogger.Object, _mockCallSiteCollector.Object);

        // Act
        var context = await collector.CollectContext(symbol, compilation);

        // Assert
        context.ImplementationBody.ShouldNotBeNull();
        context.ImplementationBody.ShouldContain("_data[index]");
    }

    [Fact]
    public async Task ExtractImplementationBody_WithLargeBody_TruncatesAndSetsFlag()
    {
        // Arrange
        var largeBody = string.Join("\n", Enumerable.Range(1, 100).Select(i => $"        var x{i} = {i};"));
        var compilation = CreateTestCompilation($@"
            public class TestClass {{
                public void LargeMethod() {{
{largeBody}
                }}
            }}
        ");

        var symbol = GetApiSymbol(compilation, "LargeMethod");
        var collector = new SignatureContextCollector(_mockLogger.Object, _mockCallSiteCollector.Object);

        // Act
        var context = await collector.CollectContext(symbol, compilation);

        // Assert
        context.ImplementationBody.ShouldNotBeNull();
        context.IsImplementationTruncated.ShouldBeTrue();
        context.ImplementationBody.ShouldContain("[... implementation truncated for token budget ...]");
    }

    [Fact]
    public async Task ExtractImplementationBody_PreservesFormatting()
    {
        // Arrange
        var compilation = CreateTestCompilation(@"
            public class TestClass {
                public void TestMethod() {
                    if (true)
                    {
                        var x = 5;
                        Console.WriteLine(x);
                    }
                }
            }
        ");

        var symbol = GetApiSymbol(compilation, "TestMethod");
        var collector = new SignatureContextCollector(_mockLogger.Object, _mockCallSiteCollector.Object);

        // Act
        var context = await collector.CollectContext(symbol, compilation);

        // Assert
        context.ImplementationBody.ShouldNotBeNull();
        // Verify indentation is preserved (ToFullString includes trivia)
        context.ImplementationBody.ShouldContain("    {"); // Opening brace with indentation
    }

    private Compilation CreateTestCompilation(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Task).Assembly.Location)
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
            .Where(s => s != null && (s.Name == symbolName || s.Name == ""))
            .FirstOrDefault(s =>
            {
                if (symbolName == "this[]")
                    return s is IPropertySymbol { IsIndexer: true };
                return s!.Name == symbolName;
            });

        symbol.ShouldNotBeNull($"Symbol {symbolName} not found");

        var location = symbol.Locations.First();
        return new ApiSymbol
        {
            Id = Guid.NewGuid().ToString(),
            SymbolType = GetSymbolType(symbol),
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

    private SymbolType GetSymbolType(ISymbol symbol)
    {
        return symbol switch
        {
            IMethodSymbol => SymbolType.Method,
            IPropertySymbol { IsIndexer: true } => SymbolType.Indexer,
            IPropertySymbol => SymbolType.Property,
            INamedTypeSymbol { TypeKind: Microsoft.CodeAnalysis.TypeKind.Class } => SymbolType.Class,
            INamedTypeSymbol { TypeKind: Microsoft.CodeAnalysis.TypeKind.Interface } => SymbolType.Interface,
            _ => SymbolType.Class
        };
    }
}
