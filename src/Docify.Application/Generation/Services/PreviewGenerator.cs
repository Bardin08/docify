using System.Text;
using Docify.Application.Generation.Interfaces;
using Docify.Application.Generation.Models;

namespace Docify.Application.Generation.Services;

/// <summary>
/// Generates preview output for documentation changes in dry-run mode
/// </summary>
public sealed class PreviewGenerator : IPreviewGenerator
{
    /// <inheritdoc />
    public string BuildPreview(List<GeneratedDocumentation> suggestions)
    {
        var sb = new StringBuilder();
        sb.AppendLine("\n=== Dry-Run Preview ===\n");

        var fileGroups = suggestions.GroupBy(s => s.FilePath);

        foreach (var group in fileGroups)
        {
            sb.AppendLine($"File: {group.Key}");
            sb.AppendLine(new string('-', 80));

            foreach (var suggestion in group)
            {
                sb.AppendLine($"\n+ API: {suggestion.ApiSymbol.FullyQualifiedName}");
                sb.AppendLine("+ Documentation:");

                // Format as triple-slash comments
                var lines = suggestion.XmlDocumentation.Split('\n');
                foreach (var line in lines)
                    sb.AppendLine($"+   /// {line.TrimStart()}");
            }

            sb.AppendLine();
        }

        sb.AppendLine(
            $"\nDry-run complete. {suggestions.Count} documentation entries would be added to {fileGroups.Count()} files.");
        sb.AppendLine("Run without --dry-run to apply changes.");

        return sb.ToString();
    }
}
