using Docify.Core.Models;
using Docify.LLM.Abstractions;
using Microsoft.CodeAnalysis;

namespace Docify.Application.Generation.Models;

/// <summary>
/// Configuration for parallel documentation generation tasks
/// </summary>
internal sealed record GenerationTaskConfig(
    string ProjectPath,
    List<ApiSymbol> Apis,
    Compilation Compilation,
    ILlmProvider Provider,
    int Parallelism,
    bool DryRun,
    GenerationContext Context);
