namespace Directory.Church;

using Directory.Enums;

public record PatchChurchRequest(
    Optional<string> CanonicalName,
    Optional<double?> Latitude,
    Optional<double?> Longitude,
    Optional<string> Street,
    Optional<string> City,
    Optional<string> State,
    Optional<string> Zip,
    Optional<string> PhoneNumber,
    Optional<string> Website,
    Optional<string> EmailAddress,
    Optional<Guid?> DenominationId,
    Optional<WorshipStyle?> WorshipStyle,
    Optional<string> PrimaryLanguage,
    Optional<bool?> AcceptsLGBTQ,
    Optional<bool?> WheelchairAccessible,
    Optional<bool?> HasNursery,
    Optional<bool?> HasYouthProgram);
