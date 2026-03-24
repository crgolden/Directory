namespace Experience.Server.Extensions;

using Azure.Identity;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

public static class ConfigurationExtensions
{
    extension(IConfiguration configuration)
    {
        public (IConfigurationSection, IConfigurationSection) GetSections()
        {
            var defaultAzureCredentialOptionsSection = configuration.GetSection(nameof(DefaultAzureCredentialOptions));
            var openIdConnectOptionsSection = configuration.GetSection(nameof(OpenIdConnectOptions));
            return (defaultAzureCredentialOptionsSection, openIdConnectOptionsSection);
        }

        public (Uri, Uri, Uri, Uri) GetUris()
        {
            var elasticsearchNode = configuration.GetValue<Uri>("ElasticsearchNode") ?? throw new InvalidOperationException("Invalid 'ElasticsearchNode'.");
            var keyVaultUrl = configuration.GetValue<Uri>("KeyVaultUri") ?? throw new InvalidOperationException("Invalid 'KeyVaultUri'.");
            var blobUrl = configuration.GetValue<Uri>("BlobUri") ?? throw new InvalidOperationException("Invalid 'BlobUri'.");
            var dataProtectionKeyIdentifier = configuration.GetValue<Uri>("DataProtectionKeyIdentifier") ?? throw new InvalidOperationException("Invalid 'DataProtectionKeyIdentifier'.");
            return (elasticsearchNode, keyVaultUrl, blobUrl, dataProtectionKeyIdentifier);
        }
    }
}
