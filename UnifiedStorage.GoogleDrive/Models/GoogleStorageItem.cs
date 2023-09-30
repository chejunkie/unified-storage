using UnifiedStorage.Common.Models;

namespace UnifiedStorage.GoogleDrive.Models;

public class GoogleStorageItem : StorageItem
{
    public string Id { get; set; } = string.Empty;
    public string ParentId { get; set; } = string.Empty;
}