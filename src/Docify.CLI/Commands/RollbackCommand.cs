using System.CommandLine;
using Docify.Application.Generation.Interfaces;
using Docify.Writer.Exceptions;
using Docify.Writer.Interfaces;
using Microsoft.Extensions.Logging;

namespace Docify.CLI.Commands;

/// <summary>
/// Command to rollback (restore) files from a backup directory.
/// Alias for RestoreCommand with simplified interface.
/// </summary>
public sealed class RollbackCommand : Command
{
    private readonly IBackupManager _backupManager;
    private readonly IUserConfirmation _userConfirmation;
    private readonly ILogger<RollbackCommand> _logger;

    public RollbackCommand(
        IBackupManager backupManager,
        IUserConfirmation userConfirmation,
        ILogger<RollbackCommand> logger)
        : base("rollback", "Restore files from a previous backup")
    {
        ArgumentNullException.ThrowIfNull(backupManager);
        ArgumentNullException.ThrowIfNull(userConfirmation);
        ArgumentNullException.ThrowIfNull(logger);

        _backupManager = backupManager;
        _userConfirmation = userConfirmation;
        _logger = logger;

        var backupPathArgument = new Argument<string>(
            name: "backup-path",
            description: "Path to backup directory to restore");

        AddArgument(backupPathArgument);

        this.SetHandler(async (backupPath) =>
        {
            Environment.ExitCode = await ExecuteAsync(backupPath);
        }, backupPathArgument);
    }

    private async Task<int> ExecuteAsync(string backupPath)
    {
        ArgumentNullException.ThrowIfNull(backupPath);

        try
        {
            // Expand ~ to user home directory if needed
            backupPath = ExpandHomePath(backupPath);
            var projectPath = Path.GetFullPath(Directory.GetCurrentDirectory());

            // Validate backup exists
            if (!Directory.Exists(backupPath))
            {
                _logger.LogError("Backup directory not found: {BackupPath}", backupPath);
                return 1;
            }

            // Count files to restore
            var filesToRestore = Directory.GetFiles(backupPath, "*", SearchOption.AllDirectories).Length;

            if (filesToRestore == 0)
            {
                _logger.LogWarning("Backup directory is empty: {BackupPath}", backupPath);
                return 1;
            }

            // Prompt user for confirmation
            var confirmed = await _userConfirmation.ConfirmRollback(filesToRestore, backupPath);
            if (!confirmed)
            {
                _logger.LogInformation("Rollback cancelled by user.");
                return 0;
            }

            // Execute rollback
            var result = await _backupManager.RestoreBackup(backupPath, projectPath);

            if (result.Success)
            {
                _logger.LogInformation("Restored {FileCount} files from {BackupPath}.",
                    result.FilesRestored, backupPath);
                return 0;
            }
            else
            {
                _logger.LogError("Rollback failed. {FilesRestored} files restored, {FailedCount} failed.",
                    result.FilesRestored, result.FailedFiles.Count);
                foreach (var (filePath, errorMessage) in result.FailedFiles)
                {
                    _logger.LogError("  - {FilePath}: {ErrorMessage}", filePath, errorMessage);
                }
                return 1;
            }
        }
        catch (InvalidBackupException ex)
        {
            _logger.LogError(ex, "Invalid backup directory: {BackupPath}", backupPath);
            return 1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during rollback operation");
            return 1;
        }
    }

    private static string ExpandHomePath(string path)
    {
        if (path.StartsWith("~"))
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                path[1..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            );
        }

        return path;
    }
}
