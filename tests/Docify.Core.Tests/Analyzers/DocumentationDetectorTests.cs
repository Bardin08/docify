using Docify.Core.Analyzers;
using Docify.Core.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace Docify.Core.Tests.Analyzers;

public class DocumentationDetectorTests
{
    private readonly DocumentationDetector _detector;

    public DocumentationDetectorTests()
    {
        var mockLogger = new Mock<ILogger<DocumentationDetector>>();
        _detector = new DocumentationDetector(mockLogger.Object);
    }

    [Fact]
    public void DetectDocumentationStatus_FullyDocumentedMethod_ReturnsDocumented()
    {
        // Arrange
        const string code = @"
            public class TestClass
            {
                /// <summary>Test summary</summary>
                /// <param name=""value"">Test param</param>
                /// <returns>Test return</returns>
                public int MyMethod(string value) { return 0; }
            }";

        var symbol = GetMethodSymbol(code, "MyMethod");

        // Act
        var status = _detector.DetectDocumentationStatus(symbol);

        // Assert
        status.ShouldBe(DocumentationStatus.Documented);
    }

    [Fact]
    public void DetectDocumentationStatus_NoXmlComments_ReturnsUndocumented()
    {
        // Arrange
        const string code = @"
            public class TestClass
            {
                public int MyMethod(string value) { return 0; }
            }";

        var symbol = GetMethodSymbol(code, "MyMethod");

        // Act
        var status = _detector.DetectDocumentationStatus(symbol);

        // Assert
        status.ShouldBe(DocumentationStatus.Undocumented);
    }

    [Fact]
    public void DetectDocumentationStatus_EmptySummaryTag_ReturnsUndocumented()
    {
        // Arrange
        const string code = @"
            public class TestClass
            {
                /// <summary></summary>
                public int MyMethod(string value) { return 0; }
            }";

        var symbol = GetMethodSymbol(code, "MyMethod");

        // Act
        var status = _detector.DetectDocumentationStatus(symbol);

        // Assert
        status.ShouldBe(DocumentationStatus.Undocumented);
    }

    [Fact]
    public void DetectDocumentationStatus_MissingParamTag_ReturnsPartiallyDocumented()
    {
        // Arrange
        const string code = @"
            public class TestClass
            {
                /// <summary>Test summary</summary>
                /// <returns>Test return</returns>
                public int MyMethod(string value) { return 0; }
            }";

        var symbol = GetMethodSymbol(code, "MyMethod");

        // Act
        var status = _detector.DetectDocumentationStatus(symbol);

        // Assert
        status.ShouldBe(DocumentationStatus.PartiallyDocumented);
    }

    [Fact]
    public void DetectDocumentationStatus_MissingReturnsTag_ReturnsPartiallyDocumented()
    {
        // Arrange
        const string code = @"
            public class TestClass
            {
                /// <summary>Test summary</summary>
                /// <param name=""value"">Test param</param>
                public int MyMethod(string value) { return 0; }
            }";

        var symbol = GetMethodSymbol(code, "MyMethod");

        // Act
        var status = _detector.DetectDocumentationStatus(symbol);

        // Assert
        status.ShouldBe(DocumentationStatus.PartiallyDocumented);
    }

    [Fact]
    public void DetectDocumentationStatus_PropertyWithSummary_ReturnsDocumented()
    {
        // Arrange
        const string code = @"
            public class TestClass
            {
                /// <summary>Test property</summary>
                public int MyProperty { get; set; }
            }";

        var symbol = GetPropertySymbol(code, "MyProperty");

        // Act
        var status = _detector.DetectDocumentationStatus(symbol);

        // Assert
        status.ShouldBe(DocumentationStatus.Documented);
    }

    [Fact]
    public void DetectDocumentationStatus_MultipleParametersSomeDocumented_ReturnsPartiallyDocumented()
    {
        // Arrange
        const string code = @"
            public class TestClass
            {
                /// <summary>Test summary</summary>
                /// <param name=""first"">First param</param>
                /// <returns>Test return</returns>
                public int MyMethod(string first, int second, bool third) { return 0; }
            }";

        var symbol = GetMethodSymbol(code, "MyMethod");

        // Act
        var status = _detector.DetectDocumentationStatus(symbol);

        // Assert
        status.ShouldBe(DocumentationStatus.PartiallyDocumented);
    }

