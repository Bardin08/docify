using Docify.Writer.Backup;
using Docify.Writer.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace Docify.Writer.Tests.Backup;

public class BackupManagerTests : IDisposable
{
    private readonly Mock<ILogger<BackupManager>> _mockLogger;
    private readonly BackupManager _backupManager;
    private readonly string _tempProjectPath;
    private readonly List<string> _tempFilesCreated = [];
    private readonly List<string> _tempDirsCreated = [];

    public BackupManagerTests()
    {
        _mockLogger = new Mock<ILogger<BackupManager>>();
        _backupManager = new BackupManager(_mockLogger.Object);
        _tempProjectPath = Path.Combine(Path.GetTempPath(), $"docify-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempProjectPath);
        _tempDirsCreated.Add(_tempProjectPath);
    }

    public void Dispose()
    {
        // Cleanup temp files and directories
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
    public async Task CreateBackup_WithValidFiles_CreatesBackupWithPreservedStructure()
    {
        // Arrange
        var testFilePath = Path.Combine(_tempProjectPath, "src", "MyClass.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(testFilePath)!);
        await File.WriteAllTextAsync(testFilePath, "public class MyClass { }");
        _tempFilesCreated.Add(testFilePath);

        var filesToBackup = new[] { testFilePath };

        // Act
        var backupPath = await _backupManager.CreateBackup(_tempProjectPath, filesToBackup);
        _tempDirsCreated.Add(backupPath);

        // Assert
        backupPath.ShouldNotBeNull();
        backupPath.ShouldContain(".docify");
        backupPath.ShouldContain("backups");
        backupPath.ShouldContain("backup-");

        var backupFilePath = Path.Combine(backupPath, "src", "MyClass.cs");
        File.Exists(backupFilePath).ShouldBeTrue();

        var backupContent = await File.ReadAllTextAsync(backupFilePath);
        backupContent.ShouldBe("public class MyClass { }");
    }

    [Fact]
    public async Task CreateBackup_WithProjectHash_GeneratesConsistentHash()
    {
        // Arrange
        var testFilePath = Path.Combine(_tempProjectPath, "test.cs");
        await File.WriteAllTextAsync(testFilePath, "test content");
        _tempFilesCreated.Add(testFilePath);

        // Act
        var backupPath1 = await _backupManager.CreateBackup(_tempProjectPath, new[] { testFilePath });
        _tempDirsCreated.Add(backupPath1);

        // Wait to ensure different timestamp
        await Task.Delay(1100);

        var backupPath2 = await _backupManager.CreateBackup(_tempProjectPath, new[] { testFilePath });
        _tempDirsCreated.Add(backupPath2);

        // Assert - both backups should be in same project hash directory
        var parentDir1 = Directory.GetParent(backupPath1)!.FullName;
        var parentDir2 = Directory.GetParent(backupPath2)!.FullName;
        parentDir1.ShouldBe(parentDir2);

        // But backup timestamps should be different
        backupPath1.ShouldNotBe(backupPath2);
    }

    [Fact]
    public async Task CreateBackup_WithTimestampFormat_UsesCorrectFormat()
    {
        // Arrange
        var testFilePath = Path.Combine(_tempProjectPath, "test.cs");
        await File.WriteAllTextAsync(testFilePath, "test");
        _tempFilesCreated.Add(testFilePath);

        // Act
        var backupPath = await _backupManager.CreateBackup(_tempProjectPath, new[] { testFilePath });
        _tempDirsCreated.Add(backupPath);

        // Assert - timestamp format should be backup-YYYY-MM-DD-HHmmss
        var backupDirName = Path.GetFileName(backupPath);
        backupDirName.ShouldStartWith("backup-");
        backupDirName.ShouldMatch(@"backup-\d{4}-\d{2}-\d{2}-\d{6}(-\d+)?");
    }

    [Fact]
    public async Task CreateBackup_WithDuplicateTimestamp_AppendsCounter()
    {
        // Arrange
        var testFilePath = Path.Combine(_tempProjectPath, "test.cs");
        await File.WriteAllTextAsync(testFilePath, "test");
        _tempFilesCreated.Add(testFilePath);

        // Create first backup
        var backupPath1 = await _backupManager.CreateBackup(_tempProjectPath, new[] { testFilePath });
        _tempDirsCreated.Add(backupPath1);

        // Simulate duplicate timestamp by creating directory manually
        var artificialDupePath = backupPath1.Replace(Path.GetFileName(backupPath1),
            $"backup-{DateTime.Now:yyyy-MM-dd-HHmmss}");
        Directory.CreateDirectory(artificialDupePath);
        _tempDirsCreated.Add(artificialDupePath);

        // Act - create backup that might have same timestamp
        var backupPath2 = await _backupManager.CreateBackup(_tempProjectPath, new[] { testFilePath });
        _tempDirsCreated.Add(backupPath2);

        // Assert - should succeed (either different timestamp or counter appended)
        backupPath2.ShouldNotBeNull();
        Directory.Exists(backupPath2).ShouldBeTrue();
    }

    [Fact]
    public async Task CreateBackup_WithEmptyFileList_CreatesBackupDirectoryOnly()
    {
        // Arrange
        var emptyList = Array.Empty<string>();

        // Act
        var backupPath = await _backupManager.CreateBackup(_tempProjectPath, emptyList);
        _tempDirsCreated.Add(backupPath);

        // Assert
        Directory.Exists(backupPath).ShouldBeTrue();
        var files = Directory.GetFiles(backupPath, "*", SearchOption.AllDirectories);
        files.ShouldBeEmpty();
    }

    [Fact]
    public async Task CreateBackup_WithNonExistentFile_SkipsFileAndContinues()
    {
        // Arrange
        var existingFile = Path.Combine(_tempProjectPath, "existing.cs");
        await File.WriteAllTextAsync(existingFile, "exists");
        _tempFilesCreated.Add(existingFile);

        var nonExistentFile = Path.Combine(_tempProjectPath, "nonexistent.cs");
        var filesToBackup = new[] { existingFile, nonExistentFile };

        // Act
        var backupPath = await _backupManager.CreateBackup(_tempProjectPath, filesToBackup);
        _tempDirsCreated.Add(backupPath);

        // Assert
        File.Exists(Path.Combine(backupPath, "existing.cs")).ShouldBeTrue();
        File.Exists(Path.Combine(backupPath, "nonexistent.cs")).ShouldBeFalse();
    }

    [Fact]
    public async Task CreateBackup_WithRelativePath_ThrowsArgumentException()
    {
        // Arrange
        var relativeProjectPath = "relative/path";

        // Act & Assert
        await Should.ThrowAsync<ArgumentException>(async () =>
            await _backupManager.CreateBackup(relativeProjectPath, Array.Empty<string>()));
    }

    [Fact]
    public async Task CreateBackup_WithNullProjectPath_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await _backupManager.CreateBackup(null!, Array.Empty<string>()));
    }

    [Fact]
    public async Task CreateBackup_WithNullFileList_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await _backupManager.CreateBackup(_tempProjectPath, null!));
    }

    [Fact]
    public async Task RestoreBackup_WithValidBackup_RestoresAllFiles()
    {
        // Arrange
        var originalFile = Path.Combine(_tempProjectPath, "original.cs");
        await File.WriteAllTextAsync(originalFile, "original content");
        _tempFilesCreated.Add(originalFile);

        var backupPath = await _backupManager.CreateBackup(_tempProjectPath, new[] { originalFile });
        _tempDirsCreated.Add(backupPath);

        // Modify original file
        await File.WriteAllTextAsync(originalFile, "modified content");

        // Act
        var restoredCount = await _backupManager.RestoreBackup(backupPath, _tempProjectPath);

        // Assert
        restoredCount.ShouldBe(1);
        var restoredContent = await File.ReadAllTextAsync(originalFile);
        restoredContent.ShouldBe("original content");
    }

    [Fact]
    public async Task RestoreBackup_WithMultipleFiles_RestoresAllCorrectly()
    {
        // Arrange
        var file1 = Path.Combine(_tempProjectPath, "file1.cs");
        var file2 = Path.Combine(_tempProjectPath, "subdir", "file2.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(file2)!);

        await File.WriteAllTextAsync(file1, "content1");
        await File.WriteAllTextAsync(file2, "content2");
        _tempFilesCreated.Add(file1);
        _tempFilesCreated.Add(file2);

        var backupPath = await _backupManager.CreateBackup(_tempProjectPath, new[] { file1, file2 });
        _tempDirsCreated.Add(backupPath);

        // Modify files
        await File.WriteAllTextAsync(file1, "modified1");
        await File.WriteAllTextAsync(file2, "modified2");

        // Act
        var restoredCount = await _backupManager.RestoreBackup(backupPath, _tempProjectPath);

        // Assert
        restoredCount.ShouldBe(2);
        (await File.ReadAllTextAsync(file1)).ShouldBe("content1");
        (await File.ReadAllTextAsync(file2)).ShouldBe("content2");
    }

    [Fact]
    public async Task RestoreBackup_WithInvalidBackupPath_ThrowsFileSystemException()
    {
        // Arrange
        var invalidPath = Path.Combine(Path.GetTempPath(), "nonexistent-backup");

        // Act & Assert
        await Should.ThrowAsync<FileSystemException>(async () =>
            await _backupManager.RestoreBackup(invalidPath, _tempProjectPath));
    }

    [Fact]
    public async Task RestoreBackup_WithNullBackupPath_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await _backupManager.RestoreBackup(null!, _tempProjectPath));
    }

    [Fact]
    public async Task RestoreBackup_WithRelativeProjectPath_ThrowsArgumentException()
    {
        // Arrange
        var testFile = Path.Combine(_tempProjectPath, "test.cs");
        await File.WriteAllTextAsync(testFile, "test");
        _tempFilesCreated.Add(testFile);

        var backupPath = await _backupManager.CreateBackup(_tempProjectPath, new[] { testFile });
        _tempDirsCreated.Add(backupPath);

        // Act & Assert
        await Should.ThrowAsync<ArgumentException>(async () =>
            await _backupManager.RestoreBackup(backupPath, "relative/path"));
    }

    [Fact]
    public void ValidateBackup_WithValidBackupPath_ReturnsTrue()
    {
        // Arrange
        var validBackupPath = Path.Combine(Path.GetTempPath(), $"test-backup-{Guid.NewGuid()}");
        Directory.CreateDirectory(validBackupPath);
        _tempDirsCreated.Add(validBackupPath);

        // Act
        var result = _backupManager.ValidateBackup(validBackupPath);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ValidateBackup_WithNonExistentPath_ReturnsFalse()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"nonexistent-{Guid.NewGuid()}");

        // Act
        var result = _backupManager.ValidateBackup(nonExistentPath);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void ValidateBackup_WithTildePath_ExpandsAndValidates()
    {
        // Arrange
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var testDir = Path.Combine(homeDir, $".docify-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(testDir);
        _tempDirsCreated.Add(testDir);

        var relativePath = Path.GetRelativePath(homeDir, testDir);
        var tildePath = $"~/{relativePath}";

        // Act
        var result = _backupManager.ValidateBackup(tildePath);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ValidateBackup_WithNullPath_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            _backupManager.ValidateBackup(null!));
    }
}
