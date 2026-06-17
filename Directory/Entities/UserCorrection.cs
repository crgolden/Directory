namespace Directory.Entities;

using Enums;

public sealed class UserCorrection
{
    public Guid Id { get; init; } = Guid.CreateVersion7(DateTimeOffset.UtcNow);

    required public Guid ChurchId { get; init; }

    required public string UserId { get; init; } = null!;

    required public string Field { get; set; } = null!;

    public string? OldValue { get; set; }

    required public string NewValue { get; set; } = null!;

    public CorrectionStatus Status { get; set; }

    public string? ReviewedBy { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public string? ChurchName { get; set; }
}
