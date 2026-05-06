namespace Experience.Server.Extensions;

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
            string ExperienceClientId,
            string ExperienceClientSecret
        ) GetExperienceSecrets()
        {
            var experienceClientId = configuration.GetRequired<string>("ExperienceClientId");
            var experienceClientSecret = configuration.GetRequired<string>("ExperienceClientSecret");
            return (
                experienceClientId,
                experienceClientSecret
            );
        }
#pragma warning restore SA1009
    }
}
