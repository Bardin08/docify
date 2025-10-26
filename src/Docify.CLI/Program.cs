using System.CommandLine;
using Docify.CLI.Commands;
using Docify.Core.Analyzers;
using Docify.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .CreateLogger();

try
{
    // Build service provider
    await using var serviceProvider = new ServiceCollection()
        .AddLogging(builder => builder.AddSerilog(dispose: true))
        .AddSingleton<IDocumentationDetector, DocumentationDetector>()
        .AddSingleton<ISymbolExtractor, SymbolExtractor>()
        .AddSingleton<ICodeAnalyzer, RoslynAnalyzer>()
        .AddSingleton<AnalyzeCommand>()
        .AddSingleton(ConstructRootCommand)
        .BuildServiceProvider();

    var rootCommand = serviceProvider.GetRequiredService<RootCommand>();
    return await rootCommand.InvokeAsync(args);
}
finally
{
    await Log.CloseAndFlushAsync();
}

RootCommand ConstructRootCommand(IServiceProvider sp1)
{
    var rootCommand = new RootCommand("Docify - AI-Powered XML Documentation Generator for .NET");
    rootCommand.AddCommand(sp1.GetRequiredService<AnalyzeCommand>());
    return rootCommand;
}
