namespace Directory.Tests.Unit.TestSupport;

using System.Globalization;
using Enums;
using Shared.Domain;

internal static class TestValues
{
    private const string Punctuation = "!@#$%^&*()-_=+[]{};:,.<>/?|~";

    internal static string LowercaseToken(int length) =>
        string.Concat(Enumerable.Range(0, length).Select(_ => (char)Random.Shared.Next('a', 'z' + 1)));

    internal static string PunctuationToken(int length) =>
        string.Concat(Enumerable.Range(0, length).Select(_ => Punctuation[Random.Shared.Next(Punctuation.Length)]));

    internal static string NewKeyword() => LowercaseToken(Random.Shared.Next(4, 12));

    internal static string NewBlank() => new string(' ', Random.Shared.Next(1, 4));

    internal static string NewPunctuationToken() => PunctuationToken(Random.Shared.Next(1, 4));

    internal static string NewPunctuationOnlyQuery() =>
        $"{NewPunctuationToken()} {NewPunctuationToken()}";

    internal static string NewUnparseableStateCode() => LowercaseToken(Random.Shared.Next(3, 10));

    internal static Uri NewWebsiteUri() => new Uri(NewWebsite());

    internal static string NewName() => $"{LowercaseToken(6)} {LowercaseToken(8)}";

    internal static string NewSlug() => $"{LowercaseToken(6)}-{LowercaseToken(8)}";

    internal static string NewCity() => LowercaseToken(9);

    internal static StateCode NewStateCode()
    {
        var defined = Enum.GetValues<StateCode>();
        return defined[Random.Shared.Next(defined.Length)];
    }

    internal static string NewStateCodeText() => NewStateCode().ToString();

    internal static StateCode NewUndefinedStateCode() =>
        (StateCode)(Enum.GetValues<StateCode>().Max(code => (int)code) + Random.Shared.Next(1, 100));

    internal static string NewZip() => Random.Shared.Next(10000, 100000).ToString(CultureInfo.InvariantCulture);

    internal static string NewStreet() => $"{Random.Shared.Next(100, 10000)} {LowercaseToken(10)} street";

    internal static string NewLanguage() => LowercaseToken(7);

    internal static string NewPhoneNumber() =>
        $"{Random.Shared.Next(200, 1000)}-{Random.Shared.Next(200, 1000)}-{Random.Shared.Next(1000, 10000)}";

    internal static string NewWebsite() => $"https://{LowercaseToken(12)}.example";

    internal static string NewEmailAddress() => $"{LowercaseToken(8)}@{LowercaseToken(12)}.example";

    internal static Guid NewUserId() => Guid.NewGuid();

    internal static string NewNonGuidSubject() => $"user-{LowercaseToken(12)}";

    internal static string NewFieldName() => LowercaseToken(9);

    internal static string NewDescription() => $"{LowercaseToken(7)} {LowercaseToken(9)} {LowercaseToken(5)}";

    internal static string NewFailureMessage() => $"failure-{LowercaseToken(10)}";

    internal static double NewLatitude() => Math.Round((Random.Shared.NextDouble() * 180.0) - 90.0, 6);

    internal static double NewLongitude() => Math.Round((Random.Shared.NextDouble() * 360.0) - 180.0, 6);

    internal static double NewRadiusMiles() => Math.Round(Random.Shared.NextDouble() * 500.0, 2);

    internal static int NewRowCount() => Random.Shared.Next(1, 1000);

    internal static int NewPage() => Random.Shared.Next(1, 50);

    internal static int NewPageSize() => Random.Shared.Next(1, 100);

    internal static int NewDayOfWeek() => Random.Shared.Next(0, 7);

    internal static TimeOnly NewTimeOfDay() => new TimeOnly(Random.Shared.Next(0, 24), Random.Shared.Next(0, 60));

    internal static WorshipStyle NewWorshipStyle()
    {
        var worshipStyles = Enum.GetValues<WorshipStyle>();
        return worshipStyles[Random.Shared.Next(worshipStyles.Length)];
    }

    internal static decimal NewConfidenceScore() => Math.Round((decimal)Random.Shared.NextDouble(), 4);

    internal static DateTimeOffset NewUtcTimestamp() =>
        DateTimeOffset.UtcNow.AddMinutes(-Random.Shared.Next(1, 100000));

    internal static DateTimeOffset NewTimestampWithNonZeroOffset() =>
        NewUtcTimestamp().ToOffset(TimeSpan.FromHours(-Random.Shared.Next(1, 13)));
}
