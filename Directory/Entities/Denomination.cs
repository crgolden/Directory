namespace Directory.Entities;

public sealed class Denomination
{
    public required Guid Id { get; init; }

    public required string Name { get; set; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset UpdatedAt { get; set; }
}
