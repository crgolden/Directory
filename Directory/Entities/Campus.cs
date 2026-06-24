namespace Directory.Entities;

public sealed class Campus
{
    public Guid Id { get; init; } = Guid.CreateVersion7(DateTimeOffset.UtcNow);

    required public Guid ChurchId { get; init; }

    required public string Name { get; set; };

    public string? Street { get; set; }

    required public string City { get; set; };

    required public string State { get; set; };

    required public string Zip { get; set; };

    required public double Latitude { get; set; }

    required public double Longitude { get; set; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
