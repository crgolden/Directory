namespace Experience.Server.Extensions;

using Azure.Security.KeyVault.Secrets;

public static class SecretClientExtensions
{
    extension(SecretClient secretClient)
    {
#pragma warning disable SA1009
        public (
            KeyVaultSecret ElasticsearchUsername,
            KeyVaultSecret ElasticsearchPassword,
            KeyVaultSecret ExperienceClientId,
            KeyVaultSecret ExperienceClientSecret
        ) GetExperienceSecrets()
        {
            var elasticsearchUsername = secretClient.GetSecret("ElasticsearchUsername");
            var elasticsearchPassword = secretClient.GetSecret("ElasticsearchPassword");
            var experienceClientId = secretClient.GetSecret("ExperienceClientId");
            var experienceClientSecret = secretClient.GetSecret("ExperienceClientSecret");
            return (
                elasticsearchUsername.Value,
                elasticsearchPassword.Value,
                experienceClientId.Value,
                experienceClientSecret.Value
            );
        }
#pragma warning restore SA1009
    }
}