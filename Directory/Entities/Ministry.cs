namespace Directory.Entities;

public sealed class Ministry
{
    public Guid Id { get; init; } = Guid.CreateVersion7(DateTimeOffset.UtcNow);

    required public Guid ChurchId { get; init; }

    required public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
