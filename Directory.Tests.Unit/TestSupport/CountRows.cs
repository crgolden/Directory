namespace Directory.Tests.Unit.TestSupport;

using System.Data;
using Directory.Church;
using Directory.Entities;

internal static class CountRows
{
    public static DataTable Table(int totalCount)
    {
        var t = new DataTable();
        t.Columns.Add(nameof(PagedResult<Church>.TotalCount), typeof(int));
        t.Rows.Add(totalCount);
        return t;
    }
}
