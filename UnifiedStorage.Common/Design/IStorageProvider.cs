using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace UnifiedStorage.Common.Design;

/// <summary>
/// Defines methods to interact with a storage system,
/// allowing for operations like adding, checking existence, and listing items.
/// </summary>
public interface IStorageProvider
{
    /// <summary>
    /// Asynchronously adds a file to the storage at the specified path.
    /// </summary>
    /// <param name="path">The path where the file should be stored.</param>
    /// <param name="stream">The data stream of the file.</param>
    /// <param name="overwrite">If set to <c>true</c>, overwrites an existing file at the path. Defaults to <c>false</c>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    Task<string> AddAsync(string path, Stream stream, bool overwrite);

    /// <summary>
    /// Deletes the specified file or folder asynchronously.
    /// </summary>
    /// <param name="path">The path to the file or folder to delete.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown if the provided path is null or empty.</exception>
    /// <exception cref="FileNotFoundException">Thrown if no file or directory is found with the specified path.</exception>
    Task DeleteAsync(string path);

    /// <summary>
    /// Asynchronously checks if a file or directory exists at the specified path in the storage system.
    /// </summary>
    /// <param name="path">The path to check for existence.</param>
    /// <returns>A <see cref="Task"/> that on completion returns <c>true</c> if the path exists, <c>false</c> otherwise.</returns>
    Task<bool> ExistsAsync(string path);

    /// <summary>
    /// Asynchronously lists all the items (files and/or directories) at the specified path in the storage system.
    /// </summary>
    /// <param name="path">The path of the directory or location to list items from.</param>
    /// <returns>A <see cref="Task"/> that on completion returns a list of items at the specified path.</returns>
    Task<IEnumerable<IStorageItem>> ListAsync(string path);

    /// <summary>
    /// Reads the content of a file as a stream.
    /// </summary>
    /// <param name="path">The path to the file.</param>
    /// <returns>A stream containing the content of the file.</returns>
    Task<Stream> ReadAsync(string path);

    #region Possible Extensions

    /// <summary>
    /// Retrieves metadata for a file or folder.
    /// </summary>
    /// <param name="path">The path of the file or folder.</param>
    /// <returns>A dictionary containing metadata key-value pairs.</returns>
    //TODO: Task<Dictionary<string, string>> GetMetadataAsync(string path);

    /// <summary>
    /// Updates metadata for a file or folder.
    /// </summary>
    /// <param name="path">The path of the file or folder.</param>
    /// <param name="metadata">The metadata to update.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    //TODO: Task UpdateMetadataAsync(string path, Dictionary<string, string> metadata);

    /// <summary>
    /// Renames or moves a file or folder to a new location.
    /// </summary>
    /// <param name="currentPath">The current path of the file or folder.</param>
    /// <param name="newPath">The new path for the file or folder.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    //TODO: Task RenameOrMoveAsync(string currentPath, string newPath);

    /// <summary>
    /// Writes data to a specified location.
    /// </summary>
    /// <param name="path">The path where data should be written.</param>
    /// <param name="stream">The data stream to write.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    //TODO: Task WriteAsync(string path, Stream stream);

    #endregion Possible Extensions
}