namespace Directory.Entities;

using System.Text.Json.Serialization;
using Directory.Enums;
using Shared.Domain;

public sealed class Church
{
    public Guid Id { get; init; } = Guid.CreateVersion7(DateTimeOffset.UtcNow);

    [JsonPropertyName(ChurchJsonNames.CanonicalName)]
    public required string CanonicalName { get; set; }

    public required string Slug { get; set; }

    public required double Latitude { get; set; }

    public required double Longitude { get; set; }

    [JsonPropertyName(ChurchJsonNames.Street)]
    public string? Street { get; set; }

    [JsonPropertyName(ChurchJsonNames.City)]
    public required string City { get; set; }

    [JsonPropertyName(ChurchJsonNames.State)]
    [JsonConverter(typeof(JsonStringEnumConverter<StateCode>))]
    public required StateCode State { get; set; }

    [JsonPropertyName(ChurchJsonNames.Zip)]
    public required string Zip { get; set; }

    [JsonPropertyName(ChurchJsonNames.PhoneNumber)]
    public string? PhoneNumber { get; set; }

    [JsonPropertyName(ChurchJsonNames.Website)]
    public string? Website { get; set; }

    [JsonPropertyName(ChurchJsonNames.EmailAddress)]
    public string? EmailAddress { get; set; }

    [JsonPropertyName(ChurchJsonNames.DenominationId)]
    public Guid? DenominationId { get; set; }

    [JsonPropertyName(ChurchJsonNames.WorshipStyle)]
    public WorshipStyle WorshipStyle { get; set; }

    [JsonPropertyName(ChurchJsonNames.PrimaryLanguage)]
    public required string PrimaryLanguage { get; set; }

    [JsonPropertyName(ChurchJsonNames.AcceptsLGBTQ)]
    public bool? AcceptsLGBTQ { get; set; }

    [JsonPropertyName(ChurchJsonNames.WheelchairAccessible)]
    public bool? WheelchairAccessible { get; set; }

    [JsonPropertyName(ChurchJsonNames.HasNursery)]
    public bool? HasNursery { get; set; }

    [JsonPropertyName(ChurchJsonNames.HasYouthProgram)]
    public bool? HasYouthProgram { get; set; }

    public decimal ConfidenceScore { get; set; }

    public DateTimeOffset? LastVerifiedAt { get; set; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public bool IsActive { get; set; } = true;

    public IReadOnlyList<ServiceSchedule> Schedules { get; set; } = [];

    public IReadOnlyList<Ministry> Ministries { get; set; } = [];

    public IReadOnlyList<Campus> Campuses { get; set; } = [];
}
