using Docify.Core.Models;
using Docify.LLM.Validation;
using Shouldly;
using Xunit;

namespace Docify.LLM.Tests.Validation;

/// <summary>
/// Unit tests for OutputValidator class.
/// Tests XML documentation validation including syntax checking, required tags, and edge cases.
/// </summary>
public class OutputValidatorTests
{
    private readonly OutputValidator _validator = new();

    [Fact]
    public void ValidateXmlDocumentation_WithValidXml_ReturnsValidResult()
    {
        // Arrange
        var generatedXml = "<summary>Validates input.</summary>";
        var context = CreateMinimalContext();

        // Act
        var result = _validator.ValidateXmlDocumentation(generatedXml, context);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.Issues.ShouldBeEmpty();
        result.CleanedXml.ShouldBeNull();
    }

    private static ApiContext CreateMinimalContext()
    {
        return new ApiContext
        {
            ApiSymbolId = "M:Sample.Method",
            ParameterTypes = [],
            ReturnType = "void",
            InheritanceHierarchy = [],
            RelatedTypes = [],
            TokenEstimate = 100,
            CallSites = [],
            CalledMethodsDocumentation = [],
            IsImplementationTruncated = false
        };
    }

    [Fact]
    public void ValidateXmlDocumentation_WithValidXmlAndParameters_ReturnsValidResult()
    {
        // Arrange
        var generatedXml = """
            <summary>Validates input.</summary>
            <param name="input">The input string.</param>
            <param name="count">The count value.</param>
            """;
        var context = CreateMinimalContext() with
        {
            ParameterTypes = ["string input", "int count"]
        };

        // Act
        var result = _validator.ValidateXmlDocumentation(generatedXml, context);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.Issues.ShouldBeEmpty();
        result.CleanedXml.ShouldBeNull();
    }

    [Fact]
    public void ValidateXmlDocumentation_WithValidXmlAndReturnValue_ReturnsValidResult()
    {
        // Arrange
        var generatedXml = """
            <summary>Calculates the sum.</summary>
            <returns>The sum of the values.</returns>
            """;
        var context = CreateMinimalContext() with
        {
            ReturnType = "int"
        };

        // Act
        var result = _validator.ValidateXmlDocumentation(generatedXml, context);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.Issues.ShouldBeEmpty();
        result.CleanedXml.ShouldBeNull();
    }

    [Fact]
    public void ValidateXmlDocumentation_MissingSummaryTag_ReturnsInvalidResult()
    {
        // Arrange
        var generatedXml = "<param name=\"input\">The input.</param>";
        var context = CreateMinimalContext() with
        {
            ParameterTypes = ["string input"]
        };

        // Act
        var result = _validator.ValidateXmlDocumentation(generatedXml, context);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Issues.ShouldContain("Missing required <summary> tag");
    }

    [Fact]
    public void ValidateXmlDocumentation_MissingParamTag_ReturnsInvalidResult()
    {
        // Arrange
        var generatedXml = "<summary>Validates input.</summary>";
        var context = CreateMinimalContext() with
        {
            ParameterTypes = ["string input"]
        };

        // Act
        var result = _validator.ValidateXmlDocumentation(generatedXml, context);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Issues.ShouldContain("Missing required <param name=\"input\"> tag");
    }

    [Fact]
    public void ValidateXmlDocumentation_MissingMultipleParamTags_ReturnsInvalidResult()
    {
        // Arrange
        var generatedXml = """
            <summary>Validates input.</summary>
            <param name="input">The input string.</param>
            """;
        var context = CreateMinimalContext() with
        {
            ParameterTypes = ["string input", "int count"]
        };

        // Act
        var result = _validator.ValidateXmlDocumentation(generatedXml, context);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Issues.ShouldContain("Missing required <param name=\"count\"> tag");
    }

    [Fact]
    public void ValidateXmlDocumentation_MissingReturnsTag_ReturnsInvalidResult()
    {
        // Arrange
        var generatedXml = "<summary>Calculates the sum.</summary>";
        var context = CreateMinimalContext() with
        {
            ReturnType = "int"
        };

        // Act
        var result = _validator.ValidateXmlDocumentation(generatedXml, context);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Issues.ShouldContain("Missing required <returns> tag");
    }

    [Fact]
    public void ValidateXmlDocumentation_VoidReturnNoReturnsTag_ReturnsValidResult()
    {
        // Arrange
        var generatedXml = "<summary>Performs validation.</summary>";
        var context = CreateMinimalContext();

        // Act
        var result = _validator.ValidateXmlDocumentation(generatedXml, context);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.Issues.ShouldBeEmpty();
    }

