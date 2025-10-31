using Docify.Core.Models;
using Docify.LLM.Abstractions;
using Microsoft.CodeAnalysis;

namespace Docify.Application.Generation.Models;

/// <summary>
/// Configuration for processing a single API documentation task
/// </summary>
internal sealed record ApiProcessingConfig(
    string ProjectPath,
    ApiSymbol Api,
    Compilation Compilation,
    ILlmProvider Provider,
    bool DryRun,
    GenerationContext Context,
    SemaphoreSlim Semaphore,
    int TotalCount);
