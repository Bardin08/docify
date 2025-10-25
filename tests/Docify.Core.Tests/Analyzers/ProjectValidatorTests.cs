using Docify.Core.Analyzers;
using Shouldly;

namespace Docify.Core.Tests.Analyzers;

public class ProjectValidatorTests
{
    [Fact]
    public void ValidateProjectPath_NullPath_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            ProjectValidator.ValidateProjectPath(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateProjectPath_EmptyOrWhitespacePath_ReturnsFailure(string path)
    {
        // Act
        var result = ProjectValidator.ValidateProjectPath(path);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ErrorMessage.ShouldBe("Project path cannot be empty.");
    }

    [Fact]
    public void ValidateProjectPath_NonExistentFile_ReturnsFailure()
    {
        // Arrange
        var path = "/non/existent/path/project.csproj";

        // Act
        var result = ProjectValidator.ValidateProjectPath(path);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ErrorMessage.ShouldBe($"File not found: {path}");
    }

    [Theory]
    [InlineData("project.txt")]
    [InlineData("project.cs")]
    [InlineData("project.dll")]
    public void ValidateProjectPath_InvalidExtension_ReturnsFailure(string fileName)
    {
        // Arrange
        var tempFile = Path.Combine(Path.GetTempPath(), fileName);
        File.WriteAllText(tempFile, "test content");

        try
        {
            // Act
            var result = ProjectValidator.ValidateProjectPath(tempFile);

            // Assert
            result.IsValid.ShouldBeFalse();
            result.ErrorMessage.ShouldNotBeNull();
            result.ErrorMessage.ShouldContain("Invalid file extension");
            result.ErrorMessage.ShouldContain("Expected .csproj or .sln file");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Theory]
    [InlineData("project.csproj")]
    [InlineData("solution.sln")]
    [InlineData("PROJECT.CSPROJ")]
    [InlineData("SOLUTION.SLN")]
    public void ValidateProjectPath_ValidExtension_ReturnsSuccess(string fileName)
    {
        // Arrange
        var tempFile = Path.Combine(Path.GetTempPath(), fileName);
        File.WriteAllText(tempFile, "test content");

        try
        {
            // Act
            var result = ProjectValidator.ValidateProjectPath(tempFile);

            // Assert
            result.IsValid.ShouldBeTrue();
            result.ErrorMessage.ShouldBeNull();
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
