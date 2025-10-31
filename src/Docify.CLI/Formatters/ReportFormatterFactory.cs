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
    /// <summary>Creates an IReportFormatter configured for the specified format. The returned formatter is ready to generate reports in the requested format.</summary>
    /// <param name="format">The output format used to configure the returned formatter.</param>
    /// <returns>An IReportFormatter configured for the specified format.</returns>
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
