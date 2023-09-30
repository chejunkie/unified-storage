using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnifiedStorage.Common.Constants;
using UnifiedStorage.Common.Design;
using UnifiedStorage.GoogleDrive.Models;
using GoogleFile = Google.Apis.Drive.v3.Data.File;

namespace UnifiedStorage.GoogleDrive;

public class GoogleDriveService : IStorageProvider
{
    // Constants for Mime Types
    private const string AppOctetStreamMimeType = "application/octet-stream";

    private const string FolderMimeType = "application/vnd.google-apps.folder";

    private readonly DriveService _driveService;
    private readonly ILogger<GoogleDriveService> _logger;

    public GoogleDriveService(
        DriveService driveService,
        ILogger<GoogleDriveService> logger)
    {
        _driveService = driveService ??
            throw new ArgumentNullException(nameof(driveService));
        _logger = logger ??
            throw new ArgumentNullException(nameof(logger));
    }

    public GoogleDriveService(
        DriveService driveService)
        : this(driveService, NullLogger<GoogleDriveService>.Instance)
    {
    }

    public int DeleteBatchThreshold { get; set; } = 50;

    /// <summary>
    /// Adds a file to Google Drive at the specified path, with an option to overwrite an existing file.
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

        string shareableLink;

        try
        {
            var (currentParent, fileName) = await
                EnsureDirectoriesExistAndGetFileParent(path);

            // Check if a file with the same name already exists
            bool fileExists = await ExistsAsync(currentParent, fileName);

            if (fileExists && !overwrite)
            {
                _logger.LogWarning(
                    "File with path {path} already exists and overwrite is not allowed.",
                    path
                );
                throw new IOException(
                    $"File {path} already exists."
                );
            }
            else if (fileExists)
            {
                await DeleteFileAsync(currentParent, fileName);
                _logger.LogInformation(
                    "Overwritten existing file in Google Drive: {path}",
                    path
                );
            }

            // Upload the file to Google Drive
            var fileMetadata = new GoogleFile
            {
                Name = fileName,
                Parents = new List<string>
            {
                currentParent
            }
            };

            var request = _driveService.Files
                .Create(
                    fileMetadata,
                    stream,
                    AppOctetStreamMimeType
                );
            var uploadProgress = await request.UploadAsync();

            if (uploadProgress.Exception != null)
            {
                _logger.LogError(uploadProgress.Exception,
                    "Error during file upload to Google Drive."
                );
                throw uploadProgress.Exception;
            }

            var uploadedFile = request.ResponseBody;

            // Set sharing permission to allow anyone with the link to view the file
            var permission = new Permission
            {
                Type = "anyone",
                Role = "reader"
            };
            await _driveService.Permissions
                .Create(permission, uploadedFile.Id)
                .ExecuteAsync();

            // Construct shareable link
            shareableLink = $"https://drive.google.com/file/d/{uploadedFile.Id}/view";
            _logger.LogInformation(
                "Successfully uploaded to Google Drive " +
                "with shareable link: {shareableLink}",
                shareableLink
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error uploading to Google Drive path: {path}",
                path
            );
            throw;
        }

        return shareableLink;
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(string path)
    {
        var fileIds = await GetFileIdsFromPath(path);
        if (fileIds.Count == 0)
        {
            _logger.LogWarning(
                "File not found on Google Drive for the given path: {path}",
                path
            );
            throw new FileNotFoundException(
                $"File not found on Google Drive: {path}"
            );
        }

        if (fileIds.Count <= DeleteBatchThreshold)
        {
            var deleteTasks = fileIds.Select(fileId => DeleteFileByIdAsync(fileId));
            await Task.WhenAll(deleteTasks);
        }
        else
        {
            // If there are more than the threshold, delete in batches
            for (int i = 0; i < fileIds.Count; i += DeleteBatchThreshold)
            {
                var batch = fileIds.Skip(i).Take(DeleteBatchThreshold);
                var deleteTasks = batch.Select(fileId => DeleteFileByIdAsync(fileId));
                await Task.WhenAll(deleteTasks);
            }
        }
    }

    /// <summary>
    /// Determines whether a file or folder exists at the specified path in Google Drive.
    /// </summary>
    /// <param name="path">The path to check.</param>
    /// <returns><c>true</c> if the file or folder exists; otherwise, <c>false</c>.</returns>
    public async Task<bool> ExistsAsync(
        string path)
    {
        // Split the path into parent and name
        string[] pathSegments = path.Split('/',
            StringSplitOptions.RemoveEmptyEntries
        );

        if (pathSegments.Length == 0)
        {
            return false;
        }

        // The last segment is considered as the file or folder name
        string name = pathSegments[^1];

        // All segments except the last one form the path, you can join them back if needed
        string parentPath = string.Join('/',
            pathSegments.Take(pathSegments.Length - 1)
        );
        string parent = await GetParentId(parentPath);

        return await ExistsAsync(parent, name);
    }

