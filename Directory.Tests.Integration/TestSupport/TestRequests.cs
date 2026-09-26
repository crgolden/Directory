namespace Directory.Tests.Integration.TestSupport;

using System.Globalization;
using Directory.Enums;

internal static class TestRequests
{
    internal static object NewChurch(string? nameSuffix = null) => new
    {
        CanonicalName = $"{Generated.NewName()}{nameSuffix}",
        Latitude = Generated.NewLatitude(),
        Longitude = Generated.NewLongitude(),
        City = Generated.NewCity(),
        State = Generated.NewStateCodeText(),
        Zip = Generated.NewZip(),
        WorshipStyle = (int)Generated.NewDefinedValue<WorshipStyle>(),
        PrimaryLanguage = Generated.NewLanguage(),
    };

    internal static object NewCampus() => new
    {
        Name = Generated.NewName(),
        City = Generated.NewCity(),
        State = Generated.NewStateCodeText(),
        Zip = Generated.NewZip(),
        Latitude = Generated.NewLatitude(),
        Longitude = Generated.NewLongitude(),
    };

    internal static object NewMinistry() => new
    {
        Name = Generated.NewName(),
        Description = Generated.NewDescription(),
    };

    internal static object NewSchedule() => new
    {
        DayOfWeek = Generated.NewDayOfWeek(),
        StartTime = Generated.NewTimeOfDay().ToString("HH:mm", CultureInfo.InvariantCulture),
        Description = Generated.NewDescription(),
    };
}
