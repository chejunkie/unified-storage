using Google.Apis.Drive.v3;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnifiedStorage.Common;
using UnifiedStorage.Common.Configuration.AzureKeyVaultConfig;
using UnifiedStorage.Common.Design;
using Xunit;

namespace UnifiedStorage.GoogleDrive.Tests;

public class GoogleDriveTests : IDisposable
{
    private readonly IStorageEndpointFactory<DriveService> _factory;
    private readonly ILogger<GoogleDriveService> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IMemoryCache _memoryCache;
    private readonly string _testFilesDirectory;
    private readonly string _secretsDirectory;

    public GoogleDriveTests()
    {
        // Configure Serilog to log to a file.
        Log.Logger = new LoggerConfiguration()
            .WriteTo.File("UnitTestLogs.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        _loggerFactory = LoggerFactory
            .Create(builder =>
                builder.AddSerilog()
            );

        _logger = _loggerFactory.CreateLogger<GoogleDriveService>();

        // Dynamically determine the path to the TestFiles directory
        // based on the current project directory.
        _testFilesDirectory = Path.GetFullPath(
            Path.Combine(
                Directory.GetCurrentDirectory(),
                @"..\..\..\..\TestFiles"
            )
        );
        _secretsDirectory = Path.GetFullPath(
            Path.Combine(
                Directory.GetCurrentDirectory(),
                @"..\..\..\..\Secrets"
        ));

        _factory = new DriveServiceFactory();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
    }

    [Fact]
    public async Task AddAsync_ShouldAddFile()
    {
        // Arrange
        var service = InitializeDriveService();
        string filePath = Path.Combine(_testFilesDirectory, "test.png");
        using var fileStream = File.OpenRead(filePath);
        const string remotePath = "UnitTest/test.png";

        // Act
        string uploadedFileLink = await service
            .AddAsync(remotePath, fileStream, overwrite: true);

        // Assert
        Assert.True(await service.ExistsAsync(remotePath));

        // Cleanup
        await service.DeleteAsync(remotePath);
    }

    [Fact]
    public async Task AddAsync_WithOverwriteFalse_ShouldThrowWhenFileExists()
    {
        // Arrange
        var service = InitializeDriveService();
        string filePath = Path.Combine(_testFilesDirectory, "test.png");
        using var fileStream = File.OpenRead(filePath);
        const string remotePath = "UnitTest/test.png";

        // Act
        await service.AddAsync(remotePath, fileStream, overwrite: true);

        // Assert
        await Assert.ThrowsAsync<IOException>(() =>
            service.AddAsync(remotePath, fileStream, false));
    }

    [Fact]
    public async Task AddAsync_WithOverwriteTrue_ShouldOverwriteExistingFile()
    {
        // Arrange
        var service = InitializeDriveService();
        string filePath = Path.Combine(_testFilesDirectory, "test.png");
        using var fileStream1 = File.OpenRead(filePath);
        const string remotePath = "UnitTest/test.png";
        await service.AddAsync(remotePath, fileStream1, true); // Ensure it exists

        // Act
        using var fileStream2 = File.OpenRead(filePath);
        await service.AddAsync(remotePath, fileStream2, true); // Try overwriting

        // Assert
        Assert.True(await service.ExistsAsync(remotePath));
    }

    [Fact]
    public async Task DeleteAsync_ShouldDeleteFile()
    {
        // Arrange
        var service = InitializeDriveService();
        string testFilePath = Path.Combine(_testFilesDirectory, "test.png");
        using var stream = new FileStream(testFilePath, FileMode.Open);

        string uploadedFileLink = await service
            .AddAsync("test_delete.png", stream, overwrite: true);

        // Ensure the file exists before we try to delete it
        Assert.True(await service.ExistsAsync("test_delete.png"));

        // Act
        await service.DeleteAsync("test_delete.png");

        // Assert
        Assert.False(await service.ExistsAsync("test_delete.png"));
    }

    [Fact]
    public async Task DeleteAsync_ShouldThrowWhenFileDoesNotExist()
    {
        // Arrange
        var googleDriveService = InitializeDriveService();
        string nonExistentFileId = "someNonExistentFileId";

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(
            async () => await googleDriveService.DeleteAsync(nonExistentFileId));
    }

    public void Dispose()
    {
        _loggerFactory?.Dispose();
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnFalseIfNotExists()
    {
        // Arrange
        var service = InitializeDriveService();
        const string remotePath = "UnitTest/nonexistent.png";

        // Act
        bool exists = await service.ExistsAsync(remotePath);

        // Assert
        Assert.False(exists);
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnTrueIfExists()
    {
        // Arrange
        var service = InitializeDriveService();
        string filePath = Path.Combine(_testFilesDirectory, "test.png");
        using var fileStream = File.OpenRead(filePath);
        const string remotePath = "UnitTest/test.png";
        string sharableLink = await service.AddAsync(remotePath, fileStream, true);

        // Act
        bool exists = await service.ExistsAsync(remotePath);

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public async Task ListAsync_ShouldReturnFilesAndFolders()
    {
        // Arrange
        var service = InitializeDriveService();
        const string folderPath = "UnitTest";

        // Act
        var items = await service.ListAsync(folderPath);

        // Assert
        Assert.True(items.Any());
    }

    [Fact]
    public async Task ReadAsync_ShouldReturnGoogleDriveFileStream()
    {
        // Arrange
        var service = InitializeDriveService();
        string filePath = Path.Combine(_testFilesDirectory, "test.png");
        using var fileStream = File.OpenRead(filePath);
        const string remotePath = "UnitTest/test.png";
        await service.AddAsync(remotePath, fileStream, true);

        // Act
        using var readStream = await service.ReadAsync(remotePath);
        var isNotEmpty = readStream.Length > 0;

        // Cleanup
        await service.DeleteAsync(remotePath);

        // Assert
        Assert.True(isNotEmpty);
    }

    private GoogleDriveService InitializeDriveService()
    {
        var secretProviderLogger = _loggerFactory
            .CreateLogger<AzureKeyVaultSecretProvider>();

        string jsonConfigPath = Path.Combine(_secretsDirectory, "secrets.json");
        var config = new ConfigLoader(jsonConfigPath).LoadSecretsFromConfig();

        var secretProvider = new AzureKeyVaultSecretProvider(
            config.Common.VaultUrl, config.Common.TenantId,
            config.Common.ClientId, config.Common.ClientSecret,
            secretProviderLogger, _memoryCache
        );

        // Get the connection string or secret for Google Drive from Azure Key Vault
        string connection = secretProvider
            .GetSecretAsync(config
                .Storages["GoogleDrive"]
                .SecretName
            )
            .GetAwaiter()
            .GetResult();

        // Create the DriveService with the fetched connection (or credential) string
        var driveService = _factory.CreateEndpoint(connection);
        return new GoogleDriveService(driveService, _logger);
    }
}