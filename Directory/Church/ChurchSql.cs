namespace Directory.Church;

internal static class ChurchSql
{
    private const string FromActiveChurches =
        " FROM [dbo].[Churches] c" +
        " WHERE c.[IsActive] = 1";

    internal static string ActivePage() =>
        "SELECT COUNT(*) AS [TotalCount]" + FromActiveChurches +
        "; SELECT " + ChurchService.SelectColumns + FromActiveChurches +
        " ORDER BY c.[CanonicalName] ASC" +
        " OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";
}
