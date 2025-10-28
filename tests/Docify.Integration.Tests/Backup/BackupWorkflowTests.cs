using Docify.Writer.Backup;
using Docify.Writer.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace Docify.Integration.Tests.Backup;

/// <summary>
/// Integration tests for end-to-end backup workflow scenarios
/// </summary>
public class BackupWorkflowTests : IDisposable
{
    private readonly BackupManager _backupManager;
    private readonly string _tempProjectPath;
    private readonly List<string> _tempDirsCreated = [];

    public BackupWorkflowTests()
    {
        var mockLogger = new Mock<ILogger<BackupManager>>();
        _tempProjectPath = Path.Combine(Path.GetTempPath(), $"docify-integration-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempProjectPath);
        _tempDirsCreated.Add(_tempProjectPath);

        // Use temp directory for backups in tests to avoid permission issues
        var tempBackupBase = Path.Combine(Path.GetTempPath(), $"docify-backups-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempBackupBase);
        _tempDirsCreated.Add(tempBackupBase);

        _backupManager = new BackupManager(mockLogger.Object, tempBackupBase);
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
    public async Task FullBackupAndRestoreCycle_WithRealFiles_PreservesContent()
    {
        // Arrange - Create a realistic project structure
        var srcDir = Path.Combine(_tempProjectPath, "src", "MyLib");
        Directory.CreateDirectory(srcDir);

        var file1 = Path.Combine(srcDir, "Class1.cs");
        var file2 = Path.Combine(srcDir, "Class2.cs");
        var testDir = Path.Combine(_tempProjectPath, "tests", "MyLib.Tests");
        Directory.CreateDirectory(testDir);
        var file3 = Path.Combine(testDir, "Class1Tests.cs");

        var originalContent1 = "namespace MyLib;\n\npublic class Class1 { }";
        var originalContent2 = "namespace MyLib;\n\npublic class Class2 { }";
        var originalContent3 = "namespace MyLib.Tests;\n\npublic class Class1Tests { }";

        await File.WriteAllTextAsync(file1, originalContent1);
        await File.WriteAllTextAsync(file2, originalContent2);
        await File.WriteAllTextAsync(file3, originalContent3);

        var filesToBackup = new[] { file1, file2, file3 };

        // Act - Create backup
        var backupPath = await _backupManager.CreateBackup(_tempProjectPath, filesToBackup);
        _tempDirsCreated.Add(backupPath);

        // Verify backup was created
        backupPath.ShouldNotBeNull();
        Directory.Exists(backupPath).ShouldBeTrue();

        // Modify original files (simulate documentation addition)
        await File.WriteAllTextAsync(file1, "/// <summary>Modified</summary>\n" + originalContent1);
        await File.WriteAllTextAsync(file2, "/// <summary>Modified</summary>\n" + originalContent2);
        await File.WriteAllTextAsync(file3, "/// <summary>Modified</summary>\n" + originalContent3);

        // Act - Restore from backup
        var restoredCount = await _backupManager.RestoreBackup(backupPath, _tempProjectPath);

        // Assert - Verify restoration
        restoredCount.ShouldBe(3);

        var restored1 = await File.ReadAllTextAsync(file1);
        var restored2 = await File.ReadAllTextAsync(file2);
        var restored3 = await File.ReadAllTextAsync(file3);

        restored1.ShouldBe(originalContent1);
        restored2.ShouldBe(originalContent2);
        restored3.ShouldBe(originalContent3);
    }

    [Fact]
    public async Task MultipleBackups_FromSameProject_CreatesSeparateBackupDirs()
    {
        // Arrange
        var testFile = Path.Combine(_tempProjectPath, "test.cs");
        await File.WriteAllTextAsync(testFile, "version1");

        // Act - Create multiple backups
        var backup1 = await _backupManager.CreateBackup(_tempProjectPath, new[] { testFile });
        _tempDirsCreated.Add(backup1);

        await Task.Delay(1100); // Ensure different timestamp

        await File.WriteAllTextAsync(testFile, "version2");
        var backup2 = await _backupManager.CreateBackup(_tempProjectPath, new[] { testFile });
        _tempDirsCreated.Add(backup2);

        await Task.Delay(1100);

        await File.WriteAllTextAsync(testFile, "version3");
        var backup3 = await _backupManager.CreateBackup(_tempProjectPath, new[] { testFile });
        _tempDirsCreated.Add(backup3);

        // Assert - All backups should exist with different timestamps
        backup1.ShouldNotBe(backup2);
        backup2.ShouldNotBe(backup3);
        backup1.ShouldNotBe(backup3);

        Directory.Exists(backup1).ShouldBeTrue();
        Directory.Exists(backup2).ShouldBeTrue();
        Directory.Exists(backup3).ShouldBeTrue();

        // Verify each backup contains the correct version
        var content1 = await File.ReadAllTextAsync(Path.Combine(backup1, "test.cs"));
        var content2 = await File.ReadAllTextAsync(Path.Combine(backup2, "test.cs"));
        var content3 = await File.ReadAllTextAsync(Path.Combine(backup3, "test.cs"));

        content1.ShouldBe("version1");
        content2.ShouldBe("version2");
        content3.ShouldBe("version3");
    }

    [Fact]
    public async Task BackupWithDeepNestedStructure_PreservesHierarchy()
    {
        // Arrange - Create deep directory structure
        var deepPath = Path.Combine(_tempProjectPath, "level1", "level2", "level3", "level4");
        Directory.CreateDirectory(deepPath);

        var deepFile = Path.Combine(deepPath, "DeepClass.cs");
        var deepContent = "namespace Deep.Nested.Namespace;\n\npublic class DeepClass { }";
        await File.WriteAllTextAsync(deepFile, deepContent);

        // Act
        var backupPath = await _backupManager.CreateBackup(_tempProjectPath, new[] { deepFile });
        _tempDirsCreated.Add(backupPath);

        // Assert - Verify directory structure is preserved
        var backupFilePath = Path.Combine(backupPath, "level1", "level2", "level3", "level4", "DeepClass.cs");
        File.Exists(backupFilePath).ShouldBeTrue();

        var backedUpContent = await File.ReadAllTextAsync(backupFilePath);
        backedUpContent.ShouldBe(deepContent);
    }

    [Fact]
    public async Task RestoreBackup_ToNonExistentDirectory_CreatesDirectories()
    {
        // Arrange - Create backup
        var file = Path.Combine(_tempProjectPath, "subdir", "file.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        await File.WriteAllTextAsync(file, "original");

        var backupPath = await _backupManager.CreateBackup(_tempProjectPath, new[] { file });
        _tempDirsCreated.Add(backupPath);

        // Delete the subdirectory
        Directory.Delete(Path.GetDirectoryName(file)!, recursive: true);

        // Act - Restore should recreate directories
        var restoredCount = await _backupManager.RestoreBackup(backupPath, _tempProjectPath);

        // Assert
        restoredCount.ShouldBe(1);
        File.Exists(file).ShouldBeTrue();
        (await File.ReadAllTextAsync(file)).ShouldBe("original");
    }

    [Fact]
    public async Task BackupWithSpecialCharactersInPath_HandlesCorrectly()
    {
        // Arrange - Create files with special characters (valid across platforms)
        var specialDir = Path.Combine(_tempProjectPath, "dir_with-special.chars");
        Directory.CreateDirectory(specialDir);

        var specialFile = Path.Combine(specialDir, "file_name-with.special@chars.cs");
        await File.WriteAllTextAsync(specialFile, "content");

        // Act
        var backupPath = await _backupManager.CreateBackup(_tempProjectPath, new[] { specialFile });
        _tempDirsCreated.Add(backupPath);

        // Assert
        var backupFilePath = Path.Combine(backupPath, "dir_with-special.chars", "file_name-with.special@chars.cs");
        File.Exists(backupFilePath).ShouldBeTrue();
        (await File.ReadAllTextAsync(backupFilePath)).ShouldBe("content");
    }

    [Fact]
    public async Task LargeFileBackup_HandlesEfficiently()
    {
        // Arrange - Create a larger file (1MB)
        var largeFile = Path.Combine(_tempProjectPath, "LargeFile.cs");
        var largeContent = new string('x', 1024 * 1024); // 1MB of 'x'
        await File.WriteAllTextAsync(largeFile, largeContent);

        // Act
        var backupPath = await _backupManager.CreateBackup(_tempProjectPath, new[] { largeFile });
        _tempDirsCreated.Add(backupPath);

        // Assert
        var backupFilePath = Path.Combine(backupPath, "LargeFile.cs");
        File.Exists(backupFilePath).ShouldBeTrue();

        var fileInfo = new FileInfo(backupFilePath);
        fileInfo.Length.ShouldBeGreaterThan(1024 * 1024 - 100); // Allow for encoding differences
    }

    [Fact]
    public async Task BackupAndRestore_WithReadOnlyFiles_HandlesCorrectly()
    {
        // Arrange
        var file = Path.Combine(_tempProjectPath, "readonly.cs");
        await File.WriteAllTextAsync(file, "original");

        // Make file read-only
        var fileInfo = new FileInfo(file);
        fileInfo.IsReadOnly = true;

        try
        {
            // Act - Backup read-only file
            var backupPath = await _backupManager.CreateBackup(_tempProjectPath, new[] { file });
            _tempDirsCreated.Add(backupPath);

            // Verify backup was created
            File.Exists(Path.Combine(backupPath, "readonly.cs")).ShouldBeTrue();

            // Make writable to modify for restore test
            fileInfo.IsReadOnly = false;
            await File.WriteAllTextAsync(file, "modified");

            // Restore should overwrite even if original was read-only
            var restoredCount = await _backupManager.RestoreBackup(backupPath, _tempProjectPath);
            restoredCount.ShouldBe(1);

            (await File.ReadAllTextAsync(file)).ShouldBe("original");
        }
        finally
        {
            // Cleanup - remove read-only attribute
            if (File.Exists(file))
            {
                fileInfo.IsReadOnly = false;
            }
        }
    }

    [Fact]
    public async Task BackupCleanupScenario_OldBackupsCanBeDeleted()
    {
        // Arrange - Create multiple backups to simulate backup accumulation
        var testFile = Path.Combine(_tempProjectPath, "test.cs");
        await File.WriteAllTextAsync(testFile, "content");

        var backups = new List<string>();
        for (int i = 0; i < 5; i++)
        {
            var backup = await _backupManager.CreateBackup(_tempProjectPath, new[] { testFile });
            backups.Add(backup);
            _tempDirsCreated.Add(backup);
            await Task.Delay(100); // Ensure different timestamps
        }

        // Act - Simulate cleanup of old backups (delete first 3)
        for (int i = 0; i < 3; i++)
        {
            Directory.Delete(backups[i], recursive: true);
        }

        // Assert - Newest 2 backups should still exist
        Directory.Exists(backups[3]).ShouldBeTrue();
        Directory.Exists(backups[4]).ShouldBeTrue();

        // And should still be valid for restore
        _backupManager.ValidateBackup(backups[3]).ShouldBeTrue();
        _backupManager.ValidateBackup(backups[4]).ShouldBeTrue();
    }
}
