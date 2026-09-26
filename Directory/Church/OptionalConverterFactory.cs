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
        return Activator.CreateInstance(converterType) is JsonConverter converter
            ? converter
            : throw new InvalidOperationException($"Could not create a converter for {typeToConvert}.");
    }
}
