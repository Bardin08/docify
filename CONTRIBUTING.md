# Contributing to Docify

Thank you for your interest in contributing to Docify! We welcome contributions from the community.

## Getting Started

1. **Fork the repository** on GitHub
2. **Clone your fork** locally:
   ```bash
   git clone https://github.com/your-username/docify.git
   cd docify
   ```
3. **Create a feature branch**:
   ```bash
   git checkout -b feature/your-feature-name
   ```

## Development Setup

### Prerequisites

- .NET 8.0 SDK or later
- A code editor (Visual Studio 2022, Visual Studio Code, or JetBrains Rider recommended)

### Building the Project

```bash
# Restore dependencies
dotnet restore

# Build the solution
dotnet build

# Run tests
dotnet test
```

## Code Style

We use EditorConfig to maintain consistent code style across the project. Please ensure your editor respects the `.editorconfig` file in the repository root.

### Key Conventions

- **Naming:**
  - Classes/Interfaces: `PascalCase`
  - Methods: `PascalCase`
  - Private fields: `_camelCase` (underscore prefix)
  - Public properties: `PascalCase`
  - Local variables: `camelCase`

- **Language Features:**
  - Use C# 12 features where appropriate
  - Enable nullable reference types
  - Use `var` when the type is apparent
  - Prefer expression-bodied members for simple properties and methods

- **Critical Rules:**
  - NEVER use `Console.WriteLine` directly - always use `ILogger`
  - All async methods must use `ConfigureAwait(false)` (except in TUI code)
  - All public methods must validate arguments
  - LLM API calls must include timeout and cancellation support

## Testing

- Write unit tests for new functionality using xUnit
- Test files should be named `{ClassUnderTest}Tests.cs`
- Use Shouldly for assertions (e.g., `result.ShouldBe(expected)`)
- Use Moq for mocking dependencies
- Follow the AAA pattern (Arrange, Act, Assert)

Example:
```csharp
[Fact]
public void AnalyzeProject_WithValidPath_ShouldReturnResults()
{
    // Arrange
    var analyzer = new RoslynAnalyzer();
    var projectPath = "/path/to/project";

    // Act
    var result = analyzer.AnalyzeProject(projectPath);

    // Assert
    result.ShouldNotBeNull();
    result.Symbols.ShouldNotBeEmpty();
}
```

## Pull Request Process

1. **Ensure all tests pass** before submitting:
   ```bash
   dotnet test
   ```

2. **Update documentation** if you're changing functionality

3. **Write a clear PR description** explaining:
   - What problem does this solve?
   - What changes were made?
   - How was it tested?

4. **Reference any related issues** using GitHub keywords (e.g., "Fixes #123")

5. **Keep PRs focused** - one feature or fix per PR

6. **Be responsive** to code review feedback

## Commit Message Guidelines

We follow conventional commit format:

```
<type>(<scope>): <subject>

<body>

<footer>
```

**Types:**
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation changes
- `refactor`: Code refactoring
- `test`: Adding or updating tests
- `chore`: Build process or tooling changes

**Example:**
```
feat(analyzer): add support for record types

Implement symbol extraction for C# 9+ record types to ensure they are properly analyzed and documented.

Fixes #42
```

## Code Review

All contributions go through code review. Reviewers will check for:

- Code quality and adherence to project standards
- Test coverage
- Documentation updates
- Performance implications
- Security considerations

## Questions?

If you have questions about contributing, please:

1. Check the [documentation](docs/)
2. Search [existing issues](https://github.com/username/docify/issues)
3. Open a new issue with the `question` label

## Code of Conduct

Please note that this project is released with a [Code of Conduct](CODE_OF_CONDUCT.md). By participating in this project you agree to abide by its terms.

## License

By contributing to Docify, you agree that your contributions will be licensed under the MIT License.
