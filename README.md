# Docify

[![CI](https://github.com/Bardin08/docify/actions/workflows/ci.yml/badge.svg)](https://github.com/Bardin08/docify/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/Docify.CLI.svg)](https://www.nuget.org/packages/Docify.CLI)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/download/dotnet/8.0)

A CLI tool for generating XML documentation comments for .NET projects using AI.

**Status:** In active development

## Overview

Docify analyzes .NET codebases using Roslyn and generates contextually-aware XML documentation through LLM integration. It provides an interactive terminal interface for reviewing and editing suggestions before applying changes.

## Key Features

- Roslyn-based semantic analysis
- Interactive terminal UI for documentation review
- Context-aware suggestions using call-site analysis
- Staleness detection for outdated documentation
- Cross-platform support (Windows, macOS, Linux)
- Local-first architecture with no cloud dependencies

## Installation

```bash
dotnet tool install -g Docify.CLI
```

## Quick Start

```bash
# Configure your LLM provider
docify config set-provider openai
docify config set-api-key openai

# Analyze your project
docify analyze <project-path>

# Generate documentation
docify generate <project-path>
```

📖 **[Read the complete Quick Start Guide](QUICKSTART.md)** for detailed instructions, command reference, and best practices.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for development setup and guidelines.

Please read our [Code of Conduct](CODE_OF_CONDUCT.md) before contributing.

## License

MIT License - see [LICENSE](LICENSE) for details.
