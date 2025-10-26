namespace Docify.CLI.Formatters;

/// <summary>
/// Factory implementation for creating report formatters.
/// </summary>
public class ReportFormatterFactory(
    TextReportFormatter textFormatter,
    JsonReportFormatter jsonFormatter,
    MarkdownReportFormatter markdownFormatter) : IReportFormatterFactory
{
    /// <inheritdoc/>
    public IReportFormatter GetFormatter(string format)
    {
        ArgumentNullException.ThrowIfNull(format);

        return format.ToLowerInvariant() switch
        {
            "text" => textFormatter,
            "json" => jsonFormatter,
            "markdown" => markdownFormatter,
            _ => throw new ArgumentException($"Invalid format '{format}'. Valid formats: text, json, markdown", nameof(format))
        };
    }
}
