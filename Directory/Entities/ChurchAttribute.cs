namespace Directory.Entities;

public sealed class ChurchAttribute
{
    public Guid Id { get; init; } = Guid.CreateVersion7(DateTimeOffset.UtcNow);

    required public Guid ChurchId { get; init; }

    required public string Key { get; set; };

    required public string Value { get; set; };

    required public string Source { get; set; };

    public decimal Confidence { get; set; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
