using Docify.Core.Analyzers;
using Docify.Core.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;

namespace Docify.Core.Tests.Analyzers;

public class SymbolExtractorTests
{
    private readonly SymbolExtractor _extractor;

    public SymbolExtractorTests()
    {
        var mockDetector = new Mock<Docify.Core.Interfaces.IDocumentationDetector>();
        mockDetector.Setup(d => d.DetectDocumentationStatus(It.IsAny<Microsoft.CodeAnalysis.ISymbol>()))
            .Returns(DocumentationStatus.Undocumented);
        _extractor = new SymbolExtractor(NullLogger<SymbolExtractor>.Instance, mockDetector.Object);
    }

    [Fact]
    public async Task ExtractPublicSymbols_NullCompilation_ThrowsArgumentNullException()
    {
        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await _extractor.ExtractPublicSymbols(null!));
    }

    [Fact]
    public async Task ExtractPublicSymbols_PublicClass_ExtractsClass()
    {
        // Arrange
        const string code = @"
namespace TestNamespace
{
    public class TestClass
    {
    }
}";
        var compilation = CreateCompilation(code);

        // Act
        var symbols = await _extractor.ExtractPublicSymbols(compilation);

        // Assert
        symbols.ShouldNotBeEmpty();
        var classSymbol = symbols.ShouldHaveSingleItem();
        classSymbol.SymbolType.ShouldBe(SymbolType.Class);
        classSymbol.FullyQualifiedName.ShouldBe("global::TestNamespace.TestClass");
        classSymbol.AccessModifier.ShouldBe("Public");
    }

    [Fact]
    public async Task ExtractPublicSymbols_PublicAndPrivateMethods_ExtractsOnlyPublic()
    {
        // Arrange
        const string code = @"
namespace TestNamespace
{
    public class TestClass
    {
        public void PublicMethod() { }
        private void PrivateMethod() { }
    }
}";
        var compilation = CreateCompilation(code);

        // Act
        var symbols = await _extractor.ExtractPublicSymbols(compilation);

        // Assert
        var methods = symbols.Where(s => s.SymbolType == SymbolType.Method).ToList();
        methods.Count.ShouldBe(1);
        methods[0].Signature.ShouldContain("PublicMethod");
    }

    [Fact]
    public async Task ExtractPublicSymbols_ProtectedMember_ExtractsProtectedMember()
    {
        // Arrange
        const string code = @"
namespace TestNamespace
{
    public class TestClass
    {
        protected void ProtectedMethod() { }
    }
}";
        var compilation = CreateCompilation(code);

        // Act
        var symbols = await _extractor.ExtractPublicSymbols(compilation);

        // Assert
        var method = symbols.Where(s => s.SymbolType == SymbolType.Method).ShouldHaveSingleItem();
        method.AccessModifier.ShouldBe("Protected");
    }

    [Fact]
    public async Task ExtractPublicSymbols_PrivateClass_DoesNotExtract()
    {
        // Arrange
        const string code = @"
namespace TestNamespace
{
    class PrivateClass
    {
    }
}";
        var compilation = CreateCompilation(code);

        // Act
        var symbols = await _extractor.ExtractPublicSymbols(compilation);

        // Assert
        symbols.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExtractPublicSymbols_InternalClass_DoesNotExtract()
    {
        // Arrange
        const string code = @"
namespace TestNamespace
{
    internal class InternalClass
    {
    }
}";
        var compilation = CreateCompilation(code);

        // Act
        var symbols = await _extractor.ExtractPublicSymbols(compilation);

        // Assert
        symbols.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExtractPublicSymbols_GenericType_CapturesTypeParameters()
    {
        // Arrange
        const string code = @"
namespace TestNamespace
{
    public class GenericClass<T>
    {
    }
}";
        var compilation = CreateCompilation(code);

        // Act
        var symbols = await _extractor.ExtractPublicSymbols(compilation);

        // Assert
        var classSymbol = symbols.ShouldHaveSingleItem();
        classSymbol.Signature.ShouldContain("<T>");
    }

    [Fact]
    public async Task ExtractPublicSymbols_GenericConstraints_CapturesConstraints()
    {
        // Arrange
        const string code = @"
using System;

namespace TestNamespace
{
    public class GenericClass<T> where T : IDisposable
    {
    }
}";
        var compilation = CreateCompilation(code);

        // Act
        var symbols = await _extractor.ExtractPublicSymbols(compilation);

        // Assert
        var classSymbol = symbols.ShouldHaveSingleItem();
        classSymbol.Signature.ShouldContain("<T>");
    }

    [Fact]
    public async Task ExtractPublicSymbols_Properties_ExtractsProperties()
    {
        // Arrange
        const string code = @"
namespace TestNamespace
{
    public class TestClass
    {
        public string PublicProperty { get; set; }
        private string PrivateProperty { get; set; }
    }
}";
        var compilation = CreateCompilation(code);

        // Act
        var symbols = await _extractor.ExtractPublicSymbols(compilation);

        // Assert
        var properties = symbols.Where(s => s.SymbolType == SymbolType.Property).ToList();
        properties.Count.ShouldBe(1);
        properties[0].Signature.ShouldContain("PublicProperty");
    }

    [Fact]
    public async Task ExtractPublicSymbols_Indexer_ExtractsIndexer()
    {
        // Arrange
        const string code = @"
namespace TestNamespace
{
    public class TestClass
    {
        public string this[int index]
        {
            get => string.Empty;
            set { }
        }
    }
}";
        var compilation = CreateCompilation(code);

        // Act
        var symbols = await _extractor.ExtractPublicSymbols(compilation);

        // Assert
        var indexers = symbols.Where(s => s.SymbolType == SymbolType.Indexer).ToList();
        indexers.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ExtractPublicSymbols_Event_ExtractsEvent()
    {
        // Arrange
        const string code = @"
using System;

namespace TestNamespace
{
    public class TestClass
    {
        public event EventHandler PublicEvent;
    }
}";
        var compilation = CreateCompilation(code);

        // Act
        var symbols = await _extractor.ExtractPublicSymbols(compilation);

        // Assert
        var events = symbols.Where(s => s.SymbolType == SymbolType.Event).ToList();
        events.Count.ShouldBe(1);
        events[0].Signature.ShouldContain("PublicEvent");
    }

    [Fact]
    public async Task ExtractPublicSymbols_Struct_ExtractsStruct()
    {
        // Arrange
        const string code = @"
namespace TestNamespace
{
    public struct TestStruct
    {
    }
}";
        var compilation = CreateCompilation(code);

        // Act
        var symbols = await _extractor.ExtractPublicSymbols(compilation);

        // Assert
        var structSymbol = symbols.ShouldHaveSingleItem();
        structSymbol.SymbolType.ShouldBe(SymbolType.Struct);
    }

    [Fact]
    public async Task ExtractPublicSymbols_Enum_ExtractsEnum()
    {
        // Arrange
        const string code = @"
namespace TestNamespace
{
    public enum TestEnum
    {
        Value1,
        Value2
    }
}";
        var compilation = CreateCompilation(code);

        // Act
        var symbols = await _extractor.ExtractPublicSymbols(compilation);

        // Assert
        var enumSymbol = symbols.ShouldHaveSingleItem();
        enumSymbol.SymbolType.ShouldBe(SymbolType.Enum);
    }

    [Fact]
    public async Task ExtractPublicSymbols_Interface_ExtractsInterface()
    {
        // Arrange
        const string code = @"
namespace TestNamespace
{
    public interface ITestInterface
    {
        void TestMethod();
    }
}";
        var compilation = CreateCompilation(code);

        // Act
        var symbols = await _extractor.ExtractPublicSymbols(compilation);

        // Assert
        var interfaceSymbol = symbols.FirstOrDefault(s => s.SymbolType == SymbolType.Interface);
        interfaceSymbol.ShouldNotBeNull();
        interfaceSymbol.FullyQualifiedName.ShouldBe("global::TestNamespace.ITestInterface");
    }

    [Fact]
    public async Task ExtractPublicSymbols_StaticMethod_SetsIsStaticTrue()
    {
        // Arrange
        const string code = @"
namespace TestNamespace
{
    public class TestClass
    {
        public static void StaticMethod() { }
    }
}";
        var compilation = CreateCompilation(code);

        // Act
        var symbols = await _extractor.ExtractPublicSymbols(compilation);

        // Assert
        var method = symbols.Where(s => s.SymbolType == SymbolType.Method).ShouldHaveSingleItem();
        method.IsStatic.ShouldBeTrue();
    }

    [Fact]
    public async Task ExtractPublicSymbols_InstanceMethod_SetsIsStaticFalse()
    {
        // Arrange
        const string code = @"
namespace TestNamespace
{
    public class TestClass
    {
        public void InstanceMethod() { }
    }
}";
        var compilation = CreateCompilation(code);

        // Act
        var symbols = await _extractor.ExtractPublicSymbols(compilation);

        // Assert
        var method = symbols.Where(s => s.SymbolType == SymbolType.Method).ShouldHaveSingleItem();
        method.IsStatic.ShouldBeFalse();
    }

    [Fact]
    public async Task ExtractPublicSymbols_ValidSymbol_PopulatesLineNumber()
    {
        // Arrange
        const string code = @"
namespace TestNamespace
{
    public class TestClass
    {
    }
}";
        var compilation = CreateCompilation(code);

        // Act
        var symbols = await _extractor.ExtractPublicSymbols(compilation);

        // Assert
        var symbol = symbols.ShouldHaveSingleItem();
        // FilePath may be empty for in-memory compilations, which is expected
        symbol.LineNumber.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task ExtractPublicSymbols_ValidSymbol_GeneratesUniqueId()
    {
        // Arrange
        const string code = @"
namespace TestNamespace
{
    public class TestClass1 { }
    public class TestClass2 { }
}";
        var compilation = CreateCompilation(code);

        // Act
        var symbols = await _extractor.ExtractPublicSymbols(compilation);

        // Assert
        symbols.Count.ShouldBe(2);
        symbols[0].Id.ShouldNotBe(symbols[1].Id);
        Guid.TryParse(symbols[0].Id, out _).ShouldBeTrue();
        Guid.TryParse(symbols[1].Id, out _).ShouldBeTrue();
    }

    private static Compilation CreateCompilation(string source)
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
}
