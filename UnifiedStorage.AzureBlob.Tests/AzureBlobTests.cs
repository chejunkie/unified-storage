using Azure.Storage.Blobs;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnifiedStorage.Common;
using UnifiedStorage.Common.Configuration.AzureKeyVaultConfig;
using UnifiedStorage.Common.Design;
using Xunit;

namespace UnifiedStorage.AzureBlob.Tests;

public class AzureBlobTests : IDisposable
{
    private readonly ILogger<AzureBlobService> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IMemoryCache _memoryCache;
    private readonly string _testFilesDirectory;
    private readonly string _secretsDirectory;

    public AzureBlobTests()
    {
        // Configure Serilog to log to a file.
        Log.Logger = new LoggerConfiguration()
            .WriteTo.File("UnitTestLogs.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        _loggerFactory = LoggerFactory.Create(builder => builder.AddSerilog());

        _logger = _loggerFactory.CreateLogger<AzureBlobService>();

        // Dynamically determine the path to the TestFiles directory
        // based on the current project directory.
        _testFilesDirectory = Path.GetFullPath(
            Path.Combine(
                Directory.GetCurrentDirectory(),
                @"..\..\..\..\TestFiles"
        ));
        _secretsDirectory = Path.GetFullPath(
            Path.Combine(
                Directory.GetCurrentDirectory(),
                @"..\..\..\..\Secrets"
        ));

        _memoryCache = new MemoryCache(new MemoryCacheOptions());
    }

    [Fact]
    public async Task AddAsync_ShouldAddFile()
    {
        // Arrange
        var storageProvider = InitializeService();
        const string destinationPath = "unittestcontainer/test_copy.png";

        string testFilePath = Path.Combine(_testFilesDirectory, "test.png");
        using var sourceStream = new FileStream(testFilePath, FileMode.Open);

        // Act
        await storageProvider.AddAsync(destinationPath, sourceStream, true);

        // Assert
        Assert.True(await storageProvider.ExistsAsync(destinationPath));
    }

    [Fact]
    public async Task AddAsync_WithOverwriteFalse_ShouldThrowWhenFileExists()
    {
        // Arrange
        var storageProvider = InitializeService();
        const string destinationPath = "unittestcontainer/test_copy.png";

        string testFilePath = Path.Combine(_testFilesDirectory, "test.png");
        using var sourceStream = new FileStream(testFilePath, FileMode.Open);

        // Ensure the file exists to begin with
        await storageProvider.AddAsync(destinationPath, sourceStream, true);
        sourceStream.Position = 0; // Reset the stream position for reuse

        // Act & Assert
        await Assert.ThrowsAsync<IOException>(
            async () => await storageProvider.AddAsync(destinationPath, sourceStream, false)
        );
    }

    [Fact]
    public async Task AddAsync_WithOverwriteTrue_ShouldOverwriteExistingFile()
    {
        // Arrange
        var storageProvider = InitializeService();
        const string destinationPath = "unittestcontainer/test_overwrite.png";
        byte[] initialContent = Encoding.UTF8.GetBytes("Initial Content");
        byte[] overwriteContent = Encoding.UTF8.GetBytes("Overwrite Content");

        using var initialStream = new MemoryStream(initialContent);
        await storageProvider.AddAsync(destinationPath, initialStream, true);

        // Act
        using var overwriteStream = new MemoryStream(overwriteContent);
        await storageProvider.AddAsync(destinationPath, overwriteStream, true);

        // Assert: Read back the content and ensure it matches the "overwriteContent"
        var memoryStream = new MemoryStream();
        await storageProvider.ReadAsync(destinationPath, memoryStream);
        var writtenContent = Encoding.UTF8.GetString(memoryStream.ToArray());

        Assert.Equal("Overwrite Content", writtenContent);
    }

    [Fact]
    public async Task DeleteAsync_ShouldDeleteFileIfExists()
    {
        // Arrange
        var service = InitializeService();
        string filePath = Path.Combine(_testFilesDirectory, "test.png");
        using var fileStream = File.OpenRead(filePath);
        const string remotePath = "UnitTest/test.png";
        string result = await service.AddAsync(remotePath, fileStream, true);

        // Act
        await service.DeleteAsync(remotePath);
        var existsAfterDelete = await service.ExistsAsync(remotePath);

        // Assert
        Assert.False(existsAfterDelete);
    }

    [Fact]
    public async Task DeleteAsync_ShouldThrowWhenFileDoesNotExist()
    {
        // Arrange
        var service = InitializeService();
        // Add file to force creation of UnitTest container
        string filePath = Path.Combine(_testFilesDirectory, "test.png");
        using var fileStream = File.OpenRead(filePath);
        const string remotePath = "UnitTest/test.png";
        string result = await service.AddAsync(remotePath, fileStream, true);
        const string nonExistentRemotePath = "UnitTest/nonExistent.png";

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            service.DeleteAsync(nonExistentRemotePath)
        );

        // Cleanup
        await service.DeleteAsync(remotePath);
    }

