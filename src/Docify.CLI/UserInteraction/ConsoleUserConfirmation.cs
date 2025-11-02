using Docify.Application.Generation.Interfaces;

namespace Docify.CLI.UserInteraction;

/// <summary>
/// Console-based implementation of user confirmation.
/// Prompts user via console for batch write confirmation.
/// </summary>
public sealed class ConsoleUserConfirmation : IUserConfirmation
{
    /// <inheritdoc />
    public Task<bool> ConfirmBatchWrite(int totalChanges, int totalFiles)
    {
        Console.WriteLine($"\nWrite {totalChanges} documentation changes to {totalFiles} files? (Y/n)");
        var userInput = Console.ReadLine();

        var confirmed = string.IsNullOrWhiteSpace(userInput)
            || userInput.Equals("Y", StringComparison.OrdinalIgnoreCase)
            || userInput.Equals("y", StringComparison.OrdinalIgnoreCase);

        return Task.FromResult(confirmed);
    }

    /// <inheritdoc />
    public Task<bool> ConfirmRollback(int fileCount, string backupPath)
    {
        Console.WriteLine($"\nRestore {fileCount} files from {backupPath}?");
        Console.WriteLine("This will overwrite current files. (Y/n)");
        var userInput = Console.ReadLine();

        var confirmed = string.IsNullOrWhiteSpace(userInput)
            || userInput.Equals("Y", StringComparison.OrdinalIgnoreCase)
            || userInput.Equals("y", StringComparison.OrdinalIgnoreCase);

        return Task.FromResult(confirmed);
    }
}
