namespace Directory.Extensions;
using System.Diagnostics.CodeAnalysis;

using Azure.Security.KeyVault.Secrets;

[ExcludeFromCodeCoverage]
public static class SecretClientExtensions
{
    extension(SecretClient secretClient)
    {
#pragma warning disable SA1009
        public (
            KeyVaultSecret ElasticsearchUsername,
            KeyVaultSecret ElasticsearchPassword,
            KeyVaultSecret SqlServerUserId,
            KeyVaultSecret SqlServerPassword
        ) GetDirectorySecrets()
        {
            var elasticsearchUsername = secretClient.GetSecret("ElasticsearchUsername");
            var elasticsearchPassword = secretClient.GetSecret("ElasticsearchPassword");
            var sqlServerUserId = secretClient.GetSecret("DirectorySqlServerUserId");
            var sqlServerPassword = secretClient.GetSecret("DirectorySqlServerPassword");
            return (
                elasticsearchUsername.Value,
                elasticsearchPassword.Value,
                sqlServerUserId.Value,
                sqlServerPassword.Value
            );
        }
#pragma warning restore SA1009
    }
}
