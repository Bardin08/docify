using System.CommandLine;
using Docify.Application.Generation.Interfaces;
using Docify.Writer.Exceptions;
using Docify.Writer.Interfaces;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace Docify.CLI.Commands;

/// <summary>
/// Command to restore files from a backup directory
/// </summary>
public class RestoreCommand : Command
{
    private readonly IBackupManager _backupManager;
    private readonly IUserConfirmation _userConfirmation;
    private readonly ILogger<RestoreCommand> _logger;

    public RestoreCommand(
        IBackupManager backupManager,
        IUserConfirmation userConfirmation,
        ILogger<RestoreCommand> logger)
        : base("restore", "Restore files from a Docify backup")
    {
        ArgumentNullException.ThrowIfNull(backupManager);
        ArgumentNullException.ThrowIfNull(userConfirmation);
        ArgumentNullException.ThrowIfNull(logger);

        _backupManager = backupManager;
        _userConfirmation = userConfirmation;
        _logger = logger;

        var backupPathArgument = new Argument<string>(
            name: "backup-path",
            description: "Path to the backup directory to restore from");

        var projectPathOption = new Option<string>(
            name: "--project-path",
            description: "Path to the project root (defaults to current directory)",
            getDefaultValue: () => Directory.GetCurrentDirectory());

        var yesOption = new Option<bool>(
            name: "--yes",
            description: "Skip confirmation prompt",
            getDefaultValue: () => false);

        AddArgument(backupPathArgument);
        AddOption(projectPathOption);
        AddOption(yesOption);

        this.SetHandler(async (backupPath, projectPath, skipConfirmation) =>
        {
            await ExecuteAsync(backupPath, projectPath, skipConfirmation);
        }, backupPathArgument, projectPathOption, yesOption);
    }

    private async Task<int> ExecuteAsync(string backupPath, string projectPath, bool skipConfirmation)
    {
        try
        {
            // Expand ~ to user home directory if needed
            backupPath = ExpandHomePath(backupPath);
            projectPath = Path.GetFullPath(projectPath);

            // Validate backup path exists
            if (!_backupManager.ValidateBackup(backupPath))
            {
                AnsiConsole.MarkupLine("[red]Error:[/] Backup directory not found or invalid: {0}", backupPath);
                _logger.LogError("Invalid backup path: {BackupPath}", backupPath);
                return 1;
            }

            // Get file count for confirmation
            var backupFiles = Directory.GetFiles(backupPath, "*", SearchOption.AllDirectories);
            var fileCount = backupFiles.Length;

            if (fileCount == 0)
            {
                AnsiConsole.MarkupLine("[yellow]Warning:[/] Backup directory is empty: {0}", backupPath);
                return 0;
            }

            // Prompt user for confirmation unless --yes is specified
            if (!skipConfirmation)
            {
                var confirmed = await _userConfirmation.ConfirmRollback(fileCount, backupPath);
                if (!confirmed)
                {
                    _logger.LogInformation("Rollback cancelled by user.");
                    return 0;
                }
            }

            // Perform restoration with progress indicator
            var result = await AnsiConsole.Status()
                .StartAsync("Restoring files...", async ctx => await _backupManager.RestoreBackup(backupPath, projectPath));

            if (result.Success)
            {
                _logger.LogInformation("Restored {FileCount} files from {BackupPath}.", result.FilesRestored, backupPath);
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

            return 0;
        }
        catch (InvalidBackupException ex)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] {0}", ex.Message);
            _logger.LogError(ex, "Invalid backup directory");
            return 1;
        }
        catch (FileSystemException ex)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] {0}", ex.Message);
            _logger.LogError(ex, "Restore operation failed");
            return 1;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]Unexpected error:[/] {0}", ex.Message);
            _logger.LogError(ex, "Unexpected error during restore operation");
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
