using Docify.Core.Models;
using Docify.LLM.Abstractions;
using Docify.LLM.ContextCollection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace Docify.LLM.Tests.ContextCollection;

public class SignatureContextCollectorTests
{
    private readonly SignatureContextCollector _collector;
    private readonly Mock<ILogger<SignatureContextCollector>> _mockLogger;
    private readonly Mock<ICallSiteCollector> _mockCallSiteCollector;

    public SignatureContextCollectorTests()
    {
        _mockLogger = new Mock<ILogger<SignatureContextCollector>>();
        _mockCallSiteCollector = new Mock<ICallSiteCollector>();

        // Mock call site collector to return empty list by default
        _mockCallSiteCollector.Setup(c => c.CollectCallSites(
            It.IsAny<ApiSymbol>(),
            It.IsAny<Compilation>(),
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CallSiteInfo>());

        _collector = new SignatureContextCollector(_mockLogger.Object, _mockCallSiteCollector.Object);
    }

    [Fact]
    public async Task CollectContext_WithSimpleMethod_ExtractsMethodNameAndReturnType()
    {
        // Arrange
        var code = @"
            namespace TestNamespace
            {
                public class TestClass
                {
                    public void SimpleMethod() { }
                }
            }";

        var (compilation, symbol) = await CreateCompilationAndSymbol(code, "TestNamespace.TestClass", "SimpleMethod");

        // Act
        var context = await _collector.CollectContext(symbol, compilation);

        // Assert
        context.ShouldNotBeNull();
        context.ApiSymbolId.ShouldBe(symbol.Id);
        context.ParameterTypes.ShouldBeEmpty();
        context.ReturnType.ShouldBeNull(); // void
    }

    [Fact]
    public async Task CollectContext_WithMethodParameters_ExtractsAllParameterNamesAndTypes()
    {
        // Arrange
        var code = @"
            namespace TestNamespace
            {
                public class TestClass
                {
                    public bool MyMethod(string name, int count) => true;
                }
            }";

        var (compilation, symbol) = await CreateCompilationAndSymbol(code, "TestNamespace.TestClass", "MyMethod");

        // Act
        var context = await _collector.CollectContext(symbol, compilation);

        // Assert
        context.ParameterTypes.Count.ShouldBe(2);
        context.ParameterTypes[0].ShouldBe("string name");
        context.ParameterTypes[1].ShouldBe("int count");
        context.ReturnType.ShouldBe("bool");
    }

    [Fact]
    public async Task CollectContext_WithGenericMethodAndConstraints_IncludesTypeParametersAndConstraints()
    {
        // Arrange
        var code = @"
            namespace TestNamespace
            {
                public class TestClass
                {
                    public T GenericMethod<T>(T value) where T : class => value;
                }
            }";

        var (compilation, symbol) = await CreateCompilationAndSymbol(code, "TestNamespace.TestClass", "GenericMethod");

        // Act
        var context = await _collector.CollectContext(symbol, compilation);

        // Assert
        context.ParameterTypes.Count.ShouldBeGreaterThan(1);
        context.ParameterTypes.ShouldContain(p => p.Contains("Type parameter: T") && p.Contains("where T : class"));
    }

    [Fact]
    public async Task CollectContext_WithAsyncMethod_CorrectlyIdentifiesReturnType()
    {
        // Arrange
        var code = @"
            using System.Threading.Tasks;
            namespace TestNamespace
            {
                public class TestClass
                {
                    public async Task<int> AsyncMethod()
                    {
                        await Task.Delay(1);
                        return 42;
                    }
                }
            }";

        var (compilation, symbol) = await CreateCompilationAndSymbol(code, "TestNamespace.TestClass", "AsyncMethod");

        // Act
        var context = await _collector.CollectContext(symbol, compilation);

        // Assert
        context.ReturnType.ShouldContain("Task<int>");
    }

    [Fact]
    public async Task CollectContext_WithProperty_IdentifiesPropertyType()
    {
        // Arrange
        var code = @"
            namespace TestNamespace
            {
                public class TestClass
                {
                    public string MyProperty { get; set; }
                }
            }";

        var (compilation, symbol) = await CreateCompilationAndSymbol(code, "TestNamespace.TestClass", "MyProperty");

        // Act
        var context = await _collector.CollectContext(symbol, compilation);

        // Assert
        context.ReturnType.ShouldBe("string");
        context.ParameterTypes.ShouldBeEmpty();
    }

    [Fact]
    public async Task CollectContext_WithIndexer_IncludesIndexerParametersAndReturnType()
    {
        // Arrange
        var code = @"
            namespace TestNamespace
            {
                public class TestClass
                {
                    public int this[string key] => 0;
                }
            }";

        var (compilation, symbol) = await CreateCompilationAndSymbol(code, "TestNamespace.TestClass", "this[]");

        // Act
        var context = await _collector.CollectContext(symbol, compilation);

        // Assert
        context.ParameterTypes.Count.ShouldBe(1);
        context.ParameterTypes[0].ShouldBe("string key");
        context.ReturnType.ShouldBe("int");
    }

    [Fact]
    public async Task CollectContext_WithNestedGenericTypes_CorrectlyFormatsComplexTypes()
    {
        // Arrange
        var code = @"
            using System.Collections.Generic;
            namespace TestNamespace
            {
                public class TestClass
                {
                    public Dictionary<string, List<int>> ComplexMethod() => null;
                }
            }";

        var (compilation, symbol) = await CreateCompilationAndSymbol(code, "TestNamespace.TestClass", "ComplexMethod");

        // Act
        var context = await _collector.CollectContext(symbol, compilation);

        // Assert
        context.ReturnType.ShouldContain("Dictionary");
        context.ReturnType.ShouldContain("string");
        context.ReturnType.ShouldContain("List<int>");
    }

    [Fact]
    public async Task CollectContext_WithNullableReferenceTypes_PreservesNullabilityAnnotations()
    {
        // Arrange
        var code = @"
            #nullable enable
            namespace TestNamespace
            {
                public class TestClass
                {
                    public string? NullableMethod(string? input) => input;
                }
            }";

        var (compilation, symbol) = await CreateCompilationAndSymbol(code, "TestNamespace.TestClass", "NullableMethod");

        // Act
        var context = await _collector.CollectContext(symbol, compilation);

        // Assert
        context.ParameterTypes[0].ShouldContain("?");
        context.ReturnType.ShouldContain("?");
    }

    [Fact]
    public async Task CollectContext_WithTupleReturnType_FormatsTupleCorrectly()
    {
        // Arrange
        var code = @"
            namespace TestNamespace
            {
                public class TestClass
                {
                    public (int count, string name) TupleMethod() => (0, """");
                }
            }";

        var (compilation, symbol) = await CreateCompilationAndSymbol(code, "TestNamespace.TestClass", "TupleMethod");

        // Act
        var context = await _collector.CollectContext(symbol, compilation);

        // Assert
        context.ReturnType.ShouldContain("int");
        context.ReturnType.ShouldContain("string");
    }

    [Fact]
    public async Task CollectContext_WithNullSymbol_ThrowsArgumentNullException()
    {
        // Arrange
        var compilation = CSharpCompilation.Create("Test");

        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await _collector.CollectContext(null!, compilation));
    }

    [Fact]
    public async Task CollectContext_WithNullCompilation_ThrowsArgumentNullException()
    {
        // Arrange
        var symbol = new ApiSymbol
        {
            Id = "test-id",
            SymbolType = SymbolType.Method,
            FullyQualifiedName = "Test.Method",
            FilePath = "test.cs",
            LineNumber = 1,
            Signature = "void Method()",
            AccessModifier = "public",
            IsStatic = false,
            HasDocumentation = false,
            DocumentationStatus = DocumentationStatus.Undocumented
        };

        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await _collector.CollectContext(symbol, null!));
    }

    [Fact]
    public async Task CollectContext_WithInheritance_ExtractsBaseTypeAndInterfaces()
    {
        // Arrange
        var code = @"
            namespace TestNamespace
            {
                public interface IMyInterface { }
                public class BaseClass { }
                public class TestClass : BaseClass, IMyInterface
                {
                    public void TestMethod() { }
                }
            }";

        var (compilation, symbol) = await CreateCompilationAndSymbol(code, "TestNamespace.TestClass", "TestMethod");

        // Act
        var context = await _collector.CollectContext(symbol, compilation);

        // Assert
        context.InheritanceHierarchy.ShouldContain(h => h.Contains("BaseClass"));
        context.InheritanceHierarchy.ShouldContain(h => h.Contains("IMyInterface"));
    }

    [Fact]
    public void SerializeToJson_WithValidContext_ProducesValidJson()
    {
        // Arrange
        var context = new ApiContext
        {
            ApiSymbolId = "test-id",
            ParameterTypes = new List<string> { "string name", "int count" },
            ReturnType = "bool",
            InheritanceHierarchy = new List<string> { "BaseClass" },
            RelatedTypes = new List<string> { "string", "int", "bool" },
            XmlDocComments = null,
            TokenEstimate = 10,
            CallSites = []
        };

        // Act
        var json = _collector.SerializeToJson(context);

        // Assert
        json.ShouldNotBeNullOrWhiteSpace();
        json.ShouldContain("apiSymbolId");
        json.ShouldContain("test-id");
        json.ShouldContain("parameterTypes");
        System.Text.Json.JsonDocument.Parse(json); // Should not throw
    }

    [Fact]
    public void FormatAsPlainText_WithValidContext_ProducesReadableFormat()
    {
        // Arrange
        var context = new ApiContext
        {
            ApiSymbolId = "MyClass.MyMethod",
            ParameterTypes = new List<string> { "string name", "int count" },
            ReturnType = "bool",
            InheritanceHierarchy = new List<string> { "BaseClass", "IMyInterface" },
            RelatedTypes = new List<string> { "string", "int", "bool" },
            XmlDocComments = "Base class documentation",
            TokenEstimate = 20,
            CallSites = []
        };

        // Act
        var plainText = _collector.FormatAsPlainText(context);

        // Assert
        plainText.ShouldContain("API Symbol: MyClass.MyMethod");
        plainText.ShouldContain("Parameters:");
        plainText.ShouldContain("- string name");
        plainText.ShouldContain("- int count");
        plainText.ShouldContain("Return Type: bool");
        plainText.ShouldContain("Inheritance:");
        plainText.ShouldContain("- BaseClass");
        plainText.ShouldContain("Related Documentation:");
    }

    private async Task<(Compilation compilation, ApiSymbol symbol)> CreateCompilationAndSymbol(
        string code,
        string typeName,
        string memberName)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Threading.Tasks.Task).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Collections.Generic.List<>).Assembly.Location)
            });

        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var root = await syntaxTree.GetRootAsync();

        ISymbol? roslynSymbol = null;
        foreach (var node in root.DescendantNodes())
        {
            var declaredSymbol = semanticModel.GetDeclaredSymbol(node);
            if (declaredSymbol != null && declaredSymbol.Name == memberName)
            {
                roslynSymbol = declaredSymbol;
                break;
            }
        }

        if (roslynSymbol == null)
        {
            throw new InvalidOperationException($"Could not find symbol {memberName}");
        }

        var fullyQualifiedName = roslynSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat
            .WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted));

        var symbol = new ApiSymbol
        {
            Id = Guid.NewGuid().ToString(),
            SymbolType = roslynSymbol switch
            {
                IMethodSymbol => SymbolType.Method,
                IPropertySymbol => SymbolType.Property,
                _ => SymbolType.Class
            },
            FullyQualifiedName = fullyQualifiedName,
            FilePath = "test.cs",
            LineNumber = 1,
            Signature = roslynSymbol.ToDisplayString(),
            AccessModifier = "public",
            IsStatic = false,
            HasDocumentation = false,
            DocumentationStatus = DocumentationStatus.Undocumented
        };

        return (compilation, symbol);
    }
}
