namespace Docify.CLI.Formatters;

/// <summary>
/// Base class for report formatters providing common helper methods.
/// </summary>
public abstract class ReportFormatterBase
{
    /// <summary>
    /// Extracts the namespace from a fully qualified name.
    /// </summary>
    /// <param name="fullyQualifiedName">The fully qualified name.</param>
    /// <returns>The namespace, or "&lt;global&gt;" if none exists.</returns>
    protected static string GetNamespace(string fullyQualifiedName)
    {
        var lastDot = fullyQualifiedName.LastIndexOf('.');
        if (lastDot == -1)
            return "<global>";

        var secondLastDot = fullyQualifiedName.LastIndexOf('.', lastDot - 1);
        return secondLastDot == -1
            ? fullyQualifiedName[..lastDot]
            : fullyQualifiedName[..secondLastDot];
    }

    /// <summary>
    /// Extracts the containing type name from a fully qualified name.
    /// </summary>
    /// <param name="fullyQualifiedName">The fully qualified name.</param>
    /// <returns>The containing type name.</returns>
    protected static string GetContainingType(string fullyQualifiedName)
    {
        var lastDot = fullyQualifiedName.LastIndexOf('.');
        if (lastDot == -1)
            return fullyQualifiedName;

        var secondLastDot = fullyQualifiedName.LastIndexOf('.', lastDot - 1);
        return secondLastDot == -1
            ? fullyQualifiedName
            : fullyQualifiedName.Substring(secondLastDot + 1, lastDot - secondLastDot - 1);
    }

    /// <summary>
    /// Extracts the member name from a fully qualified name.
    /// </summary>
    /// <param name="fullyQualifiedName">The fully qualified name.</param>
    /// <returns>The member name.</returns>
    protected static string GetMemberName(string fullyQualifiedName)
    {
        var lastDot = fullyQualifiedName.LastIndexOf('.');
        return lastDot == -1 ? fullyQualifiedName : fullyQualifiedName[(lastDot + 1)..];
    }
}