    /// <summary>
    /// Lists the items (files and folders) at the specified path in Google Drive.
    /// </summary>
    /// <param name="path">The path to list items from.</param>
    /// <returns>An enumerable of storage items representing files and folders at the specified path.</returns>
    public async Task<IEnumerable<IStorageItem>> ListAsync(
        string path)
    {
        List<GoogleStorageItem> items = new();

        try
        {
            string currentParentId = "root"; // Assume starting from root

            // If path provided is not root, get its ID
            if (!string.IsNullOrWhiteSpace(path) && path != "root")
            {
                currentParentId = await GetParentId(path);
            }

            var request = _driveService.Files.List();
            request.Q = $"'{currentParentId}' in parents";
            request.Fields = "files(id, name, mimeType)";
            request.PageSize = 100;

            var files = await request.ExecuteAsync();
            foreach (var file in files.Files)
            {
                var storageItem = new GoogleStorageItem
                {
                    Name = file.Name,
                    Type = file.MimeType == FolderMimeType
                        ? StorageItemType.Folder
                        : StorageItemType.File,
                    Id = file.Id,
                    ParentId = currentParentId
                };
                items.Add(storageItem);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error listing items in Google Drive for path: {path}",
                path
            );
            throw;
        }

        return items;
    }

    /// <summary>
    /// Reads the content of a file from Google Drive and returns it as a stream.
    /// </summary>
    /// <param name="path">The path to the file on Google Drive.</param>
    /// <returns>A stream containing the content of the file.</returns>
    /// <exception cref="System.IO.FileNotFoundException">Thrown when the file is not found on Google Drive.</exception>
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
            string fileId = await GetFileIdFromPath(path);

            var stream = new MemoryStream();
            await _driveService.Files.Get(fileId).DownloadAsync(stream);