    public void Dispose()
    {
        _loggerFactory?.Dispose();
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnFalseIfNotExists()
    {
        // Arrange
        var storageProvider = InitializeService();
        const string nonExistentPath = "unittestcontainer/nonexistentfile.png";

        // Act
        bool exists = await storageProvider.ExistsAsync(nonExistentPath);

        // Assert
        Assert.False(exists);
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnTrueIfExists()
    {
        // Arrange
        var storageProvider = InitializeService();
        const string existingPath = "unittestcontainer/existingfile.png";
        byte[] content = Encoding.UTF8.GetBytes("Test Content");

        using var stream = new MemoryStream(content);
        await storageProvider.AddAsync(existingPath, stream, true);

        // Act
        bool exists = await storageProvider.ExistsAsync(existingPath);

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public async Task ListAsync_ShouldReturnFilesAndFolders()
    {
        // Arrange
        var storageProvider = InitializeService();
        const string path = "unittestcontainer";

        // Act
        var items = await storageProvider.ListAsync(path);

        // Assert
        Assert.NotNull(items);
        Assert.NotEmpty(items);
    }

    [Fact]
    public async Task ReadAsync_ShouldReturnAzureBlobStream()
    {
        // Arrange
        var service = InitializeService();
        string filePath = Path.Combine(_testFilesDirectory, "test.txt");
        using var fileStream = File.OpenRead(filePath);
        const string remotePath = "UnitTest/test.txt";
        await service.AddAsync(remotePath, fileStream, true);

        string readContent;

        // Act
        using (var stream = await service.ReadAsync(remotePath))
        {
            using var reader = new StreamReader(stream);
            readContent = await reader.ReadToEndAsync();
        }

        // Cleanup
        await service.DeleteAsync(remotePath);

        // Assert
        Assert.Equal("Blah blah blah", readContent);
    }

    private AzureBlobService InitializeService()
    {
        var secretProviderLogger =
            _loggerFactory.CreateLogger<AzureKeyVaultSecretProvider>();

        string jsonConfigPath = Path.Combine(_secretsDirectory,"secrets.json");
        var config = new ConfigLoader(jsonConfigPath).LoadSecretsFromConfig();

        var secretProvider = new AzureKeyVaultSecretProvider(
            config.Common.VaultUrl, config.Common.TenantId,
            config.Common.ClientId, config.Common.ClientSecret,
            secretProviderLogger, _memoryCache
        );

        // Get the connection string or secret for Google Drive from Azure Key Vault
        string connection = secretProvider
            .GetSecretAsync(config
                .Storages["AzureBlob"]
                .SecretName
            )
            .GetAwaiter()
            .GetResult();

        // Create the DriveService with the fetched connection (or credential) string
        var endpoint = new BlobServiceClient(connection);

        return new AzureBlobService(endpoint, _logger);
    }
}