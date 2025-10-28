# Docify Quick Start Guide

This guide walks you through the complete journey of using Docify to generate AI-powered XML documentation for your .NET projects.

## Table of Contents

- [Prerequisites](#prerequisites)
- [Installation](#installation)
- [Configuration](#configuration)
- [User Journey](#user-journey)
  - [1. Analyze Your Project](#1-analyze-your-project)
  - [2. Generate Documentation](#2-generate-documentation)
  - [3. Restore from Backup](#3-restore-from-backup)
- [Command Reference](#command-reference)
- [Troubleshooting](#troubleshooting)

## Prerequisites

- .NET 8.0 SDK or later
- An API key from one of the supported LLM providers:
  - OpenAI GPT (recommended: gpt-5-nano)
  - Anthropic Claude

## Installation

Install Docify as a global .NET tool:

```bash
dotnet tool install -g Docify.CLI
```

Verify the installation:

```bash
docify --version
```

## Configuration

Before generating documentation, you need to configure your LLM provider and API key.

### Step 1: Set Your Primary Provider

Configure which LLM provider you want to use:

```bash
docify config set-provider openai
```

You'll be prompted to enter the model name (e.g., `gpt-5-nano`).

**Supported providers:**
- `openai` - OpenAI GPT models (recommended: gpt-5-nano)
- `anthropic` - Anthropic Claude models

### Step 2: Set Your API Key

Securely store your API key:

```bash
docify config set-api-key openai
```

You'll be prompted to enter your API key securely (input will be hidden).

### Step 3: (Optional) Configure Fallback Provider

Set a fallback provider in case the primary fails:

```bash
docify config set-fallback anthropic
```

Then set the API key for the fallback:

```bash
docify config set-api-key anthropic
```

### Step 4: Verify Configuration

Check your current configuration:

```bash
docify config show
```

This displays your provider settings and masked API keys.

## User Journey

### 1. Analyze Your Project

Before generating documentation, analyze your project to see what needs documentation.

#### Basic Analysis

```bash
docify analyze /path/to/your/project.csproj
```

This will output a text report showing:
- Total number of public APIs found
- Documentation coverage statistics
- List of undocumented APIs
- Compilation diagnostics (if any)

#### Analysis with Different Output Formats

**JSON Format** (for automation/CI):

```bash
docify analyze /path/to/your/project.csproj --format json
```

**Markdown Format** (for documentation):

```bash
docify analyze /path/to/your/project.csproj --format markdown
```

#### Save Analysis to File

```bash
docify analyze /path/to/your/project.csproj --format markdown --file-path ./reports/analysis.md
```

#### Include LLM Context

For detailed analysis including context collection:

```bash
docify analyze /path/to/your/project.csproj --include-context
```

**Note:** This option collects additional context using LLM, which may take longer and consume API credits.

### 2. Generate Documentation

Generate XML documentation comments for your undocumented APIs.

#### Basic Generation (Interactive)

```bash
docify generate /path/to/your/project.csproj
```

This command follows an interactive workflow:
1. Analyzes the project and finds undocumented APIs
2. Prompts for confirmation before generating
3. Generates documentation using your configured LLM
4. Shows progress for each API
5. Prompts for confirmation before writing to files
6. Creates backups before modifying files
7. Writes documentation to source files

#### Generation with Intensity Filters

**Document only completely undocumented APIs** (default):

```bash
docify generate /path/to/your/project.csproj --intensity undocumented
```

**Document partially documented APIs** (missing some XML tags):

```bash
docify generate /path/to/your/project.csproj --intensity partially_documented
```

**Document all public APIs** (regenerate everything):

```bash
docify generate /path/to/your/project.csproj --intensity all
```

#### Auto-Accept Mode (No Prompts)

Skip confirmation prompts (useful for CI/CD):

```bash
docify generate /path/to/your/project.csproj --auto-accept
```

**Warning:** This will automatically write changes without confirmation.

#### Dry Run (Preview Changes)

Preview what documentation would be generated without modifying files:

```bash
docify generate /path/to/your/project.csproj --dry-run
```

This shows:
- Which files would be modified
- The exact documentation that would be added
- A summary of changes

#### Control Parallelism

Adjust concurrent API requests for performance (1-10):

```bash
docify generate /path/to/your/project.csproj --parallelism 5
```

**Default:** 3 concurrent requests

**Recommendations:**
- Lower values (1-2): Slower but gentler on API rate limits
- Higher values (5-10): Faster but may hit rate limits

#### Complete Example: Production Workflow

```bash
# 1. Analyze first to see what needs documentation
docify analyze ./MyProject.csproj --format markdown --file-path ./docs/coverage-report.md

# 2. Preview changes without writing
docify generate ./MyProject.csproj --dry-run

# 3. Generate with confirmation prompts
docify generate ./MyProject.csproj --intensity undocumented --parallelism 5
```

### 3. Restore from Backup

If you need to revert changes made by Docify, use the restore command.

#### Basic Restore

```bash
docify restore /path/to/backup/directory
```

The backup directory path is shown after successful documentation generation (usually in `.docify-backups/`).

#### Restore with Custom Project Path

```bash
docify restore /path/to/backup/directory --project-path /path/to/your/project
```

#### Skip Confirmation Prompt

```bash
docify restore /path/to/backup/directory --yes
```

**Example:**

```bash
# Restore from a specific backup
docify restore ~/.docify-backups/backup-20250128-143022 --project-path ./MyProject
```

## Command Reference

### `docify analyze`

Analyze a .NET project for documentation coverage.

**Syntax:**
```bash
docify analyze <project-path> [options]
```

**Arguments:**
- `project-path` - Path to `.csproj` or `.sln` file

**Options:**
- `--format <text|json|markdown>` - Output format (default: `text`)
- `--include-context` - Include LLM-collected context in report (default: `false`)
- `--file-path <path>` - Save report to file instead of console

**Examples:**
```bash
docify analyze ./MyProject.csproj
docify analyze ./MySolution.sln --format json --file-path report.json
docify analyze ./MyProject.csproj --include-context
```

---

### `docify generate`

Generate XML documentation for undocumented APIs.

**Syntax:**
```bash
docify generate <project-path> [options]
```

**Arguments:**
- `project-path` - Path to `.csproj` or `.sln` file

**Options:**
- `--intensity <undocumented|partially_documented|all>` - Filter APIs to document (default: `undocumented`)
  - `undocumented`: Only completely undocumented APIs
  - `partially_documented`: APIs missing some XML tags
  - `all`: All public APIs
- `--auto-accept` - Skip confirmation prompts (default: `false`)
- `--dry-run` - Preview changes without writing to files (default: `false`)
- `--parallelism <1-10>` - Number of concurrent API requests (default: `3`)

**Examples:**
```bash
docify generate ./MyProject.csproj
docify generate ./MyProject.csproj --intensity all --parallelism 5
docify generate ./MyProject.csproj --dry-run
docify generate ./MyProject.csproj --auto-accept --intensity undocumented
```

---

### `docify config`

Manage Docify configuration (providers, models, API keys).

#### Subcommands

**`config set-provider`** - Set the primary LLM provider

**Syntax:**
```bash
docify config set-provider <provider>
```

**Arguments:**
- `provider` - Provider name (`anthropic` or `openai`)

**Interactive Prompts:**
- Model name (e.g., `claude-sonnet-4-5`, `gpt-5-nano`)

**Example:**
```bash
docify config set-provider openai
# You'll be prompted: Enter model name for openai: gpt-5-nano
```

---

**`config set-fallback`** - Set the fallback LLM provider

**Syntax:**
```bash
docify config set-fallback <provider>
```

**Arguments:**
- `provider` - Fallback provider name (`anthropic` or `openai`)

**Interactive Prompts:**
- Fallback model name

**Example:**
```bash
docify config set-fallback anthropic
# You'll be prompted: Enter fallback model name for anthropic: claude-sonnet-4-5
```

---

**`config set-api-key`** - Set API key for a provider

**Syntax:**
```bash
docify config set-api-key <provider>
```

**Arguments:**
- `provider` - Provider name (`anthropic` or `openai`)

**Interactive Prompts:**
- API key (secure input - hidden)

**Example:**
```bash
docify config set-api-key openai
# You'll be prompted: Enter API key for openai: ********
```

---

**`config show`** - Display current configuration

**Syntax:**
```bash
docify config show
```

**Output:**
- Primary provider and model
- Fallback provider and model (if configured)
- API key status (masked)

**Example:**
```bash
docify config show
# Displays a table with all configuration settings
```

---

### `docify restore`

Restore files from a Docify backup.

**Syntax:**
```bash
docify restore <backup-path> [options]
```

**Arguments:**
- `backup-path` - Path to the backup directory to restore from

**Options:**
- `--project-path <path>` - Path to project root (default: current directory)
- `--yes` - Skip confirmation prompt (default: `false`)

**Examples:**
```bash
docify restore ~/.docify-backups/backup-20250128-143022
docify restore ./backups/backup-20250128-143022 --project-path ./MyProject
docify restore ~/.docify-backups/backup-20250128-143022 --yes
```

## Troubleshooting

### Authentication Errors

**Problem:** `Invalid API key` or `401 Unauthorized`

**Solution:**
1. Verify your API key is correct:
   ```bash
   docify config show
   ```
2. Set the API key again:
   ```bash
   docify config set-api-key <provider>
   ```

### No APIs Found

**Problem:** Analysis shows 0 public APIs

**Solution:**
- Ensure you're pointing to a `.csproj` or `.sln` file
- Check that your project has public classes/methods
- Verify the project compiles successfully

### Rate Limiting

**Problem:** Generation fails with rate limit errors

**Solution:**
- Reduce parallelism:
  ```bash
  docify generate ./MyProject.csproj --parallelism 1
  ```
- Wait and retry
- Consider using a fallback provider

### Compilation Errors

**Problem:** Analysis shows compilation diagnostics

**Solution:**
- Fix compilation errors in your project first
- Docify can still analyze projects with warnings, but errors may affect results

### Backup Not Found

**Problem:** Cannot restore from backup

**Solution:**
- Check the backup path exists
- Backups are stored in `.docify-backups/` by default
- Use absolute path or relative path from current directory

## Best Practices

1. **Always analyze before generating** - Understand what needs documentation
2. **Use dry-run first** - Preview changes before applying them
3. **Start with undocumented intensity** - Don't regenerate existing documentation
4. **Keep backups** - Docify creates them automatically, but keep important ones
5. **Review generated documentation** - AI-generated content should be reviewed
6. **Use version control** - Commit before running generate commands
7. **Configure fallback provider** - Ensures continuity if primary provider fails

## Next Steps

- Read [CONTRIBUTING.md](CONTRIBUTING.md) to contribute to Docify
- Check [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md) for community guidelines
- Report issues on [GitHub](https://github.com/Bardin08/docify/issues)

---

For more information, visit the [project repository](https://github.com/Bardin08/docify).
