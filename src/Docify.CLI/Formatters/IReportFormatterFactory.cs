namespace Docify.CLI.Formatters;

/// <summary>
/// Factory for creating report formatters based on format type.
/// </summary>
public interface IReportFormatterFactory
{
    /// <summary>
    /// Gets a report formatter for the specified format.
    /// </summary>
    /// <param name="format">The output format (text, json, markdown).</param>
    /// <returns>A report formatter instance.</returns>
    /// <exception cref="ArgumentException">Thrown when format is invalid.</exception>
    IReportFormatter GetFormatter(string format);
}
