using Docify.Writer.Exceptions;
using Docify.Writer.Interfaces;
using Docify.Writer.Writing;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace Docify.Writer.Tests.Writing;

public class DocumentationWriterTests : IDisposable
{
    private readonly Mock<IBackupManager> _mockBackupManager;
    private readonly Mock<ILogger<DocumentationWriter>> _mockLogger;
    private readonly DocumentationWriter _writer;
    private readonly string _tempProjectPath;
    private readonly List<string> _tempFilesCreated = [];
    private readonly List<string> _tempDirsCreated = [];

    public DocumentationWriterTests()
    {
        _mockBackupManager = new Mock<IBackupManager>();
        _mockLogger = new Mock<ILogger<DocumentationWriter>>();
        _writer = new DocumentationWriter(_mockBackupManager.Object, _mockLogger.Object);

        _tempProjectPath = Path.Combine(Path.GetTempPath(), $"docify-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempProjectPath);
        _tempDirsCreated.Add(_tempProjectPath);

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
    public async Task InsertDocumentation_WithValidMethod_InsertsAtCorrectPosition()
    {
        // Arrange
        var testFile = Path.Combine(_tempProjectPath, "Test.cs");
        await File.WriteAllTextAsync(testFile, @"namespace Test;

public class TestClass
{
    public void MyMethod() { }
}");
        _tempFilesCreated.Add(testFile);

        var xmlDoc = "<summary>Test documentation</summary>";

        // Act
        var result = await _writer.InsertDocumentation(testFile, _tempProjectPath, "MyMethod", xmlDoc);

        // Assert
        result.ShouldBeTrue();
        var modifiedContent = await File.ReadAllTextAsync(testFile);
        modifiedContent.ShouldContain("/// <summary>Test documentation</summary>");
        modifiedContent.ShouldContain("public void MyMethod()");
    }

    [Fact]
    public async Task InsertDocumentation_WithClass_InsertsAboveClassDeclaration()
    {
        // Arrange
        var testFile = Path.Combine(_tempProjectPath, "Test.cs");
        await File.WriteAllTextAsync(testFile, @"namespace Test;

public class MyClass
{
}");
        _tempFilesCreated.Add(testFile);

        var xmlDoc = "<summary>My class documentation</summary>";

        // Act
        var result = await _writer.InsertDocumentation(testFile, _tempProjectPath, "MyClass", xmlDoc);

        // Assert
        result.ShouldBeTrue();
        var modifiedContent = await File.ReadAllTextAsync(testFile);
        modifiedContent.ShouldContain("/// <summary>My class documentation</summary>");
        modifiedContent.ShouldContain("public class MyClass");

        var lines = modifiedContent.Split('\n');
        var docLine = Array.FindIndex(lines, l => l.Contains("/// <summary>"));
        var classLine = Array.FindIndex(lines, l => l.Contains("public class MyClass"));
        docLine.ShouldBeLessThan(classLine);
    }

    [Fact]
    public async Task InsertDocumentation_WithProperty_InsertsCorrectly()
    {
        // Arrange
        var testFile = Path.Combine(_tempProjectPath, "Test.cs");
        await File.WriteAllTextAsync(testFile, @"namespace Test;

public class TestClass
{
    public string MyProperty { get; set; }
}");
        _tempFilesCreated.Add(testFile);

        var xmlDoc = "<summary>Property documentation</summary>";

        // Act
        var result = await _writer.InsertDocumentation(testFile, _tempProjectPath, "MyProperty", xmlDoc);

        // Assert
        result.ShouldBeTrue();
        var modifiedContent = await File.ReadAllTextAsync(testFile);
        modifiedContent.ShouldContain("/// <summary>Property documentation</summary>");
        modifiedContent.ShouldContain("public string MyProperty");
    }

    [Fact]
    public async Task InsertDocumentation_WithTabIndentation_PreservesTabsIn()
    {
        // Arrange
        var testFile = Path.Combine(_tempProjectPath, "Test.cs");
        await File.WriteAllTextAsync(testFile, "namespace Test;\n\npublic class TestClass\n{\n\tpublic void MyMethod() { }\n}");
        _tempFilesCreated.Add(testFile);

        var xmlDoc = "<summary>Test</summary>";

        // Act
        var result = await _writer.InsertDocumentation(testFile, _tempProjectPath, "MyMethod", xmlDoc);

        // Assert
        result.ShouldBeTrue();
        var modifiedContent = await File.ReadAllTextAsync(testFile);
        modifiedContent.ShouldContain("\t/// <summary>Test</summary>");
    }

    [Fact]
    public async Task InsertDocumentation_With4SpaceIndentation_Preserves4Spaces()
    {
        // Arrange
        var testFile = Path.Combine(_tempProjectPath, "Test.cs");
        await File.WriteAllTextAsync(testFile, "namespace Test;\n\npublic class TestClass\n{\n    public void MyMethod() { }\n}");
        _tempFilesCreated.Add(testFile);

        var xmlDoc = "<summary>Test</summary>";

        // Act
        var result = await _writer.InsertDocumentation(testFile, _tempProjectPath, "MyMethod", xmlDoc);

        // Assert
        result.ShouldBeTrue();
        var modifiedContent = await File.ReadAllTextAsync(testFile);
        modifiedContent.ShouldContain("    /// <summary>Test</summary>");
    }

    [Fact]
    public async Task InsertDocumentation_WithCRLFLineEndings_PreservesCRLF()
    {
        // Arrange
        var testFile = Path.Combine(_tempProjectPath, "Test.cs");
        await File.WriteAllTextAsync(testFile, "namespace Test;\r\n\r\npublic class TestClass\r\n{\r\n    public void MyMethod() { }\r\n}");
        _tempFilesCreated.Add(testFile);

        var xmlDoc = "<summary>Test</summary>";

        // Act
        var result = await _writer.InsertDocumentation(testFile, _tempProjectPath, "MyMethod", xmlDoc);

        // Assert
        result.ShouldBeTrue();
        var modifiedContent = await File.ReadAllTextAsync(testFile);
        modifiedContent.ShouldContain("\r\n");
        modifiedContent.ShouldContain("/// <summary>Test</summary>\r\n");
    }

    [Fact]
    public async Task InsertDocumentation_WithLFLineEndings_PreservesLF()
    {
        // Arrange
        var testFile = Path.Combine(_tempProjectPath, "Test.cs");
        await File.WriteAllTextAsync(testFile, "namespace Test;\n\npublic class TestClass\n{\n    public void MyMethod() { }\n}");
        _tempFilesCreated.Add(testFile);

        var xmlDoc = "<summary>Test</summary>";

        // Act
        var result = await _writer.InsertDocumentation(testFile, _tempProjectPath, "MyMethod", xmlDoc);

        // Assert
        result.ShouldBeTrue();
        var modifiedContent = await File.ReadAllTextAsync(testFile);
        modifiedContent.ShouldNotContain("\r\n");
        modifiedContent.ShouldContain("/// <summary>Test</summary>\n");
    }

    [Fact]
    public async Task InsertDocumentation_WithMultiLineXml_FormatsCorrectly()
    {
        // Arrange
        var testFile = Path.Combine(_tempProjectPath, "Test.cs");
        await File.WriteAllTextAsync(testFile, "namespace Test;\n\npublic class TestClass\n{\n    public void MyMethod() { }\n}");
        _tempFilesCreated.Add(testFile);

        var xmlDoc = "<summary>\nFirst line\nSecond line\n</summary>";

        // Act
        var result = await _writer.InsertDocumentation(testFile, _tempProjectPath, "MyMethod", xmlDoc);

        // Assert
        result.ShouldBeTrue();
        var modifiedContent = await File.ReadAllTextAsync(testFile);
        modifiedContent.ShouldContain("    /// <summary>");
        modifiedContent.ShouldContain("    /// First line");
        modifiedContent.ShouldContain("    /// Second line");
        modifiedContent.ShouldContain("    /// </summary>");
    }

    [Fact]
    public async Task InsertDocumentation_CallsBackupManager()
    {
        // Arrange
        var testFile = Path.Combine(_tempProjectPath, "Test.cs");
        await File.WriteAllTextAsync(testFile, "namespace Test;\n\npublic class TestClass\n{\n    public void MyMethod() { }\n}");
        _tempFilesCreated.Add(testFile);

        var xmlDoc = "<summary>Test</summary>";

        // Act
        await _writer.InsertDocumentation(testFile, _tempProjectPath, "MyMethod", xmlDoc);

        // Assert
        _mockBackupManager.Verify(
            bm => bm.CreateBackup(_tempProjectPath, It.Is<IReadOnlyList<string>>(list => list.Contains(testFile))),
            Times.Once);
    }

    [Fact]
    public async Task InsertDocumentation_WithInvalidApiIdentifier_ThrowsAnalysisException()
    {
        // Arrange
        var testFile = Path.Combine(_tempProjectPath, "Test.cs");
        await File.WriteAllTextAsync(testFile, "namespace Test;\n\npublic class TestClass\n{\n}");
        _tempFilesCreated.Add(testFile);

        var xmlDoc = "<summary>Test</summary>";

        // Act & Assert
        await Should.ThrowAsync<AnalysisException>(async () =>
            await _writer.InsertDocumentation(testFile, _tempProjectPath, "NonExistentMethod", xmlDoc));
    }

    [Fact]
    public async Task InsertDocumentation_WithNonExistentFile_ThrowsFileSystemException()
    {
        // Arrange
        var nonExistentFile = Path.Combine(_tempProjectPath, "NonExistent.cs");
        var xmlDoc = "<summary>Test</summary>";

        // Act & Assert
        await Should.ThrowAsync<FileSystemException>(async () =>
            await _writer.InsertDocumentation(nonExistentFile, _tempProjectPath, "MyMethod", xmlDoc));
    }

    [Fact]
    public async Task InsertDocumentation_WithRelativeFilePath_ThrowsArgumentException()
    {
        // Arrange
        var relativeFile = "relative/path/Test.cs";
        var xmlDoc = "<summary>Test</summary>";

        // Act & Assert
        await Should.ThrowAsync<ArgumentException>(async () =>
            await _writer.InsertDocumentation(relativeFile, _tempProjectPath, "MyMethod", xmlDoc));
    }

    [Fact]
    public async Task InsertDocumentation_WithRelativeProjectPath_ThrowsArgumentException()
    {
        // Arrange
        var testFile = Path.Combine(_tempProjectPath, "Test.cs");
        await File.WriteAllTextAsync(testFile, "namespace Test;\n\npublic class TestClass\n{\n    public void MyMethod() { }\n}");
        _tempFilesCreated.Add(testFile);

        var xmlDoc = "<summary>Test</summary>";

        // Act & Assert
        await Should.ThrowAsync<ArgumentException>(async () =>
            await _writer.InsertDocumentation(testFile, "relative/path", "MyMethod", xmlDoc));
    }

    [Fact]
    public async Task InsertDocumentation_DoesNotModifyOtherCode()
    {
        // Arrange
        var testFile = Path.Combine(_tempProjectPath, "Test.cs");
        var originalContent = @"namespace Test;

// Existing comment
public class TestClass
{
    private int _field;

    public void MyMethod()
    {
        var x = 42;
    }

    public void OtherMethod() { }
}";
        await File.WriteAllTextAsync(testFile, originalContent);
        _tempFilesCreated.Add(testFile);

        var xmlDoc = "<summary>Test</summary>";

        // Act
        var result = await _writer.InsertDocumentation(testFile, _tempProjectPath, "MyMethod", xmlDoc);

        // Assert
        result.ShouldBeTrue();
        var modifiedContent = await File.ReadAllTextAsync(testFile);

        // Verify other elements are preserved
        modifiedContent.ShouldContain("// Existing comment");
        modifiedContent.ShouldContain("private int _field;");
        modifiedContent.ShouldContain("var x = 42;");
        modifiedContent.ShouldContain("public void OtherMethod()");
    }

    [Fact]
    public async Task InsertDocumentation_WithInterface_InsertsCorrectly()
    {
        // Arrange
        var testFile = Path.Combine(_tempProjectPath, "Test.cs");
        await File.WriteAllTextAsync(testFile, "namespace Test;\n\npublic interface IMyInterface\n{\n}");
        _tempFilesCreated.Add(testFile);

        var xmlDoc = "<summary>Interface documentation</summary>";

        // Act
        var result = await _writer.InsertDocumentation(testFile, _tempProjectPath, "IMyInterface", xmlDoc);

        // Assert
        result.ShouldBeTrue();
        var modifiedContent = await File.ReadAllTextAsync(testFile);
        modifiedContent.ShouldContain("/// <summary>Interface documentation</summary>");
        modifiedContent.ShouldContain("public interface IMyInterface");
    }

    [Fact]
    public async Task InsertDocumentation_UsesAtomicFileWrite()
    {
        // Arrange
        var testFile = Path.Combine(_tempProjectPath, "Test.cs");
        await File.WriteAllTextAsync(testFile, "namespace Test;\n\npublic class TestClass\n{\n    public void MyMethod() { }\n}");
        _tempFilesCreated.Add(testFile);

        var xmlDoc = "<summary>Test</summary>";

        var tempFile = $"{testFile}.tmp";

        // Act
        await _writer.InsertDocumentation(testFile, _tempProjectPath, "MyMethod", xmlDoc);

        // Assert - temp file should not exist after successful write
        File.Exists(tempFile).ShouldBeFalse();
    }
}
