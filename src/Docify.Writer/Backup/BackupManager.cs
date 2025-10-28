using System.Security.Cryptography;
using System.Text;
using Docify.Writer.Exceptions;
using Docify.Writer.Interfaces;
using Microsoft.Extensions.Logging;

namespace Docify.Writer.Backup;

/// <summary>
/// Manages backup and restoration of source files before modification
/// </summary>
public class BackupManager : IBackupManager
{
    private readonly ILogger<BackupManager> _logger;
    private readonly string? _baseBackupPath;

    public BackupManager(ILogger<BackupManager> logger, string? baseBackupPath = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        _baseBackupPath = baseBackupPath;
    }

    /// <inheritdoc />
    public async Task<string> CreateBackup(string projectPath, IReadOnlyList<string> filesToBackup)
    {
        ArgumentNullException.ThrowIfNull(projectPath);
        ArgumentNullException.ThrowIfNull(filesToBackup);

        if (!Path.IsPathFullyQualified(projectPath))
            throw new ArgumentException("Project path must be absolute", nameof(projectPath));

        try
        {
            var projectHash = GenerateProjectHash(projectPath);
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd-HHmmss-fff");
            var backupDir = GetBackupDirectory(projectHash, timestamp);

            backupDir = EnsureUniqueBackupDirectory(backupDir);

            Directory.CreateDirectory(backupDir);

            _logger.LogDebug("Created backup directory: {BackupPath}", backupDir);

            foreach (var filePath in filesToBackup)
            {
                if (!Path.IsPathFullyQualified(filePath))
                {
                    _logger.LogWarning("Skipping non-absolute file path: {FilePath}", filePath);
                    continue;
                }

                if (!File.Exists(filePath))
                {
                    _logger.LogWarning("File not found, skipping backup: {FilePath}", filePath);
                    continue;
                }

                await BackupFile(projectPath, filePath, backupDir);
            }

            _logger.LogInformation("Backups created at: {BackupPath}", backupDir);

            return backupDir;
        }
        catch (Exception ex) when (ex is not FileSystemException)
        {
            throw new FileSystemException($"Failed to create backup for project: {projectPath}", ex);
        }
    }

    /// <inheritdoc />
    public async Task<int> RestoreBackup(string backupPath, string projectPath)
    {
        ArgumentNullException.ThrowIfNull(backupPath);
        ArgumentNullException.ThrowIfNull(projectPath);

        if (!ValidateBackup(backupPath)) throw new FileSystemException($"Invalid backup directory: {backupPath}");

        if (!Path.IsPathFullyQualified(projectPath))
            throw new ArgumentException("Project path must be absolute", nameof(projectPath));

        var restoredCount = 0;

        try
        {
            var backupFiles = Directory.GetFiles(backupPath, "*", SearchOption.AllDirectories);

            foreach (var backupFilePath in backupFiles)
            {
                var relativePath = Path.GetRelativePath(backupPath, backupFilePath);
                var targetPath = Path.Combine(projectPath, relativePath);

                try
                {
                    await RestoreFile(backupFilePath, targetPath);
                    restoredCount++;
                    _logger.LogDebug("Restored file: {FilePath}", targetPath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to restore file: {FilePath}", targetPath);
                    // Continue with remaining files
                }
            }

            _logger.LogInformation("Restored {Count} files from {BackupPath}", restoredCount, backupPath);

            return restoredCount;
        }
        catch (Exception ex) when (ex is not FileSystemException)
        {
            throw new FileSystemException($"Failed to restore backup from: {backupPath}", ex);
        }
    }

    /// <inheritdoc />
    public bool ValidateBackup(string backupPath)
    {
        ArgumentNullException.ThrowIfNull(backupPath);

        // Expand ~ to user home directory
        if (backupPath.StartsWith("~"))
            backupPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                backupPath[1..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            );

        return Directory.Exists(backupPath);
    }

    private static string GenerateProjectHash(string projectPath)
    {
        var absolutePath = Path.GetFullPath(projectPath);
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(absolutePath));
        var hashHex = Convert.ToHexString(hashBytes);
        return hashHex[..16].ToLowerInvariant();
    }

    private string GetBackupDirectory(string projectHash, string timestamp)
    {
        var baseDir = _baseBackupPath;
        if (string.IsNullOrEmpty(baseDir))
        {
            baseDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrEmpty(baseDir))
            {
                baseDir = Path.GetTempPath();
            }
        }
        return Path.Combine(baseDir, ".docify", "backups", projectHash, $"backup-{timestamp}");
    }

    private static string EnsureUniqueBackupDirectory(string backupDir)
    {
        if (!Directory.Exists(backupDir)) return backupDir;

        var counter = 1;
        string uniqueDir;
        do
        {
            uniqueDir = $"{backupDir}-{counter}";
            counter++;
        } while (Directory.Exists(uniqueDir));

        return uniqueDir;
    }

    private async Task BackupFile(string projectPath, string filePath, string backupDir)
    {
        var relativePath = Path.GetRelativePath(projectPath, filePath);
        var backupFilePath = Path.Combine(backupDir, relativePath);
        var backupFileDir = Path.GetDirectoryName(backupFilePath);

        if (string.IsNullOrEmpty(backupFileDir))
            throw new FileSystemException($"Invalid backup file path: {backupFilePath}");

        Directory.CreateDirectory(backupFileDir);

        // Atomic file copy: write to .tmp then move
        var tempFilePath = $"{backupFilePath}.tmp";

        try
        {
            await CopyFileAsync(filePath, tempFilePath);
            File.Move(tempFilePath, backupFilePath, overwrite: true);

            _logger.LogDebug("Backed up file: {FilePath} -> {BackupPath}", filePath, backupFilePath);
        }
        catch (Exception ex)
        {
            // Clean up temp file if it exists
            if (File.Exists(tempFilePath))
                try
                {
                    File.Delete(tempFilePath);
                }
                catch
                {
                    // Ignore cleanup errors
                }

            _logger.LogError(ex, "Failed to backup file: {FilePath}", filePath);
            throw new FileSystemException($"Failed to backup file: {filePath}", ex);
        }
    }

    private async Task RestoreFile(string backupFilePath, string targetPath)
    {
        var targetDir = Path.GetDirectoryName(targetPath);

        if (string.IsNullOrEmpty(targetDir)) throw new FileSystemException($"Invalid target file path: {targetPath}");

        Directory.CreateDirectory(targetDir);

        // Atomic file copy: write to .tmp then move
        var tempFilePath = $"{targetPath}.tmp";

        try
        {
            await CopyFileAsync(backupFilePath, tempFilePath);
            File.Move(tempFilePath, targetPath, overwrite: true);
        }
        catch (Exception ex)
        {
            // Clean up temp file if it exists
            if (File.Exists(tempFilePath))
                try
                {
                    File.Delete(tempFilePath);
                }
                catch
                {
                    // Ignore cleanup errors
                }

            throw new FileSystemException($"Failed to restore file: {targetPath}", ex);
        }
    }

    private static async Task CopyFileAsync(string sourcePath, string destPath)
    {
        const int bufferSize = 81920; // 80KB buffer

        await using var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize, useAsync: true);
        await using var destStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None,
            bufferSize, useAsync: true);

        await sourceStream.CopyToAsync(destStream);
    }
}
