using Docify.Writer.Writing;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;

namespace Docify.Writer.Tests.Writing;

public class FormattingPreserverTests
{
    [Fact]
    public void ValidateFormattingPreserved_WithMatchingContent_ReturnsTrue()
    {
        // Arrange
        var original = "public class Foo\n{\n    public void Bar() { }\n}";
        var inserted = "    /// <summary>Test</summary>\n";
        var modified = "public class Foo\n{\n    /// <summary>Test</summary>\n    public void Bar() { }\n}";

        // Act
        var result = FormattingPreserver.ValidateFormattingPreserved(original, modified, inserted);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ValidateFormattingPreserved_WithExtraChanges_ReturnsFalse()
    {
        // Arrange
        var original = "public class Foo\n{\n    public void Bar() { }\n}";
        var inserted = "    /// <summary>Test</summary>\n";
        var modified = "public class Foo\n{\n    /// <summary>Test</summary>\n    public int Bar() { }\n}"; // Changed return type

        // Act
        var result = FormattingPreserver.ValidateFormattingPreserved(original, modified, inserted);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void ValidateFormattingPreserved_WithCRLFLineEndings_ReturnsTrue()
    {
        // Arrange
        var original = "public class Foo\r\n{\r\n    public void Bar() { }\r\n}";
        var inserted = "    /// <summary>Test</summary>\r\n";
        var modified = "public class Foo\r\n{\r\n    /// <summary>Test</summary>\r\n    public void Bar() { }\r\n}";

        // Act
        var result = FormattingPreserver.ValidateFormattingPreserved(original, modified, inserted);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void CountBlankLinesBefore_WithNoBlankLines_ReturnsZero()
    {
        // Arrange
        var code = "public class Test\n{\n    public void Method() { }\n}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var methodNode = root.DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>()
            .First();

        // Act
        var blankLines = FormattingPreserver.CountBlankLinesBefore(methodNode);

        // Assert
        blankLines.ShouldBe(0);
    }

    [Fact]
    public void CountBlankLinesBefore_WithOneBlankLine_ReturnsOne()
    {
        // Arrange
        var code = "public class Test\n{\n    public void Method1() { }\n\n    public void Method2() { }\n}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var methods = root.DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>()
            .ToList();
        var method2 = methods[1]; // Second method

        // Act
        var blankLines = FormattingPreserver.CountBlankLinesBefore(method2);

        // Assert
        blankLines.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void CountBlankLinesBefore_WithMultipleBlankLines_ReturnsCorrectCount()
    {
        // Arrange
        var code = "public class Test\n{\n    public void Method1() { }\n\n\n    public void Method2() { }\n}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var methods = root.DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>()
            .ToList();
        var method2 = methods[1];

        // Act
        var blankLines = FormattingPreserver.CountBlankLinesBefore(method2);

        // Assert
        blankLines.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void ValidateOnlyDocumentationAdded_WithValidInsertion_ReturnsTrue()
    {
        // Arrange
        var original = "public class Foo\n{\n    public void Bar() { }\n}";
        var inserted = "    /// <summary>Test</summary>\n";
        var insertionPosition = original.IndexOf("    public void Bar");
        var modified = original.Insert(insertionPosition, inserted);

        // Act
        var result = FormattingPreserver.ValidateOnlyDocumentationAdded(original, modified, inserted, insertionPosition);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ValidateOnlyDocumentationAdded_WithModifiedCode_ReturnsFalse()
    {
        // Arrange
        var original = "public class Foo\n{\n    public void Bar() { }\n}";
        var inserted = "    /// <summary>Test</summary>\n";
        var insertionPosition = original.IndexOf("    public void Bar");
        var modified = original.Insert(insertionPosition, inserted).Replace("void Bar", "int Bar");

        // Act
        var result = FormattingPreserver.ValidateOnlyDocumentationAdded(original, modified, inserted, insertionPosition);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void ValidateOnlyDocumentationAdded_WithInvalidPosition_ReturnsFalse()
    {
        // Arrange
        var original = "public class Foo\n{\n    public void Bar() { }\n}";
        var inserted = "    /// <summary>Test</summary>\n";
        var invalidPosition = original.Length + 100;
        var modified = original + inserted;

        // Act
        var result = FormattingPreserver.ValidateOnlyDocumentationAdded(original, modified, inserted, invalidPosition);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void ExtractIndentation_WithTabIndentation_ReturnsTabs()
    {
        // Arrange
        var code = "public class Test\n{\n\tpublic void Method() { }\n}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var methodNode = root.DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>()
            .First();

        // Act
        var indentation = FormattingPreserver.ExtractIndentation(methodNode);

        // Assert
        indentation.ShouldContain("\t");
    }

    [Fact]
    public void ExtractIndentation_WithSpaceIndentation_ReturnsSpaces()
    {
        // Arrange
        var code = "public class Test\n{\n    public void Method() { }\n}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var methodNode = root.DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>()
            .First();

        // Act
        var indentation = FormattingPreserver.ExtractIndentation(methodNode);

        // Assert
        indentation.ShouldBe("    "); // 4 spaces
    }

    [Fact]
    public void DetectLineEnding_WithCRLF_ReturnsCRLF()
    {
        // Arrange
        var content = "Line1\r\nLine2\r\nLine3\r\n";

        // Act
        var lineEnding = FormattingPreserver.DetectLineEnding(content);

        // Assert
        lineEnding.ShouldBe("\r\n");
    }

    [Fact]
    public void DetectLineEnding_WithLF_ReturnsLF()
    {
        // Arrange
        var content = "Line1\nLine2\nLine3\n";

        // Act
        var lineEnding = FormattingPreserver.DetectLineEnding(content);

        // Assert
        lineEnding.ShouldBe("\n");
    }

    [Fact]
    public void DetectLineEnding_WithMixedLineEndings_ReturnsPrevalentFormat()
    {
        // Arrange
        var content = "Line1\r\nLine2\r\nLine3\nLine4\r\n"; // 3 CRLF, 1 LF

        // Act
        var lineEnding = FormattingPreserver.DetectLineEnding(content);

        // Assert
        lineEnding.ShouldBe("\r\n"); // CRLF is more prevalent
    }
}
