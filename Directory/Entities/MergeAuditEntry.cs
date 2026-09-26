namespace Directory.Entities;

public sealed class MergeAuditEntry
{
    public Guid Id { get; init; } = Guid.CreateVersion7(DateTimeOffset.UtcNow);

    public required Guid SurvivingId { get; init; }

    public required Guid AbsorbedId { get; init; }

    public Guid? MergedBy { get; init; }

    public DateTimeOffset MergedAt { get; init; } = DateTimeOffset.UtcNow;

    public string? FieldsOverridden { get; set; }
}
