namespace Docify.Writer.Interfaces;

/// <summary>
/// Manages backup and restoration of source files before modification
/// </summary>
public interface IBackupManager
{
    /// <summary>
    /// Creates a backup of specified files in the project
    /// </summary>
    /// <param name="projectPath">Absolute path to the project root</param>
    /// <param name="filesToBackup">List of absolute file paths to backup</param>
    /// <returns>Absolute path to the backup directory</returns>
    Task<string> CreateBackup(string projectPath, IReadOnlyList<string> filesToBackup);

    /// <summary>
    /// Restores files from a backup directory to their original locations
    /// </summary>
    /// <param name="backupPath">Absolute path to the backup directory</param>
    /// <param name="projectPath">Absolute path to the project root (original location)</param>
    /// <returns>Count of files successfully restored</returns>
    Task<int> RestoreBackup(string backupPath, string projectPath);

    /// <summary>
    /// Validates that a backup directory exists and contains valid backup structure
    /// </summary>
    /// <param name="backupPath">Absolute path to the backup directory</param>
    /// <returns>True if backup is valid, false otherwise</returns>
    bool ValidateBackup(string backupPath);
}
