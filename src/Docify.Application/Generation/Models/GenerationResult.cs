namespace Docify.Application.Generation.Models;

/// <summary>
/// Result of documentation generation workflow
/// </summary>
public sealed record GenerationResult(
    GenerationStatus Status,
    string? Message = null,
    int ApiCount = 0,
    int SuccessCount = 0,
    int FileCount = 0,
    string? PreviewOutput = null,
    string? CacheFilePath = null)
{
    public static GenerationResult Success(int successCount, int fileCount, string? previewOutput = null,
        string? cacheFilePath = null) =>
        new(GenerationStatus.Success, null, 0, successCount, fileCount, previewOutput, cacheFilePath);

    public static GenerationResult Error(GenerationStatus status, string message) =>
        new(status, message, 0);
}

/// <summary>
/// Status of generation workflow
/// </summary>
public enum GenerationStatus
{
    Success,
    NoApisFound,
    GenerationFailed,
    WriteFailed
}
