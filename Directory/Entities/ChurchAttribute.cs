namespace Directory.Entities;

public sealed class ChurchAttribute
{
    public Guid Id { get; init; } = Guid.CreateVersion7(DateTimeOffset.UtcNow);

    public required Guid ChurchId { get; init; }

    public required string Key { get; set; }

    public required string Value { get; set; }

    public required string Source { get; set; }

    public decimal Confidence { get; set; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
