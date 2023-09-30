using System.Collections.Generic;
using UnifiedStorage.Common.Configuration.AzureKeyVaultConfig;

namespace UnifiedStorage.Common.Configuration;

public class SecretsConfig
{
    public KeyVaultConfig Common { get; set; } = new();
    public Dictionary<string, StorageConfig> Storages { get; set; } = new();
}