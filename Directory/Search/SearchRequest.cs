namespace Directory.Search;

using Directory.Enums;

public readonly record struct SearchRequest(
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
    string? Sort,
    int Page = 1,
    int PageSize = 20);
