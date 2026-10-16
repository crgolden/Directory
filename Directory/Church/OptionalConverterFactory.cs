namespace Directory.Church;

using System.Text.Json;
using System.Text.Json.Serialization;

public sealed class OptionalConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(Optional<>);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var valueType = typeToConvert.GetGenericArguments()[0];
        var converterType = typeof(OptionalConverter<>).MakeGenericType(valueType);
        if (Activator.CreateInstance(converterType) is JsonConverter converter)
        {
            return converter;
        }

        // Stryker disable once String,Statement : OptionalConverter<T> is public with a parameterless constructor, so Activator.CreateInstance never returns null and this message is unreachable
        throw new InvalidOperationException($"Could not create a converter for {typeToConvert}.");
    }
}
