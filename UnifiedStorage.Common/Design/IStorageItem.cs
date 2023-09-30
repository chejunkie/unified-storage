using UnifiedStorage.Common.Constants;

namespace UnifiedStorage.Common.Design;

public interface IStorageItem
{
    string Name { get; }
    StorageItemType Type { get; }
}