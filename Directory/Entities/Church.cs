namespace Directory.Entities;

using Enums;

public sealed class Church
{
    public Guid Id { get; init; } = Guid.CreateVersion7(DateTimeOffset.UtcNow);

    required public string CanonicalName { get; set; };

    required public string Slug { get; set; };

    required public double Latitude { get; set; }

    required public double Longitude { get; set; }

    public string? Street { get; set; }

    required public string City { get; set; };

    required public string State { get; set; };

    required public string Zip { get; set; };

    public string? PhoneNumber { get; set; }

    public string? Website { get; set; }

    public string? EmailAddress { get; set; }

    public Guid? DenominationId { get; set; }

    public WorshipStyle WorshipStyle { get; set; }

    required public string PrimaryLanguage { get; set; };

    public bool? AcceptsLGBTQ { get; set; }

    public bool? WheelchairAccessible { get; set; }

    public bool? HasNursery { get; set; }

    public bool? HasYouthProgram { get; set; }

    public decimal ConfidenceScore { get; set; }

    public DateTimeOffset? LastVerifiedAt { get; set; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public bool IsActive { get; set; } = true;

    // Populated on the church-detail read; empty on list responses.
    public IReadOnlyList<ServiceSchedule> Schedules { get; set; } = [];

    public IReadOnlyList<Ministry> Ministries { get; set; } = [];

    public IReadOnlyList<Campus> Campuses { get; set; } = [];
}
