namespace Docify.Application.Generation.Models;

/// <summary>
/// Options for documentation generation workflow
/// </summary>
public sealed record GenerationOptions(
    string ProjectPath,
    string Intensity,
    int Parallelism = 3,
    bool DryRun = false);