            // Reset the stream's position to 0 before returning
            stream.Position = 0;
            return stream;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error reading file from Google Drive: {path}",
                path
            );
            throw;
        }
    }

    private async Task DeleteFileAsync(
            string currentParent,
            string fileName)
    {
        var existingFileRequest = _driveService.Files.List();
        existingFileRequest.Q = @$"
            mimeType!='{FolderMimeType}'
            and '{currentParent}' in parents
            and name='{fileName}'
        ";
        var existingFiles = await existingFileRequest.ExecuteAsync();
        var fileToDeleteId = existingFiles.Files[0].Id;

        await _driveService.Files
            .Delete(fileToDeleteId)
            .ExecuteAsync();
    }

    private async Task DeleteFileByIdAsync(string fileId)
    {
        if (string.IsNullOrEmpty(fileId))
        {
            throw new ArgumentNullException(nameof(fileId),
                "File ID cannot be null or empty."
            );
        }

        try
        {
            await _driveService.Files.Delete(fileId).ExecuteAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error deleting file with ID {fileId} on Google Drive",
                fileId
            );
            throw;
        }
    }

    private async Task<(string currentParent, string fileName)> EnsureDirectoriesExistAndGetFileParent(string path)
    {
        string currentParent = "root";
        string[] pathSegments = path.Split('/',
            StringSplitOptions.RemoveEmptyEntries
        );

        // Traverse or create folders
        // Exclude the last segment, which is the file name
        for (int i = 0; i < pathSegments.Length - 1; i++)
        {
            currentParent = await
                EnsureDirectoryExistsAndGetParent(
                    currentParent,
                    pathSegments[i]
                );
        }

        return (currentParent, pathSegments[^1]);
    }

    private async Task<string> EnsureDirectoryExistsAndGetParent(
            string currentParent,
            string directoryName)
    {
        var folderRequest = _driveService.Files.List();
        folderRequest.Q = @$"
            mimeType='{FolderMimeType}'
            and '{currentParent}' in parents
            and name='{directoryName}'
        ";

        var files = await folderRequest.ExecuteAsync();

        // If folder doesn't exist, create it
        if (files.Files.Count == 0)
        {
            var folderMetadata = new GoogleFile
            {
                Name = directoryName,
                MimeType = FolderMimeType,
                Parents = new List<string> { currentParent }
            };

            var folder = await _driveService.Files
                .Create(folderMetadata)
                .ExecuteAsync();

            return folder.Id;
        }
        return files.Files[0].Id;
    }

    /// <summary>
    /// Determines whether a file or folder exists in Google Drive based on parent and name.
    /// </summary>
    /// <param name="parent">The parent ID where to check for the item.</param>
    /// <param name="name">The name of the file or folder.</param>
    /// <param name="isFolder">Indicates whether the item to check is a folder.</param>
    /// <returns><c>true</c> if the file or folder exists; otherwise, <c>false</c>.</returns>
    private async Task<bool> ExistsAsync(
        string parent,
        string name,
        bool isFolder = false)
    {
        var searchRequest = _driveService.Files.List();
        string query;

        if (isFolder)
        {
            query = @$"
            mimeType='{FolderMimeType}'
            and '{parent}' in parents
            and name='{name}'
        ";
        }
        else
        {
            // For files, just check by parent and name without MIME type
            query = @$"
            '{parent}' in parents
            and name='{name}'
        ";
        }

        searchRequest.Q = query;
        var files = await searchRequest.ExecuteAsync();
        return files.Files.Count > 0;
    }

    public string? ExtractFileIdFromLink(
        string link)
    {
        var match = Regex.Match(link, @"\/file\/d\/(.*?)\/view");
        if (match.Success)
        {
            return match.Groups[1].Value;
        }
        return null;
    }

    private async Task<string> GetFileIdFromPath(string path)
    {
        string currentParent = "root"; // Assume starting from root

        foreach (var segment in path.Split('/'))
        {
            var request = _driveService.Files.List();
            request.Q = @$"
                '{currentParent}' in parents
                and name='{segment}'
            ";
            request.Fields = "files(id)";
            request.PageSize = 1;

            var files = await request.ExecuteAsync();
            if (files.Files.Count == 0)
            {
                throw new FileNotFoundException(
                    $"Path segment '{segment}' not found in Google Drive."
                );
            }
            currentParent = files.Files[0].Id;
        }
        return currentParent;
    }

    public async Task<List<string>> GetFileIdsFromPath(string path)
    {
        List<string> fileIds = new();
        string currentParent = "root"; // Assume starting from root

        if (string.IsNullOrWhiteSpace(path) || path == "root")
        {
            return new List<string> { currentParent };
        }

        var segments = path.Split('/');
        for (int i = 0; i < segments.Length - 1; i++) // Exclude the last segment for now
        {
            var request = _driveService.Files.List();
            request.Q = @$"
                '{currentParent}' in parents
                and name='{segments[i]}'
            ";
            request.Fields = "files(id)";
            // Assuming a single parent isn't repeated more than 20 times
            request.PageSize = 20;

            var files = await request.ExecuteAsync();
            if (files.Files.Count == 0)
            {
                throw new FileNotFoundException(
                    $"Path segment '{segments[i]}' not found in Google Drive."
                );
            }

            currentParent = files.Files[0].Id;
        }

        // Now for the last segment (file or folder you're interested in)
        var finalRequest = _driveService.Files.List();
        finalRequest.Q = @$"
            '{currentParent}' in parents
            and name='{segments.Last()}'
        ";
        finalRequest.Fields = "files(id)";
        // Adjust if you expect more than 100 files/folders with the same name in the same directory
        finalRequest.PageSize = 100;

        var finalFiles = await finalRequest.ExecuteAsync();
        foreach (var file in finalFiles.Files)
        {
            fileIds.Add(file.Id);
        }

        return fileIds;
    }

    /// <summary>
    /// Retrieves the ID of the folder specified by the given path in Google Drive.
    /// </summary>
    /// <param name="path">The path to the folder whose ID should be retrieved.</param>
    /// <returns>The ID of the folder.</returns>
    private async Task<string> GetParentId(
        string path)
    {
        string currentParent = "root"; // Google Drive's root directory ID
        string[] pathSegments = path.Split('/',
            StringSplitOptions.RemoveEmptyEntries
        );

        for (int i = 0; i < pathSegments.Length; i++)
        {
            var folderRequest = _driveService.Files.List();
            folderRequest.Q = @$"
                mimeType='{FolderMimeType}'
                and '{currentParent}' in parents
                and name='{pathSegments[i]}'
            ";

            var files = await folderRequest.ExecuteAsync();

            // If folder doesn't exist, return null or throw an error
            if (files.Files.Count == 0)
            {
                throw new ArgumentException(
                    $"Path segment '{pathSegments[i]}' " +
                    "does not exist in Google Drive."
                );
            }

            // Set the current folder ID for the next iteration
            currentParent = files.Files[0].Id;
        }

        return currentParent;
    }

    private async Task UploadFileAsync(
        string parent,
        string fileName,
        Stream stream)
    {
        var fileMetadata = new GoogleFile
        {
            Name = fileName,
            Parents = new List<string>
            {
                parent
            }
        };

        var request = _driveService.Files
            .Create(
                fileMetadata,
                stream,
                AppOctetStreamMimeType
            );
        await request.UploadAsync();
    }
}