    [Fact]
    public void ValidateXmlDocumentation_InvalidXmlSyntax_ReturnsInvalidResult()
    {
        // Arrange
        var generatedXml = "<summary>Unclosed tag";
        var context = CreateMinimalContext();

        // Act
        var result = _validator.ValidateXmlDocumentation(generatedXml, context);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Issues.Count.ShouldBeGreaterThan(0);
        result.Issues[0].ShouldContain("Invalid XML syntax");
    }

    [Fact]
    public void ValidateXmlDocumentation_EmptyResponse_ReturnsInvalidResult()
    {
        // Arrange
        var generatedXml = "";
        var context = CreateMinimalContext();

        // Act
        var result = _validator.ValidateXmlDocumentation(generatedXml, context);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Issues.ShouldContain("LLM returned empty response");
    }

    [Fact]
    public void ValidateXmlDocumentation_NullResponse_ReturnsInvalidResult()
    {
        // Arrange
        string? generatedXml = null;
        var context = CreateMinimalContext();

        // Act
        var result = _validator.ValidateXmlDocumentation(generatedXml, context);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Issues.ShouldContain("LLM returned empty response");
    }

    [Fact]
    public void ValidateXmlDocumentation_WhitespaceResponse_ReturnsInvalidResult()
    {
        // Arrange
        var generatedXml = "   \n\t  ";
        var context = CreateMinimalContext();

        // Act
        var result = _validator.ValidateXmlDocumentation(generatedXml, context);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Issues.ShouldContain("LLM returned empty response");
    }

    [Fact]
    public void ValidateXmlDocumentation_NonXmlTextWithEmbeddedXml_ExtractsAndValidates()
    {
        // Arrange
        var generatedXml = """
            Here is the documentation:
            <summary>Validates input.</summary>
            <param name="input">The input string.</param>
            Hope this helps!
            """;
        var context = CreateMinimalContext() with
        {
            ParameterTypes = ["string input"]
        };

        // Act
        var result = _validator.ValidateXmlDocumentation(generatedXml, context);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.CleanedXml.ShouldNotBeNull();
        result.CleanedXml.ShouldContain("<summary>Validates input.</summary>");
        result.CleanedXml.ShouldContain("<param name=\"input\">The input string.</param>");
    }

    [Fact]
    public void ValidateXmlDocumentation_NonXmlTextWithoutEmbeddedXml_ReturnsInvalidResult()
    {
        // Arrange
        var generatedXml = "This is just plain text without any XML tags.";
        var context = CreateMinimalContext();

        // Act
        var result = _validator.ValidateXmlDocumentation(generatedXml, context);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Issues[0].ShouldContain("Missing required <summary> tag");
    }

    [Fact]
    public void ValidateXmlDocumentation_WithRemarksTag_ReturnsValidResult()
    {
        // Arrange
        var generatedXml = """
            <summary>Validates input.</summary>
            <remarks>This method performs comprehensive validation.</remarks>
            """;
        var context = CreateMinimalContext();

        // Act
        var result = _validator.ValidateXmlDocumentation(generatedXml, context);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.Issues.ShouldBeEmpty();
    }

    [Fact]
    public void ValidateXmlDocumentation_WithExceptionTag_ReturnsValidResult()
    {
        // Arrange
        var generatedXml = """
            <summary>Validates input.</summary>
            <param name="input">The input string.</param>
            <exception cref="ArgumentNullException">Thrown when input is null.</exception>
            """;
        var context = CreateMinimalContext() with
        {
            ParameterTypes = ["string input"]
        };

        // Act
        var result = _validator.ValidateXmlDocumentation(generatedXml, context);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.Issues.ShouldBeEmpty();
    }

    [Fact]
    public void ValidateXmlDocumentation_NullContext_ThrowsArgumentNullException()
    {
        // Arrange
        var generatedXml = "<summary>Test</summary>";

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => _validator.ValidateXmlDocumentation(generatedXml, null!));
    }

    [Fact]
    public void ValidateXmlDocumentation_ComplexMethodWithAllTags_ReturnsValidResult()
    {
        // Arrange
        var generatedXml = """
            <summary>Processes the data with advanced validation.</summary>
            <param name="data">The data to process.</param>
            <param name="options">The processing options.</param>
            <returns>The processed result.</returns>
            <remarks>This method uses advanced algorithms for data processing.</remarks>
            <exception cref="InvalidOperationException">Thrown when data is invalid.</exception>
            """;
        var context = CreateMinimalContext() with
        {
            ParameterTypes = ["string data", "Options options"],
            ReturnType = "Result"
        };

        // Act
        var result = _validator.ValidateXmlDocumentation(generatedXml, context);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.Issues.ShouldBeEmpty();
    }
}
