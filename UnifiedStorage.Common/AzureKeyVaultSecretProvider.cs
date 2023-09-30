using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Threading.Tasks;
using UnifiedStorage.Common.Design;

namespace UnifiedStorage.Common;

public class AzureKeyVaultSecretProvider : ISecretProvider
{
    private const int MaxRetryAttempts = 3;

    private readonly IMemoryCache _cache;
    private readonly ILogger<AzureKeyVaultSecretProvider> _logger;
    private readonly SecretClient _secretClient;

    public AzureKeyVaultSecretProvider(
        string keyVaultUrl,
        string tenantId,
        string clientId,
        string clientSecret,
        ILogger<AzureKeyVaultSecretProvider> logger,
        IMemoryCache cache)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));

        var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
        _secretClient = new SecretClient(new Uri(keyVaultUrl), credential);
    }

    public AzureKeyVaultSecretProvider(
        string keyVaultUrl,
        string tenantId,
        string clientId,
        string clientSecret)
        : this(keyVaultUrl, tenantId, clientId, clientSecret,
              NullLogger<AzureKeyVaultSecretProvider>.Instance,
              new MemoryCache(new MemoryCacheOptions()))
    { }

    /// <summary>
    /// Asynchronously retrieves the value of a specified secret from Azure Key Vault.
    /// </summary>
    /// <param name="secretName">The name of the secret to retrieve.</param>
    /// <returns>The value of the specified secret.</returns>
    /// <exception cref="ArgumentException">Thrown if the provided secret name is null or empty.</exception>
    /// <exception cref="Azure.RequestFailedException">Thrown when there's an error during the retrieval process from Azure Key Vault.</exception>
    public async Task<string> GetSecretAsync(string secretName)
    {
        if (string.IsNullOrEmpty(secretName))
        {
            _logger.LogError("Secret name was not provided.");
            throw new ArgumentException(
                "Secret name cannot be null or empty.",
                nameof(secretName)
            );
        }

        // Try to get the secret from cache
        if (_cache.TryGetValue(secretName, out string? cachedSecret)
            && !string.IsNullOrEmpty(cachedSecret))
        {
            return cachedSecret;
        }

        try
        {
            var secret = await _secretClient.GetSecretAsync(secretName);

            // Ensure the secret is not null before caching
            if (secret?.Value?.Value != null)
            {
                // Set the secret in cache with an absolute expiration time (for example, 1 hour)
                _cache.Set(secretName, secret.Value.Value, TimeSpan.FromHours(1));

                return secret.Value.Value;
            }

            _logger.LogError(
                "Received null or invalid secret value " +
                "from Azure Key Vault for {secretName}.",
                secretName
            );
            throw new InvalidOperationException(
                $"Failed to retrieve a valid secret value for {secretName}."
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to retrieve secret {secretName} from Azure Key Vault.",
                secretName
            );
            throw;
        }
    }

    private static SecretClient CreateSecretClient(
        string keyVaultUrl)
    {
        var clientOptions = new SecretClientOptions
        {
            Retry =
            {
                Delay= TimeSpan.FromSeconds(2),
                MaxDelay = TimeSpan.FromSeconds(16),
                MaxRetries = MaxRetryAttempts,
                Mode = RetryMode.Exponential
            }
        };

        return new SecretClient(
            new Uri(keyVaultUrl),
            new DefaultAzureCredential(),
            clientOptions
       );
    }
}