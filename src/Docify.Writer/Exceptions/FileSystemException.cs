namespace Docify.Writer.Exceptions;

/// <summary>
/// Exception thrown for file system operation failures during backup/restore operations
/// </summary>
public class FileSystemException : DocifyException
{
    public FileSystemException()
    {
    }

    public FileSystemException(string message) : base(message)
    {
    }

    public FileSystemException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
