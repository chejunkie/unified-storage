using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnifiedStorage.Common.Constants;
using UnifiedStorage.Common.Design;
using UnifiedStorage.Common.Models;

namespace UnifiedStorage.AzureBlob;

public class AzureBlobService : IStorageProvider
{
    private readonly BlobServiceClient _endpoint;
    private readonly ILogger _logger;

    public AzureBlobService(
        BlobServiceClient endpoint,
        ILogger<AzureBlobService> logger)
    {
        _endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public AzureBlobService(
        BlobServiceClient endpoint)
        : this(endpoint, NullLogger<AzureBlobService>.Instance)
    {
    }

    /// <summary>
    /// Adds or overwrites content in Azure Blob storage at the specified path.
    /// </summary>
    /// <param name="path">The path in Azure Blob storage where content should be added or overwritten.</param>
    /// <param name="stream">The data stream to upload.</param>
    /// <param name="overwrite">Indicates whether to overwrite the blob if it already exists. Defaults to <c>false</c>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the provided path is null or empty.</exception>
    /// <exception cref="ArgumentException">Thrown when the provided path is invalid.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the blob already exists and overwrite is not allowed.</exception>
    public async Task<string> AddAsync(
        string path,
        Stream stream,
        bool overwrite = false)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            _logger.LogError(
                "The provided path is null or empty."
            );
            throw new ArgumentNullException(nameof(path),
                "Path cannot be null or empty."
            );
        }

        try
        {
            var (containerName, blobName) = ParsePath(path);

            // Create or get the container reference
            BlobContainerClient containerClient = _endpoint
                .GetBlobContainerClient(containerName);

            if (!await containerClient.ExistsAsync())
            {
                await containerClient.CreateIfNotExistsAsync();
                _logger.LogInformation(
                    "Created or fetched container: {containerName}",
                    containerName
                );
            }

            // Upload the blob
            BlobClient blobClient = containerClient.GetBlobClient(blobName);
            if (overwrite || !await blobClient.ExistsAsync())
            {
                await blobClient.UploadAsync(stream, overwrite: true);
                _logger.LogInformation(
                    "Successfully uploaded to Azure Blob: {path}",
                    path
                );

                return blobClient.Uri.ToString();
            }

            _logger.LogWarning(
                "Blob with path {path} already exists " +
                "and overwrite is not allowed.",
                path
            );
            throw new IOException(
                $"Blob {path} already exists."
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error uploading to Azure Blob path: {path}",
                path
            );
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(string path)
    {
        var (containerName, blobName) = ParsePath(path);

        BlobContainerClient containerClient = _endpoint
            .GetBlobContainerClient(containerName);

        if (!await containerClient.ExistsAsync())
        {
            _logger.LogWarning(
                "Container does not exist: {containerName}",
                containerName
            );
            throw new InvalidOperationException(
                $"Container not found: {containerName}"
            );
        }

        BlobClient blobClient = containerClient.GetBlobClient(blobName);

        if (!await blobClient.ExistsAsync())
        {
            _logger.LogWarning(
                "Blob does not exist: {path}",
                path
            );
            throw new FileNotFoundException(
                $"Blob not found: {path}"
            );
        }

        try
        {
            await blobClient.DeleteAsync();
            _logger.LogInformation(
                "Successfully deleted blob: {path}",
                path
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error deleting blob: {path}",
                path
            );
            throw;
        }
    }

    /// <summary>
    /// Checks if a specified path exists in Azure Blob storage.
    /// </summary>
    /// <param name="path">The blob path to check.</param>
    /// <returns>A <see cref="Task{TResult}"/> that represents the asynchronous operation.
    /// The task result contains a boolean indicating if the blob exists.</returns>
    public async Task<bool> ExistsAsync(
        string path)
    {
        var (containerName, blobName) = ParsePath(path);

        BlobContainerClient containerClient = _endpoint.GetBlobContainerClient(containerName);
        BlobClient blobClient = containerClient.GetBlobClient(blobName);

        return await blobClient.ExistsAsync();
    }

    /// <summary>
    /// Lists items (both files and folders) in a specified path in Azure Blob storage.
    /// </summary>
    /// <param name="path">The path to list items from.</param>
    /// <returns>A <see cref="Task{TResult}"/> that represents the asynchronous operation.
    /// The task result contains an enumerable of storage items.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the provided path is null or empty.</exception>
    /// <exception cref="ArgumentException">Thrown when the provided path is invalid.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the specified container does not exist.</exception>
    public async Task<IEnumerable<IStorageItem>> ListAsync(string path)
    {
        List<StorageItem> items = new();

        if (string.IsNullOrWhiteSpace(path))
        {
            _logger.LogError(
                "The provided path is null or empty."
            );
            throw new ArgumentNullException(nameof(path),
                "Path cannot be null or empty."
            );
        }

        // Split the path into container and blob name
        string[] pathSegments = path.Split('/',
            StringSplitOptions.RemoveEmptyEntries
        );

        if (pathSegments.Length == 0)
        {
            throw new ArgumentException(
                "Invalid path.",
                nameof(path)
            );
        }

        // The first segment is assumed to be the container name
        // The rest of the segments form the blob prefix
        string containerName = pathSegments[0];
        string? blobPrefix = pathSegments.Length > 1
            ? string.Join('/', pathSegments.Skip(1)) + "/"
            : null;

        BlobContainerClient containerClient =
            _endpoint.GetBlobContainerClient(containerName);

        if (!await containerClient.ExistsAsync())
        {
            _logger.LogError(
                "The specified container does not exist: {containerName}",
                containerName
            );
            throw new InvalidOperationException(
                $"Container {containerName} does not exist."
            );
        }

        try
        {
            await foreach (var blobItem in containerClient
                .GetBlobsByHierarchyAsync(delimiter: "/", prefix: blobPrefix))
            {
                if (blobItem.IsBlob)
                {
                    items.Add(new StorageItem
                    {
                        Name = blobItem.Blob.Name,
                        Type = StorageItemType.File
                    });
                }
                else
                {
                    items.Add(new StorageItem
                    {
                        Name = blobItem.Prefix.TrimEnd('/'),
                        Type = StorageItemType.Folder
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error listing items in Azure Blob for path: {path}",
                path
            );
            throw;
        }

        return items;
    }

    /// <summary>
    /// Reads the content of a blob from Azure Blob Storage and returns it as a stream.
    /// </summary>
    /// <param name="path">The path to the blob on Azure Blob Storage.</param>
    /// <returns>A stream containing the content of the blob.</returns>
    /// <exception cref="Azure.RequestFailedException">Thrown when the blob is not found or when an error occurs during reading.</exception>
    public async Task<Stream> ReadAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            _logger.LogError(
                "The provided path is null or empty."
            );
            throw new ArgumentNullException(nameof(path),
                "Path cannot be null or empty."
            );
        }

        try
        {
            var (containerName, blobName) = ParsePath(path);
            var blobClient = _endpoint
                .GetBlobContainerClient(containerName)
                .GetBlobClient(blobName);

            BlobDownloadInfo blobDownloadInfo = await blobClient.DownloadAsync();

            return blobDownloadInfo.Content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error reading file from Azure Blob: {path}",
                path
            );
            throw;
        }
    }

    /// <summary>
    /// Reads the content of the specified blob into the provided destination stream.
    /// </summary>
    /// <param name="path">The path to the blob, which includes both the container name and the blob name.</param>
    /// <param name="destinationStream">The stream into which the blob content will be read. This stream must be writable.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the provided path is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the specified container or blob does not exist.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the specified blob is not found.</exception>
    public async Task ReadAsync(
        string path,
        Stream destinationStream)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            _logger.LogError(
                "The provided path is null or empty."
            );
            throw new ArgumentNullException(nameof(path),
                "Path cannot be null or empty."
            );
        }

