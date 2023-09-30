namespace UnifiedStorage.Common.Configuration.AzureKeyVaultConfig;

public class KeyVaultConfig
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string VaultUrl { get; set; } = string.Empty;
}