namespace Experience.Server.Extensions;

using Azure.Security.KeyVault.Secrets;

public static class SecretClientExtensions
{
    public static async Task<(KeyVaultSecret, KeyVaultSecret, KeyVaultSecret, KeyVaultSecret, KeyVaultSecret)> GetSecrets(this SecretClient secretClient, CancellationToken cancellationToken = default)
    {
        var tasks = new[]
        {
            secretClient.GetSecretAsync("ElasticsearchUsername", cancellationToken: cancellationToken),
            secretClient.GetSecretAsync("ElasticsearchPassword", cancellationToken: cancellationToken),
            secretClient.GetSecretAsync("ExperienceClientId", cancellationToken: cancellationToken),
            secretClient.GetSecretAsync("ExperienceClientSecret", cancellationToken: cancellationToken),
            secretClient.GetSecretAsync("OidcAuthority", cancellationToken: cancellationToken),
        };
        var result = await Task.WhenAll(tasks);
        return (result[0].Value, result[1].Value, result[2].Value, result[3].Value, result[4].Value);
    }
}
