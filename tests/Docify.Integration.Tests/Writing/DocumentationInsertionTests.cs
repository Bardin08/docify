using Docify.Writer.Backup;
using Docify.Writer.Writing;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace Docify.Integration.Tests.Writing;

/// <summary>
/// Integration tests for end-to-end documentation insertion workflow
/// </summary>
public class DocumentationInsertionTests : IDisposable
{
    private readonly DocumentationWriter _writer;
    private readonly string _tempProjectPath;
    private readonly List<string> _tempDirsCreated = [];

    public DocumentationInsertionTests()
    {
        var backupLogger = new Mock<ILogger<BackupManager>>();
        var writerLogger = new Mock<ILogger<DocumentationWriter>>();

        var backupManager = new BackupManager(backupLogger.Object);
        _writer = new DocumentationWriter(backupManager, writerLogger.Object);

        _tempProjectPath = Path.Combine(Path.GetTempPath(), $"docify-integration-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempProjectPath);
        _tempDirsCreated.Add(_tempProjectPath);
    }

    public void Dispose()
    {
        foreach (var dir in _tempDirsCreated)
        {
            try
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
            }
            catch { /* Ignore cleanup errors */ }
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task FullWorkflow_WithRealCSharpFile_InsertsAndMaintainsSyntaxValidity()
    {
        // Arrange - Create realistic C# file
        var testFile = Path.Combine(_tempProjectPath, "Calculator.cs");
        var originalContent = @"using System;

namespace MathLibrary;

public class Calculator
{
    private readonly ILogger _logger;

    public Calculator(ILogger logger)
    {
        _logger = logger;
    }

    public int Add(int a, int b)
    {
        return a + b;
    }

    public int Subtract(int a, int b)
    {
        return a - b;
    }
}";
        await File.WriteAllTextAsync(testFile, originalContent);

        var xmlDoc = @"<summary>
Adds two integers together.
</summary>
<param name=""a"">First number</param>
<param name=""b"">Second number</param>
<returns>Sum of a and b</returns>";

        // Act
        var result = await _writer.InsertDocumentation(testFile, _tempProjectPath, "Add", xmlDoc);

        // Assert
        result.ShouldBeTrue();

        var modifiedContent = await File.ReadAllTextAsync(testFile);

        // Verify documentation was inserted
        modifiedContent.ShouldContain("/// <summary>");
        modifiedContent.ShouldContain("/// Adds two integers together.");
        modifiedContent.ShouldContain("/// <param name=\"a\">First number</param>");
        modifiedContent.ShouldContain("/// <param name=\"b\">Second number</param>");
        modifiedContent.ShouldContain("/// <returns>Sum of a and b</returns>");

        // Verify syntax validity using Roslyn
        var syntaxTree = CSharpSyntaxTree.ParseText(modifiedContent);
        var diagnostics = syntaxTree.GetDiagnostics();
        var errors = diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error).ToList();

        errors.ShouldBeEmpty($"Modified file should have no syntax errors, but found: {string.Join(", ", errors.Select(e => e.GetMessage()))}");

        // Verify other code unchanged
        modifiedContent.ShouldContain("public int Subtract(int a, int b)");
        modifiedContent.ShouldContain("private readonly ILogger _logger;");
    }

    [Fact]
    public async Task InsertionIntoFileWithExistingDocumentation_DoesNotCorrupt()
    {
        // Arrange
        var testFile = Path.Combine(_tempProjectPath, "Service.cs");
        var originalContent = @"namespace Services;

public class UserService
{
    /// <summary>
    /// Gets a user by ID.
    /// </summary>
    public User GetUser(int id)
    {
        return new User();
    }

    public void DeleteUser(int id)
    {
        // Delete logic
    }
}

public class User { }";
        await File.WriteAllTextAsync(testFile, originalContent);

        var xmlDoc = "<summary>\nDeletes a user by ID.\n</summary>\n<param name=\"id\">User ID</param>";

        // Act
        var result = await _writer.InsertDocumentation(testFile, _tempProjectPath, "DeleteUser", xmlDoc);

        // Assert
        result.ShouldBeTrue();

        var modifiedContent = await File.ReadAllTextAsync(testFile);

        // Verify existing documentation preserved
        modifiedContent.ShouldContain("/// Gets a user by ID.");

        // Verify new documentation added
        modifiedContent.ShouldContain("/// Deletes a user by ID.");
        modifiedContent.ShouldContain("/// <param name=\"id\">User ID</param>");

        // Verify syntax validity
        var syntaxTree = CSharpSyntaxTree.ParseText(modifiedContent);
        var errors = syntaxTree.GetDiagnostics()
            .Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
            .ToList();

        errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task InsertionWithVariousFormattingStyles_MaintainsConsistency()
    {
        // Arrange - File with mixed braces style
        var testFile = Path.Combine(_tempProjectPath, "MixedStyle.cs");
        var originalContent = @"namespace Test;

public class MixedStyleClass
{
    public void Method1()
    {
        var x = 1;
    }

    public void Method2() { return; }

    public string Property { get; set; }
}";
        await File.WriteAllTextAsync(testFile, originalContent);

        var xmlDoc1 = "<summary>First method</summary>";
        var xmlDoc2 = "<summary>Second method</summary>";
        var xmlDoc3 = "<summary>String property</summary>";

        // Act
        await _writer.InsertDocumentation(testFile, _tempProjectPath, "Method1", xmlDoc1);
        await _writer.InsertDocumentation(testFile, _tempProjectPath, "Method2", xmlDoc2);
        await _writer.InsertDocumentation(testFile, _tempProjectPath, "Property", xmlDoc3);

        // Assert
        var modifiedContent = await File.ReadAllTextAsync(testFile);

        // Verify all documentation inserted
        modifiedContent.ShouldContain("/// <summary>First method</summary>");
        modifiedContent.ShouldContain("/// <summary>Second method</summary>");
        modifiedContent.ShouldContain("/// <summary>String property</summary>");

        // Verify syntax validity
        var syntaxTree = CSharpSyntaxTree.ParseText(modifiedContent);
        var errors = syntaxTree.GetDiagnostics()
            .Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
            .ToList();

        errors.ShouldBeEmpty();

        // Verify formatting preserved
        modifiedContent.ShouldContain("public void Method2() { return; }");
    }

    [Fact]
    public async Task BackupCreation_IntegrationWithRealBackupManager()
    {
        // Arrange
        var testFile = Path.Combine(_tempProjectPath, "BackupTest.cs");
        await File.WriteAllTextAsync(testFile, "namespace Test;\n\npublic class TestClass\n{\n    public void MyMethod() { }\n}");

        var xmlDoc = "<summary>Test</summary>";

        // Act
        var result = await _writer.InsertDocumentation(testFile, _tempProjectPath, "MyMethod", xmlDoc);

        // Assert
        result.ShouldBeTrue();

        // Verify backup was created
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var backupsDir = Path.Combine(homeDir, ".docify", "backups");

        Directory.Exists(backupsDir).ShouldBeTrue();

        // Find backup directory for this project (should contain project hash)
        var projectDirs = Directory.GetDirectories(backupsDir);
        projectDirs.ShouldNotBeEmpty();

        // Cleanup backup directories created during test
        foreach (var dir in projectDirs)
        {
            try
            {
                _tempDirsCreated.Add(dir);
            }
            catch { /* Best effort cleanup */ }
        }
    }
}
