namespace Directory.Entities;

using Directory.Enums;
using JetBrains.Annotations;

public sealed class UserCorrection
{
    public Guid Id { get; init; } = Guid.CreateVersion7(DateTimeOffset.UtcNow);

    public required Guid ChurchId { get; init; }

    public Guid? UserId { get; init; }

    public required string Field { get; set; }

    public string? OldValue { get; set; }

    public required string NewValue { get; set; }

    public CorrectionStatus Status { get; set; }

    public Guid? ReviewedBy { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public string? ChurchName { get; set; }

    [UsedImplicitly]
    public string? TargetChurchName { get; set; }

    [UsedImplicitly]
    public string? ChurchSlug { get; set; }

    [UsedImplicitly]
    public string? TargetChurchSlug { get; set; }
}