    [Fact]
    public void DetectDocumentationStatus_VoidMethodWithSummaryAndParams_ReturnsDocumented()
    {
        // Arrange
        const string code = @"
            public class TestClass
            {
                /// <summary>Test summary</summary>
                /// <param name=""value"">Test param</param>
                public void MyMethod(string value) { }
            }";

        var symbol = GetMethodSymbol(code, "MyMethod");

        // Act
        var status = _detector.DetectDocumentationStatus(symbol);

        // Assert
        status.ShouldBe(DocumentationStatus.Documented);
    }

    [Fact]
    public void DetectDocumentationStatus_ClassWithSummary_ReturnsDocumented()
    {
        // Arrange
        const string code = @"
            /// <summary>Test class</summary>
            public class TestClass
            {
            }";

        var symbol = GetClassSymbol(code, "TestClass");

        // Act
        var status = _detector.DetectDocumentationStatus(symbol);

        // Assert
        status.ShouldBe(DocumentationStatus.Documented);
    }

    [Fact]
    public void GetXmlDocumentation_WithDocumentation_ReturnsXmlString()
    {
        // Arrange
        const string code = @"
            public class TestClass
            {
                /// <summary>Test summary</summary>
                public int MyProperty { get; set; }
            }";

        var symbol = GetPropertySymbol(code, "MyProperty");

        // Act
        var xml = _detector.GetXmlDocumentation(symbol);

        // Assert
        xml.ShouldNotBeNullOrEmpty();
        xml.ShouldContain("<summary>Test summary</summary>");
    }

    [Fact]
    public void GetXmlDocumentation_WithoutDocumentation_ReturnsEmptyOrNull()
    {
        // Arrange
        const string code = @"
            public class TestClass
            {
                public int MyProperty { get; set; }
            }";

        var symbol = GetPropertySymbol(code, "MyProperty");

        // Act
        var xml = _detector.GetXmlDocumentation(symbol);

        // Assert
        xml.ShouldBeNullOrEmpty();
    }

    [Fact]
    public void DetectDocumentationStatus_NullSymbol_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => _detector.DetectDocumentationStatus(null!));
    }

    [Fact]
    public void GetXmlDocumentation_NullSymbol_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => _detector.GetXmlDocumentation(null!));
    }

    private static IMethodSymbol GetMethodSymbol(string code, string methodName)
    {
        var compilation = CreateCompilation(code);
        var tree = compilation.SyntaxTrees.First();
        var model = compilation.GetSemanticModel(tree);
        var methodSyntax = tree.GetRoot()
            .DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>()
            .First(m => m.Identifier.Text == methodName);

        return model.GetDeclaredSymbol(methodSyntax)!;
    }

    private static IPropertySymbol GetPropertySymbol(string code, string propertyName)
    {
        var compilation = CreateCompilation(code);
        var tree = compilation.SyntaxTrees.First();
        var model = compilation.GetSemanticModel(tree);
        var propertySyntax = tree.GetRoot()
            .DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.PropertyDeclarationSyntax>()
            .First(p => p.Identifier.Text == propertyName);

        return model.GetDeclaredSymbol(propertySyntax)!;
    }

    private static INamedTypeSymbol GetClassSymbol(string code, string className)
    {
        var compilation = CreateCompilation(code);
        var tree = compilation.SyntaxTrees.First();
        var model = compilation.GetSemanticModel(tree);
        var classSyntax = tree.GetRoot()
            .DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>()
            .First(c => c.Identifier.Text == className);

        return model.GetDeclaredSymbol(classSyntax)!;
    }

    private static CSharpCompilation CreateCompilation(string code)
    {
        var parseOptions = new CSharpParseOptions(
            languageVersion: LanguageVersion.CSharp12,
            documentationMode: DocumentationMode.Parse);

        var syntaxTree = CSharpSyntaxTree.ParseText(code, parseOptions);
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
        };

        return CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}
