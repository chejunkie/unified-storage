using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnifiedStorage.Common.Constants;
using UnifiedStorage.Common.Design;
using UnifiedStorage.Common.Models;

namespace UnifiedStorage.LocalDisk;

public class LocalDiskService : IStorageProvider
{
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalDiskService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public LocalDiskService(
        ILogger<LocalDiskService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public LocalDiskService()
        : this(NullLogger<LocalDiskService>.Instance)
    {
    }

    /// <summary>
    /// Adds a file to the local disk at the specified path, 
    /// with an option to overwrite an existing file.
    /// </summary>
    /// <param name="path">The path where the file should be stored.</param>
    /// <param name="stream">The data stream of the file.</param>
    /// <param name="overwrite">If set to <c>true</c>, overwrites an existing file at the path. Defaults to <c>false</c>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
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
            // Check if the file exists and if overwrite is not allowed
            bool fileExists = await ExistsAsync(path);
            if (!overwrite && fileExists)
            {
                _logger.LogWarning(
                    "File with path {path} already exists " +
                    "and overwrite is not allowed.",
                    path
                );
                throw new IOException(
                    $"File {path} already exists."
                );
            }

            // Create directory if it does not already exist
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) &&
                !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                _logger.LogInformation(
                    "Created directory: {directory}",
                    directory
                );
            }

            // Write data to the local disk
            using FileStream fileStream = new(
                path, FileMode.Create, FileAccess.Write, FileShare.None,
                bufferSize: 8192, FileOptions.Asynchronous
            );
            await stream.CopyToAsync(fileStream);
            _logger.LogInformation(
                "Successfully wrote to: {path}",
                path
            );
            return path;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error writing to path: {path}",
                path
            );
            throw;
        }
    }


    /// <inheritdoc/>
    public Task DeleteAsync(string path)
    {
        return Task.Run(() =>
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

            if (File.Exists(path))
            {
                File.Delete(path);
            }
            else if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
            else
            {
                _logger.LogWarning(
                    "The specified path does not exist: {path}",
                    path
                );
                throw new FileNotFoundException(
                    $"No file or directory found with the specified path: {path}"
                );
            }
        });
    }

    /// <summary>
    /// Checks if a file or directory exists at the specified path.
    /// </summary>
    /// <param name="path">The path to check for existence.</param>
    /// <returns>A <see cref="Task"/> that on completion returns <c>true</c> if the path exists, <c>false</c> otherwise.</returns>
    public Task<bool> ExistsAsync(string path)
    {
        // Use Task.FromResult since File.Exists is synchronous
        return Task.FromResult(File.Exists(path));
    }

    /// <summary>
    /// Lists all the files and folders in the specified directory.
    /// </summary>
    /// <param name="path">The path of the directory to list items from.</param>
    /// <returns>A <see cref="Task"/> that on completion returns a list of items in the specified directory.</returns>
    public async Task<IEnumerable<IStorageItem>> ListAsync(
        string path)
    {
        List<StorageItem> items = new List<StorageItem>();

        if (string.IsNullOrWhiteSpace(path))
        {
            _logger.LogError(
                "The provided path is null or empty."
            );
            throw new ArgumentNullException(nameof(path),
                "Path cannot be null or empty."
            );
        }

        if (!Directory.Exists(path))
        {
            _logger.LogError(
                "The specified directory does not exist: {path}",
                path
            );
            throw new DirectoryNotFoundException(
                $"Directory not found: {path}"
            );
        }

        await Task.Yield(); // This ensures method runs asynchronously,
                            // as directory operations are synchronous.

        try
        {
            // Get directories
            var directories = Directory.GetDirectories(path);
            items.AddRange(directories.Select(directory => new StorageItem
            {
                // Extract the last folder name from the full path
                Name = Path.GetFileName(directory),
                Type = StorageItemType.Folder
            }));

            // Get files
            var files = Directory.GetFiles(path);
            items.AddRange(files.Select(file => new StorageItem
            {
                // Extract the file name from the full path
                Name = Path.GetFileName(file),
                Type = StorageItemType.File
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error listing items in local disk for path: {path}",
                path
            );
            throw;
        }

        return items;
    }

    /// <summary>
    /// Reads the content of a file from the local disk and returns it as a stream.
    /// </summary>
    /// <param name="path">The relative path to the file from the root directory.</param>
    /// <returns>A stream containing the content of the file.</returns>
    /// <exception cref="System.IO.FileNotFoundException">Thrown when the file is not found.</exception>
    /// <exception cref="System.Exception">Thrown when an error occurs during reading.</exception>
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
            var fileStream = new FileStream(path,
                FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 8192, useAsync: true
            );
            return fileStream;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error reading file from local disk: {path}",
                path
            );
            throw;
        }
    }
}