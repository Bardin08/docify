using Docify.Core.Models;
using Docify.LLM.PromptEngineering;
using Shouldly;

namespace Docify.LLM.Tests.PromptEngineering;

public class PromptBuilderTests
{
    private readonly PromptBuilder _builder;

    public PromptBuilderTests()
    {
        _builder = new PromptBuilder();
    }

    [Fact]
    public void BuildPrompt_WithNullContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            _builder.BuildPrompt(null!));
    }

    [Fact]
    public void BuildPrompt_WithMinimalContext_IncludesRequiredSections()
    {
        // Arrange
        var context = CreateMinimalContext();

        // Act
        var prompt = _builder.BuildPrompt(context);

        // Assert
        prompt.ShouldNotBeNullOrWhiteSpace();
        prompt.ShouldContain("Generate XML documentation");
        prompt.ShouldContain("API Signature:");
        prompt.ShouldContain("TestNamespace.TestClass.TestMethod");
        prompt.ShouldContain("Output Format:");
        prompt.ShouldContain("<summary>");
    }

    [Fact]
    public void BuildPrompt_WithImplementationBody_IncludesImplementationSection()
    {
        // Arrange
        var context = CreateMinimalContext();
        context = context with
        {
            ImplementationBody = "return a + b;"
        };

        // Act
        var prompt = _builder.BuildPrompt(context);

        // Assert
        prompt.ShouldContain("Implementation:");
        prompt.ShouldContain("return a + b;");
    }

    [Fact]
    public void BuildPrompt_WithTruncatedImplementation_IndicatesTruncation()
    {
        // Arrange
        var context = CreateMinimalContext();
        context = context with
        {
            ImplementationBody = "return a + b;",
            IsImplementationTruncated = true
        };

        // Act
        var prompt = _builder.BuildPrompt(context);

        // Assert
        prompt.ShouldContain("Implementation:");
        prompt.ShouldContain("return a + b;");
        prompt.ShouldContain("(Implementation truncated for token budget)");
    }

    [Fact]
    public void BuildPrompt_WithCalledMethodsDocumentation_IncludesCalledMethodsSection()
    {
        // Arrange
        var context = CreateMinimalContext();
        context = context with
        {
            CalledMethodsDocumentation = new List<CalledMethodDoc>
            {
                new CalledMethodDoc
                {
                    MethodName = "ValidateInput",
                    XmlDocumentation = "/// <summary>Validates the input data.</summary>",
                    IsFresh = true
                },
                new CalledMethodDoc
                {
                    MethodName = "TransformData",
                    XmlDocumentation = "/// <summary>Transforms the data.</summary>",
                    IsFresh = true
                }
            }
        };

        // Act
        var prompt = _builder.BuildPrompt(context);

        // Assert
        prompt.ShouldContain("Called Methods Documentation:");
        prompt.ShouldContain("ValidateInput");
        prompt.ShouldContain("Validates the input data.");
        prompt.ShouldContain("TransformData");
        prompt.ShouldContain("Transforms the data.");
    }

    [Fact]
    public void BuildPrompt_WithInheritanceHierarchy_IncludesTypeRelationshipsSection()
    {
        // Arrange
        var context = CreateMinimalContext();
        context = context with
        {
            InheritanceHierarchy = new List<string> { "Object", "BaseService", "MyService" }
        };

        // Act
        var prompt = _builder.BuildPrompt(context);

        // Assert
        prompt.ShouldContain("Type Relationships:");
        prompt.ShouldContain("Object -> BaseService -> MyService");
    }

    [Fact]
    public void BuildPrompt_WithRelatedTypes_IncludesRelatedTypes()
    {
        // Arrange
        var context = CreateMinimalContext();
        context = context with
        {
            RelatedTypes = new List<string> { "Request", "Response", "Config" }
        };

        // Act
        var prompt = _builder.BuildPrompt(context);

        // Assert
        prompt.ShouldContain("Related types:");
        prompt.ShouldContain("Request");
        prompt.ShouldContain("Response");
        prompt.ShouldContain("Config");
    }

    [Fact]
    public void BuildPrompt_WithManyRelatedTypes_ExcludesRelatedTypes()
    {
        // Arrange
        var context = CreateMinimalContext();
        context = context with
        {
            RelatedTypes = new List<string>
            {
                "Type1", "Type2", "Type3", "Type4", "Type5",
                "Type6", "Type7", "Type8", "Type9", "Type10",
                "Type11" // More than 10
            }
        };

        // Act
        var prompt = _builder.BuildPrompt(context);

        // Assert
        prompt.ShouldNotContain("Related types:");
        prompt.ShouldNotContain("Type1");
    }

    [Fact]
    public void BuildPrompt_WithCallSites_IncludesUsageExamplesSection()
    {
        // Arrange
        var context = CreateMinimalContext();
        context = context with
        {
            CallSites = new List<CallSiteInfo>
            {
                new CallSiteInfo
                {
                    FilePath = "TestFile.cs",
                    LineNumber = 42,
                    ContextBefore = new List<string> { "var calculator = new Calculator();" },
                    CallExpression = "var result = calculator.Add(2, 3);",
                    ContextAfter = new List<string> { "Console.WriteLine(result);" }
                },
                new CallSiteInfo
                {
                    FilePath = "AnotherFile.cs",
                    LineNumber = 100,
                    ContextBefore = new List<string>(),
                    CallExpression = "calculator.Add(-5, 10);",
                    ContextAfter = new List<string>()
                }
            }
        };

        // Act
        var prompt = _builder.BuildPrompt(context);

        // Assert
        prompt.ShouldContain("Usage Examples:");
        prompt.ShouldContain("Example 1 (TestFile.cs:42):");
        prompt.ShouldContain("var calculator = new Calculator();");
        prompt.ShouldContain("var result = calculator.Add(2, 3);");
        prompt.ShouldContain("Console.WriteLine(result);");
        prompt.ShouldContain("Example 2 (AnotherFile.cs:100):");
        prompt.ShouldContain("calculator.Add(-5, 10);");
    }

    [Fact]
    public void BuildPrompt_WithMaximumCallSites_IncludesOnlyFirstThree()
    {
        // Arrange
        var context = CreateMinimalContext();
        context = context with
        {
            CallSites = new List<CallSiteInfo>
            {
                CreateCallSite("File1.cs", 1),
                CreateCallSite("File2.cs", 2),
                CreateCallSite("File3.cs", 3),
                CreateCallSite("File4.cs", 4),
                CreateCallSite("File5.cs", 5)
            }
        };

        // Act
        var prompt = _builder.BuildPrompt(context);

        // Assert
        prompt.ShouldContain("Example 1 (File1.cs:1):");
        prompt.ShouldContain("Example 2 (File2.cs:2):");
        prompt.ShouldContain("Example 3 (File3.cs:3):");
        prompt.ShouldNotContain("File4.cs");
        prompt.ShouldNotContain("File5.cs");
    }

    [Fact]
    public void BuildPrompt_WithXmlDocComments_IncludesRelatedDocumentationSection()
    {
        // Arrange
        var context = CreateMinimalContext();
        context = context with
        {
            XmlDocComments = "/// <summary>Processes data from various sources.</summary>"
        };

        // Act
        var prompt = _builder.BuildPrompt(context);

        // Assert
        prompt.ShouldContain("Related Documentation:");
        prompt.ShouldContain("Base class or interface documentation:");
        prompt.ShouldContain("Processes data from various sources.");
    }

    [Fact]
    public void BuildPrompt_WithParameters_IncludesParameterList()
    {
        // Arrange
        var context = CreateMinimalContext();
        context = context with
        {
            ParameterTypes = new List<string> { "string name", "int age" }
        };

        // Act
        var prompt = _builder.BuildPrompt(context);

        // Assert
        prompt.ShouldContain("Parameters:");
        prompt.ShouldContain("string name");
        prompt.ShouldContain("int age");
    }

    [Fact]
    public void BuildPrompt_WithReturnType_IncludesReturnType()
    {
        // Arrange
        var context = CreateMinimalContext();
        context = context with
        {
            ReturnType = "bool"
        };

        // Act
        var prompt = _builder.BuildPrompt(context);

        // Assert
        prompt.ShouldContain("Return Type: bool");
    }

    [Fact]
    public void BuildPrompt_IncludesOutputFormatSpecification()
    {
        // Arrange
        var context = CreateMinimalContext();

        // Act
        var prompt = _builder.BuildPrompt(context);

        // Assert
        prompt.ShouldContain("Output Format:");
        prompt.ShouldContain("<summary>");
        prompt.ShouldContain("<param");
        prompt.ShouldContain("<returns>");
        prompt.ShouldContain("<exception");
        prompt.ShouldContain("<remarks>");
    }

    [Fact]
    public void BuildPrompt_IncludesStyleGuidelines()
    {
        // Arrange
        var context = CreateMinimalContext();

        // Act
        var prompt = _builder.BuildPrompt(context);

        // Assert
        prompt.ShouldContain("Style Guidelines:");
        prompt.ShouldContain("concise");
        prompt.ShouldContain("present tense");
        prompt.ShouldContain("third person");
    }

    [Fact]
    public void BuildPrompt_IncludesExamplesOfGoodDocumentation()
    {
        // Arrange
        var context = CreateMinimalContext();

        // Act
        var prompt = _builder.BuildPrompt(context);

        // Assert
        prompt.ShouldContain("Examples of Good Documentation:");
        prompt.ShouldContain("Example 1 (Method with parameters):");
        prompt.ShouldContain("Example 2 (Property):");
    }

    [Fact]
    public void BuildPrompt_WithCompleteContext_GeneratesComprehensivePrompt()
    {
        // Arrange
        var context = new ApiContext
        {
            ApiSymbolId = "TestNamespace.Calculator.Divide",
            ParameterTypes = new List<string> { "double numerator", "double denominator" },
            ReturnType = "double",
            InheritanceHierarchy = new List<string> { "Object", "BaseCalculator", "Calculator" },
            RelatedTypes = new List<string> { "DivideByZeroException" },
            TokenEstimate = 500,
            ImplementationBody = "if (denominator == 0) throw new DivideByZeroException(); return numerator / denominator;",
            CalledMethodsDocumentation = new List<CalledMethodDoc>
            {
                new CalledMethodDoc
                {
                    MethodName = "ValidateInput",
                    XmlDocumentation = "/// <summary>Validates division input.</summary>",
                    IsFresh = true
                }
            },
            CallSites = new List<CallSiteInfo>
            {
                new CallSiteInfo
                {
                    FilePath = "Program.cs",
                    LineNumber = 10,
                    ContextBefore = new List<string>(),
                    CallExpression = "var result = calculator.Divide(10, 2);",
                    ContextAfter = new List<string>()
                }
            },
            XmlDocComments = "/// <summary>Divides two numbers.</summary>",
            IsImplementationTruncated = false
        };

        // Act
        var prompt = _builder.BuildPrompt(context);

        // Assert
        // Verify all major sections are present
        prompt.ShouldContain("Generate XML documentation");
        prompt.ShouldContain("API Signature:");
        prompt.ShouldContain("TestNamespace.Calculator.Divide");
        prompt.ShouldContain("Parameters:");
        prompt.ShouldContain("double numerator");
        prompt.ShouldContain("double denominator");
        prompt.ShouldContain("Return Type: double");
        prompt.ShouldContain("Implementation:");
        prompt.ShouldContain("DivideByZeroException");
        prompt.ShouldContain("Called Methods Documentation:");
        prompt.ShouldContain("ValidateInput");
        prompt.ShouldContain("Type Relationships:");
        prompt.ShouldContain("Object -> BaseCalculator -> Calculator");
        prompt.ShouldContain("Usage Examples:");
        prompt.ShouldContain("Program.cs:10");
        prompt.ShouldContain("Related Documentation:");
        prompt.ShouldContain("Divides two numbers.");
        prompt.ShouldContain("Output Format:");
        prompt.ShouldContain("Style Guidelines:");
        prompt.ShouldContain("Examples of Good Documentation:");
    }

    private static ApiContext CreateMinimalContext()
    {
        return new ApiContext
        {
            ApiSymbolId = "TestNamespace.TestClass.TestMethod",
            ParameterTypes = new List<string>(),
            ReturnType = null,
            InheritanceHierarchy = new List<string>(),
            RelatedTypes = new List<string>(),
            TokenEstimate = 100,
            CallSites = new List<CallSiteInfo>(),
            CalledMethodsDocumentation = new List<CalledMethodDoc>(),
            IsImplementationTruncated = false
        };
    }

    private static CallSiteInfo CreateCallSite(string filePath, int lineNumber)
    {
        return new CallSiteInfo
        {
            FilePath = filePath,
            LineNumber = lineNumber,
            ContextBefore = new List<string>(),
            CallExpression = $"Method call at line {lineNumber}",
            ContextAfter = new List<string>()
        };
    }
}
