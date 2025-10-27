namespace Docify.Writer.Exceptions;

/// <summary>
/// Base exception for all Docify-related errors
/// </summary>
public class DocifyException : Exception
{
    public DocifyException()
    {
    }

    public DocifyException(string message) : base(message)
    {
    }

    public DocifyException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
