using Docify.Writer.Interfaces;
using Docify.Writer.Writing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace Docify.Integration.Tests.Writing;

/// <summary>
/// Integration tests for formatting preservation during documentation insertion
/// </summary>
public class FormattingPreservationTests : IDisposable
{
    private readonly Mock<IBackupManager> _mockBackupManager;
    private readonly Mock<ILogger<DocumentationWriter>> _mockLogger;
    private readonly DocumentationWriter _writer;
    private readonly string _tempProjectPath;
    private readonly List<string> _tempFilesCreated = [];

    public FormattingPreservationTests()
    {
        _mockBackupManager = new Mock<IBackupManager>();
        _mockLogger = new Mock<ILogger<DocumentationWriter>>();
        _writer = new DocumentationWriter(_mockBackupManager.Object, _mockLogger.Object);

        _tempProjectPath = Path.Combine(Path.GetTempPath(), $"docify-integration-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempProjectPath);

        // Setup default backup manager behavior
        _mockBackupManager.Setup(bm => bm.CreateBackup(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>()))
            .ReturnsAsync("/backup/path");
    }

    public void Dispose()
    {
        foreach (var file in _tempFilesCreated)
        {
            try
            {
                if (File.Exists(file)) File.Delete(file);
            }
            catch { /* Ignore cleanup errors */ }
        }

        try
        {
            if (Directory.Exists(_tempProjectPath)) Directory.Delete(_tempProjectPath, recursive: true);
        }
        catch { /* Ignore cleanup errors */ }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task InsertDocumentation_WithTabIndentedFile_PreservesTabsAndBlankLines()
    {
        // Arrange
        var testFile = Path.Combine(_tempProjectPath, "Test.cs");
        var content = "public class Test\n{\n\tpublic void Method1() { }\n\n\tpublic void Method2() { }\n}";
        await File.WriteAllTextAsync(testFile, content);
        _tempFilesCreated.Add(testFile);

        var xmlDoc = "<summary>Test documentation</summary>";

        // Act
        var result = await _writer.InsertDocumentation(testFile, _tempProjectPath, "Method1", xmlDoc);

        // Assert
        result.ShouldBeTrue();

        var modifiedContent = await File.ReadAllTextAsync(testFile);

        // Verify tab indentation is preserved
        modifiedContent.ShouldContain("\t/// <summary>Test documentation</summary>");

        // Verify blank line between methods is preserved
        modifiedContent.ShouldContain("Method1() { }\n\n\tpublic void Method2");

        // Verify no syntax errors
        var syntaxTree = CSharpSyntaxTree.ParseText(modifiedContent);
        var errors = syntaxTree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error);
        errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task InsertDocumentation_With2SpaceIndentation_Preserves2Spaces()
    {
        // Arrange
        var testFile = Path.Combine(_tempProjectPath, "Test.cs");
        var content = "public class Test\n{\n  public void Method1() { }\n\n  public void Method2() { }\n}";
        await File.WriteAllTextAsync(testFile, content);
        _tempFilesCreated.Add(testFile);

        var xmlDoc = "<summary>Test documentation</summary>";

        // Act
        var result = await _writer.InsertDocumentation(testFile, _tempProjectPath, "Method1", xmlDoc);

        // Assert
        result.ShouldBeTrue();

        var modifiedContent = await File.ReadAllTextAsync(testFile);

        // Verify 2-space indentation is preserved
        modifiedContent.ShouldContain("  /// <summary>Test documentation</summary>");

        // Verify blank line is preserved
        modifiedContent.ShouldContain("Method1() { }\n\n  public void Method2");

        // Verify no syntax errors
        var syntaxTree = CSharpSyntaxTree.ParseText(modifiedContent);
        var errors = syntaxTree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error);
        errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task InsertDocumentation_With4SpaceIndentation_Preserves4Spaces()
    {
        // Arrange
        var testFile = Path.Combine(_tempProjectPath, "Test.cs");
        var content = "public class Test\n{\n    public void Method1() { }\n\n    public void Method2() { }\n}";
        await File.WriteAllTextAsync(testFile, content);
        _tempFilesCreated.Add(testFile);

        var xmlDoc = "<summary>Test documentation</summary>";

        // Act
        var result = await _writer.InsertDocumentation(testFile, _tempProjectPath, "Method1", xmlDoc);

        // Assert
        result.ShouldBeTrue();

        var modifiedContent = await File.ReadAllTextAsync(testFile);

        // Verify 4-space indentation is preserved
        modifiedContent.ShouldContain("    /// <summary>Test documentation</summary>");

        // Verify blank line is preserved
        modifiedContent.ShouldContain("Method1() { }\n\n    public void Method2");

        // Verify no syntax errors
        var syntaxTree = CSharpSyntaxTree.ParseText(modifiedContent);
        var errors = syntaxTree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error);
        errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task InsertDocumentation_PreservesBlankLinesBetweenMethods()
    {
        // Arrange
        var testFile = Path.Combine(_tempProjectPath, "Test.cs");
        var content = @"namespace Test;

public class MyClass
{
    public void Method1() { }

    public void Method2() { }


    public void Method3() { }
}";
        await File.WriteAllTextAsync(testFile, content);
        _tempFilesCreated.Add(testFile);

        var xmlDoc = "<summary>Method documentation</summary>";

        // Act - Add documentation to Method2
        var result = await _writer.InsertDocumentation(testFile, _tempProjectPath, "Method2", xmlDoc);

        // Assert
        result.ShouldBeTrue();

        var modifiedContent = await File.ReadAllTextAsync(testFile);

        // Verify documentation was inserted before Method2
        modifiedContent.ShouldContain("/// <summary>Method documentation</summary>");
        modifiedContent.ShouldContain("public void Method2()");

        // Verify blank lines are maintained in structure
        var lines = modifiedContent.Split('\n');
        var blankLineCount = lines.Count(l => string.IsNullOrWhiteSpace(l));
        blankLineCount.ShouldBeGreaterThan(2); // At least namespace blank, + method blanks

        // Verify no syntax errors
        var syntaxTree = CSharpSyntaxTree.ParseText(modifiedContent);
        var errors = syntaxTree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error);
        errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task InsertDocumentation_PreservesBlankLinesBetweenProperties()
    {
        // Arrange
        var testFile = Path.Combine(_tempProjectPath, "Test.cs");
        var content = @"public class Test
{
    public int Property1 { get; set; }

    public string Property2 { get; set; }
}";
        await File.WriteAllTextAsync(testFile, content);
        _tempFilesCreated.Add(testFile);

        var xmlDoc = "<summary>Property documentation</summary>";

        // Act
        var result = await _writer.InsertDocumentation(testFile, _tempProjectPath, "Property2", xmlDoc);

        // Assert
        result.ShouldBeTrue();

        var modifiedContent = await File.ReadAllTextAsync(testFile);

        // Verify documentation was inserted before Property2
        modifiedContent.ShouldContain("/// <summary>Property documentation</summary>");
        modifiedContent.ShouldContain("public string Property2");

        // Verify blank lines exist in the structure
        var lines = modifiedContent.Split('\n');
        var blankLineCount = lines.Count(l => string.IsNullOrWhiteSpace(l));
        blankLineCount.ShouldBeGreaterThan(0); // At least one blank line

        // Verify no syntax errors
        var syntaxTree = CSharpSyntaxTree.ParseText(modifiedContent);
        var errors = syntaxTree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error);
        errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task InsertDocumentation_NoReformatting_ExistingCodeStructure()
    {
        // Arrange
        var testFile = Path.Combine(_tempProjectPath, "Test.cs");
        var originalContent = @"namespace Test;

// Important comment
public class MyClass
{
    private readonly int _field = 42;

    public void Method1()
    {
        var x = 10;
        var y = 20;
        Console.WriteLine(x + y);
    }

    public void Method2() { }
}";
        await File.WriteAllTextAsync(testFile, originalContent);
        _tempFilesCreated.Add(testFile);

        var xmlDoc = "<summary>Method documentation</summary>";

        // Act
        var result = await _writer.InsertDocumentation(testFile, _tempProjectPath, "Method1", xmlDoc);

        // Assert
        result.ShouldBeTrue();

        var modifiedContent = await File.ReadAllTextAsync(testFile);

        // Verify comment is preserved
        modifiedContent.ShouldContain("// Important comment");

        // Verify field initialization is preserved
        modifiedContent.ShouldContain("private readonly int _field = 42;");

        // Verify method body is preserved exactly
        modifiedContent.ShouldContain("var x = 10;");
        modifiedContent.ShouldContain("var y = 20;");
        modifiedContent.ShouldContain("Console.WriteLine(x + y);");

        // Verify Method2 is unchanged
        modifiedContent.ShouldContain("public void Method2() { }");

        // Verify no syntax errors
        var syntaxTree = CSharpSyntaxTree.ParseText(modifiedContent);
        var errors = syntaxTree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error);
        errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task InsertDocumentation_WithCRLFLineEndings_PreservesCRLF()
    {
        // Arrange
        var testFile = Path.Combine(_tempProjectPath, "Test.cs");
        var content = "public class Test\r\n{\r\n    public void Method() { }\r\n}";
        await File.WriteAllTextAsync(testFile, content);
        _tempFilesCreated.Add(testFile);

        var xmlDoc = "<summary>Test documentation</summary>";

        // Act
        var result = await _writer.InsertDocumentation(testFile, _tempProjectPath, "Method", xmlDoc);

        // Assert
        result.ShouldBeTrue();

        var modifiedContent = await File.ReadAllTextAsync(testFile);

        // Verify CRLF is preserved (should contain \r\n)
        modifiedContent.ShouldContain("\r\n");

        // Count CRLF vs LF
        int crlfCount = modifiedContent.Split("\r\n").Length - 1;
        int lfCount = modifiedContent.Split("\n").Length - 1;

        // CRLF should be the dominant line ending
        crlfCount.ShouldBeGreaterThan(0);
        crlfCount.ShouldBe(lfCount); // All \n should be part of \r\n

        // Verify no syntax errors
        var syntaxTree = CSharpSyntaxTree.ParseText(modifiedContent);
        var errors = syntaxTree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error);
        errors.ShouldBeEmpty();
    }
}
