using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Sdk;

namespace UnifiedStorage.LocalDisk.Tests;

public class LocalDiskTests : IDisposable
{
    private readonly ILogger<LocalDiskService> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly string _testFilesDirectory;

    public LocalDiskTests()
    {
        // Configure Serilog to log to a file.
        Log.Logger = new LoggerConfiguration()
            .WriteTo.File("UnitTestLogs.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        _loggerFactory = LoggerFactory.Create(builder => builder.AddSerilog());

        _logger = _loggerFactory.CreateLogger<LocalDiskService>();

        // Dynamically determine the path to the TestFiles directory
        // based on the current project directory.
        _testFilesDirectory = Path.GetFullPath(
            Path.Combine(
                Directory.GetCurrentDirectory(),
                @"..\..\..\..\TestFiles"
        ));
    }

    [Fact]
    public async Task AddAsync_ShouldAddFile()
    {
        // Arrange
        var storageProvider = new LocalDiskService(_logger);
        string destinationPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "Temp", "test_copy.png"
        );

        // Ensure the destination doesn't exist to start
        if (File.Exists(destinationPath))
        {
            File.Delete(destinationPath);
        }

        string testFilePath = Path.Combine(_testFilesDirectory, "test.png");
        using var sourceStream = new FileStream(testFilePath, FileMode.Open);

        // Act
        await storageProvider.AddAsync(destinationPath, sourceStream, true);

        // Assert
        Assert.True(File.Exists(destinationPath));

        // Cleanup
        File.Delete(destinationPath);
    }

    [Fact]
    public async Task AddAsync_WithOverwriteFalse_ShouldThrowWhenFileExists()
    {
        // Arrange
        var storageProvider = new LocalDiskService(_logger);

        string destinationPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "Temp", "test_copy.png"
        );

        // Ensure the destination directory exists
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)
            ?? throw new NullException("Destination path cannot be null."));

        // Ensure the destination file exists to start with
        string testFilePath = Path.Combine(_testFilesDirectory, "test.png");
        File.Copy(testFilePath, destinationPath, true); // Overwrite just in case

        using var sourceStream = new FileStream(testFilePath, FileMode.Open);

        // Act & Assert
        await Assert.ThrowsAsync<IOException>(async () => await
            storageProvider.AddAsync(destinationPath, sourceStream, false)
        );

        // Cleanup
        File.Delete(destinationPath);
    }

    [Fact]
    public async Task AddAsync_WithOverwriteTrue_ShouldOverwriteExistingFile()
    {
        // Arrange
        var storageProvider = new LocalDiskService(_logger);
        string destinationPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "Temp", "test_overwrite.png"
        );

        // Ensure the destination directory exists
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)
            ?? throw new NullException("Destination path cannot be null."));

        // Ensure the destination file exists to start with
        string testFilePath = Path.Combine(_testFilesDirectory, "test.png");
        File.Copy(testFilePath, destinationPath, overwrite: true); // Overwrite just in case

        using var sourceStream = new MemoryStream(
            Encoding.UTF8.GetBytes("Overwrite content")
        );

        // Act
        await storageProvider.AddAsync(destinationPath, sourceStream, true);

        // Assert
        Assert.True(File.Exists(destinationPath));
        var content = await File.ReadAllTextAsync(destinationPath);
        Assert.Equal("Overwrite content", content);

        // Cleanup
        File.Delete(destinationPath);
    }

    [Fact]
    public async Task DeleteAsync_ShouldDeleteFile()
    {
        // Arrange
        var storageProvider = new LocalDiskService(_logger);
        string testFilePath = Path.Combine(_testFilesDirectory, "test.png");
        string destinationPath = Path.Combine(
            Directory.GetCurrentDirectory(), "Temp", "test_delete.png"
        );
        // Ensure the destination directory exists
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)
            ?? throw new NullException("Destination path cannot be null."));
        File.Copy(testFilePath, destinationPath, true);

        // Ensure the file exists before we try to delete it
        Assert.True(File.Exists(destinationPath));

        // Act
        await storageProvider.DeleteAsync(destinationPath);

        // Assert
        Assert.False(File.Exists(destinationPath));
    }

    [Fact]
    public async Task DeleteAsync_ShouldDeleteFolder()
    {
        // Arrange
        var storageProvider = new LocalDiskService(_logger);
        string testFolderPath = Path.Combine(Directory.GetCurrentDirectory(), "TempDeleteFolder");
        Directory.CreateDirectory(testFolderPath);

        // Ensure the folder exists before we try to delete it
        Assert.True(Directory.Exists(testFolderPath));

        // Act
        await storageProvider.DeleteAsync(testFolderPath);

        // Assert
        Assert.False(Directory.Exists(testFolderPath));
    }

    [Fact]
    public async Task DeleteAsync_ShouldThrowWhenPathDoesNotExist()
    {
        // Arrange
        var storageProvider = new LocalDiskService(_logger);
        string nonExistentPath = Path.Combine(Directory.GetCurrentDirectory(), "NonExistentPath");

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(
            async () => await storageProvider.DeleteAsync(nonExistentPath));
    }

    public void Dispose()
    {
        _loggerFactory?.Dispose();
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnFalseIfNotExists()
    {
        // Arrange
        var storageProvider = new LocalDiskService(_logger);

        string nonExistentFilePath = Path.Combine(_testFilesDirectory, "nonexistent.png");

        // Act
        bool exists = await storageProvider.ExistsAsync(nonExistentFilePath);

        // Assert
        Assert.False(exists);
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnTrueIfExists()
    {
        // Arrange
        var storageProvider = new LocalDiskService(_logger);

        string testFilePath = Path.Combine(_testFilesDirectory, "test.png");

        // Act
        bool exists = await storageProvider.ExistsAsync(testFilePath);

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public async Task ListAsync_ShouldReturnFilesAndFolders()
    {
        // Arrange
        var storageProvider = new LocalDiskService(_logger);

        // Act
        var items = await storageProvider.ListAsync(_testFilesDirectory);

        // Assert
        Assert.NotNull(items);
        Assert.True(items.Any()); // Should be at least one file/folder in the test directory
    }

    [Fact]
    public async Task ReadAsync_ShouldReturnFileStream()
    {
        // Arrange
        var service = new LocalDiskService();
        string filePath = Path.Combine(Directory.GetCurrentDirectory(), "test.txt");
        const string fileContent = "Hello, World!";
        File.WriteAllText(filePath, fileContent);

        string readContent;

        // Act
        using (var stream = await service.ReadAsync(filePath))
        {
            using var reader = new StreamReader(stream);
            readContent = await reader.ReadToEndAsync();
        }  // Ensure the stream is fully closed and disposed of before attempting to delete

        // Cleanup
        File.Delete(filePath);

        // Assert
        Assert.Equal(fileContent, readContent);
    }
}