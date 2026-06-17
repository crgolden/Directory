namespace Directory.Extensions;

public static class ConfigurationExtensions
{
    extension(IConfiguration configuration)
    {
        public T GetRequired<T>(string key)
            where T : notnull
        {
            return configuration.GetValue<T?>(key) ?? throw new InvalidOperationException($"Invalid '{key}'.");
        }

#pragma warning disable SA1009
        internal (
            string SqlServerUserId,
            string SqlServerPassword,
            string ServiceBusConnectionString
        ) GetDirectorySecrets()
        {
            var sqlServerUserId = configuration.GetRequired<string>("SqlConnectionStringBuilder:UserID");
            var sqlServerPassword = configuration.GetRequired<string>("SqlConnectionStringBuilder:Password");
            var serviceBusConnectionString = configuration.GetRequired<string>("ServiceBusConnectionString");
            return (
                sqlServerUserId,
                sqlServerPassword,
                serviceBusConnectionString
            );
        }
#pragma warning restore SA1009
    }
}
