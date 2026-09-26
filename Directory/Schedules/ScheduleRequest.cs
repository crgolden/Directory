namespace Directory.Schedules;

public record ScheduleRequest(byte DayOfWeek, string StartTime, string? Description);
