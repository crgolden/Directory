namespace Directory.Moderation;

internal static class ModerationSql
{
    private const string StatusFilter = " WHERE (@Status IS NULL OR c.[Status] = @Status)";

    internal static string CorrectionsPage() =>
        "SELECT COUNT(*) AS [TotalCount] FROM [dbo].[UserCorrections] c" + StatusFilter +
        "; SELECT " + ModerationService.SelectCorrection +
        ModerationService.FromCorrection +
        StatusFilter +
        " ORDER BY c.[CreatedAt] DESC" +
        " OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";
}
