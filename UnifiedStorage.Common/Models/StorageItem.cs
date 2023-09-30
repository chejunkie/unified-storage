using UnifiedStorage.Common.Constants;
using UnifiedStorage.Common.Design;

namespace UnifiedStorage.Common.Models;

public class StorageItem : IStorageItem
{
    public string Name { get; set; } = string.Empty;
    public StorageItemType Type { get; set; }

    public override string ToString()
    {
        return $"{Type}: {Name}";
    }
}