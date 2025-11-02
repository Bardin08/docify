namespace Docify.Writer.Models;

/// <summary>
/// Represents the result of a backup restoration operation.
/// </summary>
/// <param name="Success">Indicates whether all files were successfully restored.</param>
/// <param name="FilesRestored">The number of files successfully restored.</param>
/// <param name="FailedFiles">List of files that failed to restore with error messages.</param>
public sealed record RestoreResult(
    bool Success,
    int FilesRestored,
    List<(string FilePath, string ErrorMessage)> FailedFiles);
