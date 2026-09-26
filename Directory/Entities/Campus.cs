namespace Directory.Entities;

using System.Text.Json.Serialization;
using Shared.Domain;

public sealed class Campus
{
    public Guid Id { get; init; } = Guid.CreateVersion7(DateTimeOffset.UtcNow);

    public required Guid ChurchId { get; init; }

    public required string Name { get; set; }

    public string? Street { get; set; }

    public required string City { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter<StateCode>))]
    public required StateCode State { get; set; }

    public required string Zip { get; set; }

    public required double Latitude { get; set; }

    public required double Longitude { get; set; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
