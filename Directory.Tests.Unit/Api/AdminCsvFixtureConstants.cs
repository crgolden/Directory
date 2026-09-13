namespace Directory.Tests.Unit.Api;

using System.Globalization;

internal static class AdminCsvFixtureConstants
{
    internal const char CsvFieldSeparator = ',';

    internal const char CsvLineSeparator = '\n';

    internal const int CsvHeaderLineCount = 1;

    internal const string CommaDecimalDottedDateCultureName = "de-DE";

    internal static readonly CultureInfo CommaDecimalDottedDateCulture =
        CultureInfo.GetCultureInfo(CommaDecimalDottedDateCultureName);
}