        try
        {
            var (containerName, blobName) = ParsePath(path);

            // Get the container reference
            BlobContainerClient containerClient
                = _endpoint.GetBlobContainerClient(containerName);

            if (!await containerClient.ExistsAsync())
            {
                _logger.LogError(
                    "The specified container does not exist: {containerName}",
                    containerName
                );
                throw new InvalidOperationException(
                    $"Container {containerName} does not exist."
                );
            }

            // Get the blob client
            BlobClient blobClient = containerClient.GetBlobClient(blobName);

            if (!await blobClient.ExistsAsync())
            {
                _logger.LogError(
                    "The specified blob does not exist: {path}",
                    path
                );
                throw new FileNotFoundException(
                    $"Blob not found: {path}"
                );
            }

            // Download blob content into the provided destination stream
            var response = await blobClient.DownloadAsync();
            await response.Value.Content.CopyToAsync(destinationStream);
            destinationStream.Position = 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error reading from Azure Blob path: {path}",
                path
            );
            throw;
        }
    }

    /// <summary>
    /// Parses the provided path into container name and blob name.
    /// </summary>
    /// <param name="path">The path to parse.</param>
    /// <returns>A tuple containing the container name and blob name.</returns>
    /// <exception cref="ArgumentException">Thrown when the provided path is invalid.</exception>
    private static (string containerName, string blobName) ParsePath(
        string path)
    {
        string[] pathSegments = path.Split('/',
            StringSplitOptions.RemoveEmptyEntries
        );
        if (pathSegments.Length == 0)
        {
            throw new ArgumentException(
                "Invalid path.",
                nameof(path)
            );
        }

        // The first segment is assumed to be the container name
        // The rest of the segments form the blob name
        string containerName = pathSegments[0].ToLower();
        string blobName = string.Join('/', pathSegments.Skip(1));

        return (containerName, blobName);
    }
}