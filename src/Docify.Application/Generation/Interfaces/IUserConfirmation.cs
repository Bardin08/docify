namespace Docify.Application.Generation.Interfaces;

/// <summary>
/// Abstraction for user confirmation interactions.
/// Allows orchestrator to request user consent without knowing about console/UI details.
/// </summary>
public interface IUserConfirmation
{
    /// <summary>
    /// Prompts user to confirm a batch write operation.
    /// </summary>
    /// <param name="totalChanges">Total number of documentation changes to write</param>
    /// <param name="totalFiles">Total number of files affected</param>
    /// <returns>True if user confirms, false if user declines</returns>
    Task<bool> ConfirmBatchWrite(int totalChanges, int totalFiles);

    /// <summary>
    /// Prompts user to confirm a rollback operation.
    /// </summary>
    /// <param name="fileCount">Total number of files to restore from backup</param>
    /// <param name="backupPath">Path to the backup directory</param>
    /// <returns>True if user confirms, false if user declines</returns>
    Task<bool> ConfirmRollback(int fileCount, string backupPath);
}
