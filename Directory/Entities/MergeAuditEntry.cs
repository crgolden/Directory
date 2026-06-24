namespace Directory.Entities;

public sealed class MergeAuditEntry
{
    public Guid Id { get; init; } = Guid.CreateVersion7(DateTimeOffset.UtcNow);

    required public Guid SurvivingId { get; init; }

    required public Guid AbsorbedId { get; init; }

    required public string MergedBy { get; init; };

    public DateTimeOffset MergedAt { get; init; } = DateTimeOffset.UtcNow;

    public string? FieldsOverridden { get; set; }
}
