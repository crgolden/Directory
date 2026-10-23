namespace Directory.Moderation;

internal static class ModerationSql
{
    internal static string CorrectionsPage() =>
        "SELECT " + ModerationService.SelectCorrection + ", COUNT(*) OVER() AS [TotalCount]" +
        ModerationService.FromCorrection +
        " WHERE (@Status IS NULL OR c.[Status] = @Status)" +
        " ORDER BY c.[CreatedAt] DESC" +
        " OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";
}
