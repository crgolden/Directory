namespace Directory.Entities;

public sealed class ServiceSchedule
{
    public Guid Id { get; init; } = Guid.CreateVersion7(DateTimeOffset.UtcNow);

    required public Guid ChurchId { get; init; }

    public Guid? CampusId { get; init; }

    required public DayOfWeek DayOfWeek { get; set; }

    required public TimeOnly StartTime { get; set; }

    public string? Description { get; set; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
