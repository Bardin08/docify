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

public class ContextCollectionIntegrationTests
{
    private readonly Mock<ILogger<SignatureContextCollector>> _mockLogger;
    private readonly Mock<ILogger<CallSiteCollector>> _mockCallSiteLogger;
    private readonly Mock<IStalenessDetector> _mockStalenessDetector;

    public ContextCollectionIntegrationTests()
    {
        _mockLogger = new Mock<ILogger<SignatureContextCollector>>();
        _mockCallSiteLogger = new Mock<ILogger<CallSiteCollector>>();
        _mockStalenessDetector = new Mock<IStalenessDetector>();
    }

    [Fact]
    public async Task CollectContext_WithComplexMethod_ReturnsCompleteContext()
    {
        // Arrange
        var compilation = CreateTestCompilation(@"
            using System;
            using System.Threading.Tasks;

            public class DocumentProcessor {
                /// <summary>Processes a document with validation.</summary>
                public async Task<string> ProcessDocument(string input) {
                    if (string.IsNullOrEmpty(input))
                        throw new ArgumentException(""Input cannot be empty"");

                    var validated = await ValidateInput(input);
                    var transformed = TransformData(validated);
                    LogResult(transformed);

                    return transformed;
                }

                /// <summary>Validates the input string.</summary>
                private async Task<string> ValidateInput(string input) {
                    await Task.Delay(10);
                    return input.Trim();
                }

                /// <summary>Transforms the data to uppercase.</summary>
                private string TransformData(string data) {
                    return data.ToUpper();
                }

                /// <summary>STALE: Logs the result.</summary>
                private void LogResult(string result) {
                    Console.WriteLine(result);
                }
            }
        ");

        // Setup staleness detection
        _mockStalenessDetector
            .Setup(x => x.DetectStaleDocumentation(It.Is<ApiSymbol>(s => s.FullyQualifiedName.Contains("ValidateInput"))))
            .Returns(new StalenessResult { IsStale = false });

        _mockStalenessDetector
            .Setup(x => x.DetectStaleDocumentation(It.Is<ApiSymbol>(s => s.FullyQualifiedName.Contains("TransformData"))))
            .Returns(new StalenessResult { IsStale = false });

        _mockStalenessDetector
            .Setup(x => x.DetectStaleDocumentation(It.Is<ApiSymbol>(s => s.FullyQualifiedName.Contains("LogResult"))))
            .Returns(new StalenessResult { IsStale = true, Severity = StalenessSeverity.Critical });

        var callSiteCollector = new CallSiteCollector(_mockCallSiteLogger.Object);
        var collector = new SignatureContextCollector(_mockLogger.Object, callSiteCollector, _mockStalenessDetector.Object);
        var symbol = GetApiSymbol(compilation, "ProcessDocument");

        // Act
        var context = await collector.CollectContext(symbol, compilation);

        // Assert - Verify all aspects of context collection

        // 1. Implementation body extracted
        context.ImplementationBody.ShouldNotBeNull();
        context.ImplementationBody.ShouldContain("string.IsNullOrEmpty");
        context.ImplementationBody.ShouldContain("ValidateInput");
        context.ImplementationBody.ShouldContain("TransformData");
        context.ImplementationBody.ShouldContain("LogResult");
        context.IsImplementationTruncated.ShouldBeFalse();

        // 2. Called methods documentation collected (only fresh)
        context.CalledMethodsDocumentation.Count.ShouldBe(2); // ValidateInput and TransformData (LogResult is stale)
        context.CalledMethodsDocumentation.ShouldAllBe(doc => doc.IsFresh);
        context.CalledMethodsDocumentation.ShouldContain(doc => doc.XmlDocumentation.Contains("Validates the input string"));
        context.CalledMethodsDocumentation.ShouldContain(doc => doc.XmlDocumentation.Contains("Transforms the data to uppercase"));
        context.CalledMethodsDocumentation.ShouldNotContain(doc => doc.XmlDocumentation.Contains("STALE"));

        // 3. Signature information
        context.ParameterTypes.Count.ShouldBe(1);
        context.ParameterTypes[0].ShouldContain("string input");
        context.ReturnType.ShouldContain("Task<string>");

        // 4. Token estimate includes all context
        context.TokenEstimate.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task CollectContext_WithPropertyAndHelpers_CollectsImplementationAndDocs()
    {
        // Arrange
        var compilation = CreateTestCompilation(@"
            using System;

            public class DataContainer {
                private int _value = 0;

                public int ComputedValue {
                    get {
                        var result = CalculateValue();
                        ValidateResult(result);
                        return result;
                    }
                }

                /// <summary>Calculates the internal value.</summary>
                private int CalculateValue() {
                    return _value * 2;
                }

                /// <summary>Validates the computed result.</summary>
                private void ValidateResult(int result) {
                    if (result < 0)
                        throw new InvalidOperationException(""Negative result"");
                }
            }
        ");

        _mockStalenessDetector
            .Setup(x => x.DetectStaleDocumentation(It.IsAny<ApiSymbol>()))
            .Returns(new StalenessResult { IsStale = false });

        var callSiteCollector = new CallSiteCollector(_mockCallSiteLogger.Object);
        var collector = new SignatureContextCollector(_mockLogger.Object, callSiteCollector, _mockStalenessDetector.Object);
        var symbol = GetApiSymbol(compilation, "ComputedValue");

        // Act
        var context = await collector.CollectContext(symbol, compilation);

        // Assert
        // Property body extracted
        context.ImplementationBody.ShouldNotBeNull();
        context.ImplementationBody.ShouldContain("get");
        context.ImplementationBody.ShouldContain("CalculateValue()");
        context.ImplementationBody.ShouldContain("ValidateResult");

        // Called methods documentation
        context.CalledMethodsDocumentation.Count.ShouldBe(2);
        context.CalledMethodsDocumentation.ShouldContain(doc => doc.MethodName.Contains("CalculateValue"));
        context.CalledMethodsDocumentation.ShouldContain(doc => doc.MethodName.Contains("ValidateResult"));
    }

    [Fact]
    public async Task CollectContext_WithNoCallSites_HandlesGracefully()
    {
        // Arrange
        var compilation = CreateTestCompilation(@"
            using System;

            public class UtilityClass {
                public void UnusedMethod() {
                    var x = 5;
                    Console.WriteLine(x);
                }
            }
        ");

        var callSiteCollector = new CallSiteCollector(_mockCallSiteLogger.Object);
        var collector = new SignatureContextCollector(_mockLogger.Object, callSiteCollector, _mockStalenessDetector.Object);
        var symbol = GetApiSymbol(compilation, "UnusedMethod");

        // Act
        var context = await collector.CollectContext(symbol, compilation);

        // Assert
        context.CallSites.ShouldBeEmpty();
        context.ImplementationBody.ShouldNotBeNull();
        context.CalledMethodsDocumentation.ShouldBeEmpty();
    }

    [Fact]
    public async Task CollectContext_VerifyTokenEstimateAccuracy()
    {
        // Arrange
        var compilation = CreateTestCompilation(@"
            using System;

            public class TestClass {
                public void TestMethod() {
                    var longString = ""This is a moderately long string that will contribute to the token estimate"";
                    Helper1();
                    Helper2();
                }

                /// <summary>Helper method 1 with some documentation content.</summary>
                private void Helper1() { }

                /// <summary>Helper method 2 with additional documentation that increases token count.</summary>
                private void Helper2() { }
            }
        ");

        _mockStalenessDetector
            .Setup(x => x.DetectStaleDocumentation(It.IsAny<ApiSymbol>()))
            .Returns(new StalenessResult { IsStale = false });

        var callSiteCollector = new CallSiteCollector(_mockCallSiteLogger.Object);
        var collector = new SignatureContextCollector(_mockLogger.Object, callSiteCollector, _mockStalenessDetector.Object);
        var symbol = GetApiSymbol(compilation, "TestMethod");

        // Act
        var context = await collector.CollectContext(symbol, compilation);

        // Assert
        context.TokenEstimate.ShouldBeGreaterThan(0);

        // Rough verification: token estimate should correlate with content length
        var totalContentLength = 0;
        totalContentLength += context.ImplementationBody?.Length ?? 0;
        totalContentLength += context.CalledMethodsDocumentation.Sum(doc => doc.XmlDocumentation.Length);

        var expectedTokens = totalContentLength / 4; // Using same heuristic as implementation
        // Allow 50% variance due to other context elements
        context.TokenEstimate.ShouldBeInRange((int)(expectedTokens * 0.5), (int)(expectedTokens * 2));
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
            .FirstOrDefault(s => s?.Name == symbolName);

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
            _ => SymbolType.Method
        };
    }
}
