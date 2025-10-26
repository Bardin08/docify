namespace Docify.Core.Analyzers;

/// <summary>
/// Validates project file paths before analysis.
/// </summary>
public static class ProjectValidator
{
    private static readonly string[] _validExtensions = [".csproj", ".sln"];

    /// <summary>
    /// Validates that the specified project path exists and has a valid extension.
    /// </summary>
    /// <param name="path">The path to validate.</param>
    /// <returns>A validation result containing success status and error message if applicable.</returns>
    public static ValidationResult ValidateProjectPath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (string.IsNullOrWhiteSpace(path))
            return ValidationResult.Failure("Project path cannot be empty.");

        if (!File.Exists(path))
            return ValidationResult.Failure($"File not found: {path}");

        var extension = Path.GetExtension(path);
        if (!_validExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            return ValidationResult.Failure(
                $"Invalid file extension '{extension}'. Expected .csproj or .sln file.");

        return ValidationResult.Success();
    }
}

/// <summary>
/// Represents the result of a validation operation.
/// </summary>
public record ValidationResult
{
    /// <summary>
    /// Gets a value indicating whether the validation succeeded.
    /// </summary>
    public bool IsValid { get; private init; }

    /// <summary>
    /// Gets the error message if validation failed.
    /// </summary>
    public string? ErrorMessage { get; private init; }

    private ValidationResult() { }

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static ValidationResult Success() => new() { IsValid = true };

    /// <summary>
    /// Creates a failed validation result with an error message.
    /// </summary>
    /// <param name="errorMessage">The error message.</param>
    public static ValidationResult Failure(string errorMessage) =>
        new() { IsValid = false, ErrorMessage = errorMessage };
}
