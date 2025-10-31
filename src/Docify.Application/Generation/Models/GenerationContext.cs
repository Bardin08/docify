using System.Collections.Concurrent;
using Docify.Core.Models;

namespace Docify.Application.Generation.Models;

/// <summary>
/// Context for parallel documentation generation with shared state
/// </summary>
internal sealed class GenerationContext
{
    public DryRunCache? Cache { get; init; }
    public ConcurrentBag<GeneratedDocumentation> Suggestions { get; } = [];
    public int CompletedCount;
    public int CacheHits;
    public int CacheMisses;
    public int AuthFailureDetected;
    public string? AuthErrorMessage;
}
