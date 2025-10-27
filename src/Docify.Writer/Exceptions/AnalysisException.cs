namespace Docify.Writer.Exceptions;

/// <summary>
/// Exception thrown for Roslyn parsing and analysis failures
/// </summary>
public class AnalysisException : DocifyException
{
    public AnalysisException()
    {
    }

    public AnalysisException(string message) : base(message)
    {
    }

    public AnalysisException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
