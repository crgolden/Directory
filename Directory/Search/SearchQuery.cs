namespace Directory.Search;

using Directory.Enums;

public record SearchQuery(
    string? Q,
    double? Lat,
    double? Lng,
    double? RadiusMiles,
    string? State,
    Guid? DenominationId,
    WorshipStyle? WorshipStyle,
    bool? WheelchairAccessible,
    int? DayOfWeek,
    TimeOnly? StartTimeBefore,
    TimeOnly? StartTimeAfter,
    int Page,
    int PageSize,
    string? Sort = null);
