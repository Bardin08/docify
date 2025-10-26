namespace Docify.Core;

/// <summary>
/// Exception thrown when a project or solution cannot be loaded or analyzed.
/// </summary>
public class ProjectLoadException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectLoadException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public ProjectLoadException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectLoadException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public ProjectLoadException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
