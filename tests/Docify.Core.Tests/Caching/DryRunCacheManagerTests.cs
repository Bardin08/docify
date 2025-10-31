using Docify.Core.Caching;
using Docify.Core.Models;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Xunit;

namespace Docify.Core.Tests.Caching;

/// <summary>
/// Unit tests for DryRunCacheManager
/// </summary>
public class DryRunCacheManagerTests : IDisposable
{
    private readonly Mock<ILogger<DryRunCacheManager>> _mockLogger;
    private readonly DryRunCacheManager _cacheManager;
    private readonly string _testProjectPath;
    private readonly string _tempDirectory;

    public DryRunCacheManagerTests()
    {
        _mockLogger = new Mock<ILogger<DryRunCacheManager>>();
        _cacheManager = new DryRunCacheManager(_mockLogger.Object);

        // Create temp directory for test cache files
        _tempDirectory = Path.Combine(Path.GetTempPath(), "docify-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDirectory);
        _testProjectPath = Path.Combine(_tempDirectory, "TestProject.csproj");
    }

    public void Dispose()
    {
        // Cleanup temp directory
        if (Directory.Exists(_tempDirectory))
        {
            try
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Fact]
    public async Task SaveEntry_WithValidData_CreatesCacheFile()
    {
        // Arrange
        var entry = new DryRunCacheEntry
        {
            ApiSymbolId = "test-api-id",
            GeneratedXml = "<summary>Test documentation</summary>",
            CachedAt = DateTime.UtcNow,
            Provider = "Claude",
            Model = "claude-sonnet-4-5"
        };

        // Act
        await _cacheManager.SaveEntry(_testProjectPath, entry);

        // Assert
        var cacheFilePath = _cacheManager.GetCacheFilePath(_testProjectPath);
        File.Exists(cacheFilePath).ShouldBeTrue();
    }

    [Fact]
    public async Task SaveEntry_ThenLoad_ReturnsCorrectData()
    {
        // Arrange
        var entry = new DryRunCacheEntry
        {
            ApiSymbolId = "test-api-123",
            GeneratedXml = "<summary>Test method</summary>",
            CachedAt = DateTime.UtcNow,
            Provider = "GPT-5",
            Model = "gpt-5-turbo"
        };

        // Act
        await _cacheManager.SaveEntry(_testProjectPath, entry);
        var loadedCache = await _cacheManager.LoadCache(_testProjectPath);

        // Assert
        loadedCache.ShouldNotBeNull();
        loadedCache.Entries.ShouldContain(e => e.ApiSymbolId == "test-api-123");
        var loadedEntry = loadedCache.Entries.First(e => e.ApiSymbolId == "test-api-123");
        loadedEntry.GeneratedXml.ShouldBe("<summary>Test method</summary>");
        loadedEntry.Provider.ShouldBe("GPT-5");
        loadedEntry.Model.ShouldBe("gpt-5-turbo");
    }

    [Fact]
    public async Task SaveEntry_MultipleEntries_StoresAllEntries()
    {
        // Arrange
        var entry1 = new DryRunCacheEntry
        {
            ApiSymbolId = "api-1",
            GeneratedXml = "<summary>First API</summary>",
            CachedAt = DateTime.UtcNow,
            Provider = "Claude",
            Model = "claude-sonnet-4-5"
        };

        var entry2 = new DryRunCacheEntry
        {
            ApiSymbolId = "api-2",
            GeneratedXml = "<summary>Second API</summary>",
            CachedAt = DateTime.UtcNow,
            Provider = "Claude",
            Model = "claude-sonnet-4-5"
        };

        // Act
        await _cacheManager.SaveEntry(_testProjectPath, entry1);
        await _cacheManager.SaveEntry(_testProjectPath, entry2);
        var loadedCache = await _cacheManager.LoadCache(_testProjectPath);

        // Assert
        loadedCache.ShouldNotBeNull();
        loadedCache.Entries.Count.ShouldBe(2);
        loadedCache.Entries.ShouldContain(e => e.ApiSymbolId == "api-1");
        loadedCache.Entries.ShouldContain(e => e.ApiSymbolId == "api-2");
    }

    [Fact]
    public async Task SaveEntry_DuplicateApiId_UpdatesExistingEntry()
    {
        // Arrange
        var originalEntry = new DryRunCacheEntry
        {
            ApiSymbolId = "api-duplicate",
            GeneratedXml = "<summary>Original documentation</summary>",
            CachedAt = DateTime.UtcNow.AddHours(-1),
            Provider = "Claude",
            Model = "claude-sonnet-4-5"
        };

        var updatedEntry = new DryRunCacheEntry
        {
            ApiSymbolId = "api-duplicate",
            GeneratedXml = "<summary>Updated documentation</summary>",
            CachedAt = DateTime.UtcNow,
            Provider = "GPT-5",
            Model = "gpt-5-turbo"
        };

        // Act
        await _cacheManager.SaveEntry(_testProjectPath, originalEntry);
        await _cacheManager.SaveEntry(_testProjectPath, updatedEntry);
        var loadedCache = await _cacheManager.LoadCache(_testProjectPath);

        // Assert
        loadedCache.ShouldNotBeNull();
        loadedCache.Entries.Count.ShouldBe(1);
        var entry = loadedCache.Entries.First();
        entry.GeneratedXml.ShouldBe("<summary>Updated documentation</summary>");
        entry.Provider.ShouldBe("GPT-5");
    }

    [Fact]
    public async Task LoadCache_NoExistingCache_ReturnsNull()
    {
        // Arrange
        var nonExistentProjectPath = Path.Combine(_tempDirectory, "NonExistent.csproj");

        // Act
        var cache = await _cacheManager.LoadCache(nonExistentProjectPath);

        // Assert
        cache.ShouldBeNull();
    }

    [Fact]
    public async Task ClearCache_ExistingCache_DeletesCacheFile()
    {
        // Arrange
        var entry = new DryRunCacheEntry
        {
            ApiSymbolId = "test-clear",
            GeneratedXml = "<summary>To be cleared</summary>",
            CachedAt = DateTime.UtcNow,
            Provider = "Claude",
            Model = "claude-sonnet-4-5"
        };

        await _cacheManager.SaveEntry(_testProjectPath, entry);
        var cacheFilePath = _cacheManager.GetCacheFilePath(_testProjectPath);
        File.Exists(cacheFilePath).ShouldBeTrue();

        // Act
        await _cacheManager.ClearCache(_testProjectPath);

        // Assert
        File.Exists(cacheFilePath).ShouldBeFalse();
    }

    [Fact]
    public async Task ClearCache_NoExistingCache_DoesNotThrow()
    {
        // Arrange
        var nonExistentProjectPath = Path.Combine(_tempDirectory, "NonExistent2.csproj");

        // Act & Assert (should not throw)
        await _cacheManager.ClearCache(nonExistentProjectPath);
    }

    [Fact]
    public void IsCacheExpired_RecentTimestamp_ReturnsFalse()
    {
        // Arrange
        var recentTimestamp = DateTime.UtcNow.AddHours(-12); // 12 hours ago

        // Act
        var isExpired = _cacheManager.IsCacheExpired(recentTimestamp);

        // Assert
        isExpired.ShouldBeFalse();
    }

    [Fact]
    public void IsCacheExpired_OldTimestamp_ReturnsTrue()
    {
        // Arrange
        var oldTimestamp = DateTime.UtcNow.AddHours(-25); // 25 hours ago

        // Act
        var isExpired = _cacheManager.IsCacheExpired(oldTimestamp);

        // Assert
        isExpired.ShouldBeTrue();
    }

    [Fact]
    public void IsCacheExpired_ExactlyExpired_ReturnsTrue()
    {
        // Arrange
        var exactlyExpiredTimestamp = DateTime.UtcNow.AddHours(-24).AddSeconds(-1); // Just over 24 hours

        // Act
        var isExpired = _cacheManager.IsCacheExpired(exactlyExpiredTimestamp);

        // Assert
        isExpired.ShouldBeTrue();
    }

    [Fact]
    public void GetCacheFilePath_ValidProjectPath_ReturnsCorrectPath()
    {
        // Arrange
        var projectPath = "/path/to/project/Test.csproj";

        // Act
        var cacheFilePath = _cacheManager.GetCacheFilePath(projectPath);

        // Assert
        cacheFilePath.ShouldContain(".docify");
        cacheFilePath.ShouldContain("cache");
        cacheFilePath.ShouldEndWith("dry-run-cache.json");
    }

    [Fact]
    public void GetCacheFilePath_SameProjectPath_ReturnsSamePath()
    {
        // Arrange
        var projectPath = "/path/to/same/Project.csproj";

        // Act
        var path1 = _cacheManager.GetCacheFilePath(projectPath);
        var path2 = _cacheManager.GetCacheFilePath(projectPath);

        // Assert
        path1.ShouldBe(path2);
    }

    [Fact]
    public void GetCacheFilePath_DifferentProjectPaths_ReturnsDifferentPaths()
    {
        // Arrange
        var projectPath1 = "/path/to/project1/Test.csproj";
        var projectPath2 = "/path/to/project2/Test.csproj";

        // Act
        var path1 = _cacheManager.GetCacheFilePath(projectPath1);
        var path2 = _cacheManager.GetCacheFilePath(projectPath2);

        // Assert
        path1.ShouldNotBe(path2);
    }

    [Fact]
    public async Task SaveEntry_NullProjectPath_ThrowsArgumentNullException()
    {
        // Arrange
        var entry = new DryRunCacheEntry
        {
            ApiSymbolId = "test",
            GeneratedXml = "<summary>Test</summary>",
            CachedAt = DateTime.UtcNow,
            Provider = "Claude",
            Model = "claude-sonnet-4-5"
        };

        // Act & Assert
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        await Should.ThrowAsync<ArgumentNullException>(() => _cacheManager.SaveEntry(null, entry));
#pragma warning restore CS8625
    }

    [Fact]
    public async Task SaveEntry_NullEntry_ThrowsArgumentNullException()
    {
        // Act & Assert
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        await Should.ThrowAsync<ArgumentNullException>(() => _cacheManager.SaveEntry(_testProjectPath, null));
#pragma warning restore CS8625
    }

    [Fact]
    public async Task LoadCache_NullProjectPath_ThrowsArgumentNullException()
    {
        // Act & Assert
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        await Should.ThrowAsync<ArgumentNullException>(() => _cacheManager.LoadCache(null));
#pragma warning restore CS8625
    }
}
