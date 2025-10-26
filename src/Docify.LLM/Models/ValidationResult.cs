namespace Docify.LLM.Models;

/// <summary>
/// Represents the result of validating LLM-generated XML documentation.
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Indicates whether the generated XML documentation is valid.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// List of validation issues found (warnings or errors).
    /// Empty if no issues were found.
    /// </summary>
    public List<string> Issues { get; init; } = [];

    /// <summary>
    /// Cleaned XML extracted from non-XML response, if applicable.
    /// Set when XML tags were successfully extracted from a response containing non-XML text.
    /// </summary>
    public string? CleanedXml { get; init; }

    /// <summary>
    /// Creates a valid validation result with no issues.
    /// </summary>
    /// <param name="cleanedXml">Optional cleaned XML if extraction was performed.</param>
    /// <returns>A ValidationResult indicating success.</returns>
    public static ValidationResult Valid(string? cleanedXml = null)
    {
        return new ValidationResult
        {
            IsValid = true,
            Issues = [],
            CleanedXml = cleanedXml
        };
    }

    /// <summary>
    /// Creates an invalid validation result with the specified issue.
    /// </summary>
    /// <param name="issue">Description of the validation issue.</param>
    /// <returns>A ValidationResult indicating failure.</returns>
    public static ValidationResult Invalid(string issue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issue, nameof(issue));

        return new ValidationResult
        {
            IsValid = false,
            Issues = [issue]
        };
    }

    /// <summary>
    /// Creates an invalid validation result with multiple issues.
    /// </summary>
    /// <param name="issues">List of validation issues.</param>
    /// <returns>A ValidationResult indicating failure.</returns>
    public static ValidationResult Invalid(List<string> issues)
    {
        ArgumentNullException.ThrowIfNull(issues, nameof(issues));

        if (issues.Count == 0)
            throw new ArgumentException("Issues list cannot be empty", nameof(issues));

        return new ValidationResult
        {
            IsValid = false,
            Issues = issues
        };
    }
}
