namespace Directory.Church;

using Directory.Enums;

public record ChurchRequest(
    string CanonicalName,
    double Latitude,
    double Longitude,
    string? Street,
    string City,
    string State,
    string Zip,
    string? PhoneNumber,
    string? Website,
    string? EmailAddress,
    Guid? DenominationId,
    WorshipStyle WorshipStyle,
    string PrimaryLanguage,
    bool? AcceptsLGBTQ,
    bool? WheelchairAccessible,
    bool? HasNursery,
    bool? HasYouthProgram);
