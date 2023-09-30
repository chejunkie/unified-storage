using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;

namespace UnifiedStorage.Common.Configuration.AzureKeyVaultConfig;

public class ConfigLoader
{
    private readonly string _jsonConfigPath;

    public ConfigLoader(string jsonConfigPath)
    {
        if (string.IsNullOrWhiteSpace(jsonConfigPath))
        {
            throw new ArgumentException(
                "Value cannot be null or whitespace.",
                nameof(jsonConfigPath)
            );
        }
        if (!File.Exists(jsonConfigPath))
        {
            throw new FileNotFoundException(
                "File not found",
                jsonConfigPath
            );
        }
        _jsonConfigPath = jsonConfigPath;
    }

    public SecretsConfig LoadSecretsFromConfig()
    {
        if (!File.Exists(_jsonConfigPath))
        {
            throw new InvalidOperationException(
                "secrets.json file not found."
            );
        }

        // Parse JSON
        var configText = File.ReadAllText(_jsonConfigPath);
        if (string.IsNullOrEmpty(configText))
        {
            throw new InvalidOperationException(
                "Configuration file is empty."
            );
        }

        var config = JObject.Parse(configText);

        // Ensure required properties in "Common" are present
        EnsureRequiredConfigValue(config, "Common", "VaultUrl");
        EnsureRequiredConfigValue(config, "Common", "TenantId");
        EnsureRequiredConfigValue(config, "Common", "ClientId");
        EnsureRequiredConfigValue(config, "Common", "ClientSecret");

        var secretsConfig = new SecretsConfig
        {
            Common = config["Common"]!.ToObject<KeyVaultConfig>()!,
            Storages = config["Storages"]!.ToObject<Dictionary<string, StorageConfig>>()!
        };

        return secretsConfig;
    }

    private void EnsureRequiredConfigValue(
        JObject config,
        string parentKey,
        string key)
    {
        if (config == null)
        {
            throw new InvalidOperationException(
                "Configuration is null."
            );
        }

        if (string.IsNullOrEmpty(config[parentKey]?.Value<string>(key)))
        {
            throw new InvalidOperationException(
                $"{key} is missing in secrets.json under {parentKey}.");
        }
    }
}