namespace Directory.Search;

using System.Text;

internal static class RelevanceOrder
{
    internal static void Append(StringBuilder sb, int termCount)
    {
        var rank = string.Join(" + ", Enumerable.Range(0, termCount).Select(term => $"ft{term}.[RANK]"));
        sb.Append(" ORDER BY CASE WHEN c.[CanonicalName] = @ExactQ THEN 0 " +
                  "WHEN c.[CanonicalName] LIKE @PrefixQ ESCAPE '\\' THEN 1 " +
                  "ELSE 2 END ASC, ");
        sb.Append(rank);
        sb.Append(" DESC, c.[CanonicalName] ASC");
    }
}
