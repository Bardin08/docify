using System.CommandLine;
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
    private readonly ILogger<RestoreCommand> _logger;

    public RestoreCommand(
        IBackupManager backupManager,
        ILogger<RestoreCommand> logger)
        : base("restore", "Restore files from a Docify backup")
    {
        ArgumentNullException.ThrowIfNull(backupManager);
        ArgumentNullException.ThrowIfNull(logger);

        _backupManager = backupManager;
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
                AnsiConsole.MarkupLine("[yellow]Warning:[/] This will overwrite {0} file(s) in: {1}", fileCount, projectPath);

                if (!AnsiConsole.Confirm("Do you want to continue?", defaultValue: false))
                {
                    AnsiConsole.MarkupLine("[yellow]Restore cancelled.[/]");
                    return 0;
                }
            }

            // Perform restoration with progress indicator
            int restoredCount = 0;

            await AnsiConsole.Status()
                .StartAsync("Restoring files...", async ctx =>
                {
                    restoredCount = await _backupManager.RestoreBackup(backupPath, projectPath);
                });

            if (restoredCount > 0)
            {
                AnsiConsole.MarkupLine("[green]Success:[/] Restored {0} file(s) from backup.", restoredCount);
                _logger.LogInformation("Successfully restored {Count} files from {BackupPath}", restoredCount, backupPath);
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]Warning:[/] No files were restored.");
                _logger.LogWarning("No files restored from {BackupPath}", backupPath);
            }

            return 0;
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
