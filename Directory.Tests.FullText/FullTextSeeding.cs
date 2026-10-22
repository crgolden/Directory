namespace Directory.Tests.FullText;

using System.Data;
using System.Globalization;
using Directory.Tests.Integration.TestSupport;
using Microsoft.Data.SqlClient;

internal static class FullTextSeeding
{
    private const string InsertChurchSql = """
        INSERT INTO [dbo].[Churches] ([Id], [CanonicalName], [Slug], [Latitude], [Longitude], [City], [State], [Zip], [CreatedAt], [UpdatedAt])
        VALUES (@Id, @CanonicalName, @Slug, @Latitude, @Longitude, @City, @State, @Zip, @CreatedAt, @UpdatedAt);
        """;

    private const string KeywordIndexedSql = """
        SELECT COUNT(*) FROM CONTAINSTABLE([dbo].[Churches], ([CanonicalName], [City]), @Keyword) AS ft WHERE ft.[KEY] = @Id
        """;

    internal static async Task<Guid> SeedChurchAsync(DirectoryWebApplicationFactory factory, string canonicalName, CancellationToken ct)
    {
        var id = Guid.NewGuid();
        var createdAt = Generated.NewTimestampWithNonZeroOffset();
        await using var conn = await factory.OpenTestConnectionAsync(ct);
        await using var cmd = new SqlCommand(InsertChurchSql, conn);
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@CanonicalName", canonicalName);
        cmd.Parameters.AddWithValue("@Slug", Generated.NewSlug());
        cmd.Parameters.AddWithValue("@Latitude", Generated.NewLatitude());
        cmd.Parameters.AddWithValue("@Longitude", Generated.NewLongitude());
        cmd.Parameters.AddWithValue("@City", Generated.NewCity());
        cmd.Parameters.AddWithValue("@State", Generated.NewStateCode().ToString());
        cmd.Parameters.AddWithValue("@Zip", Generated.NewZip());
        cmd.Parameters.AddWithValue("@CreatedAt", createdAt);
        cmd.Parameters.AddWithValue("@UpdatedAt", createdAt);
        await cmd.ExecuteNonQueryAsync(ct);
        return id;
    }

    internal static async Task WaitForKeywordIndexedAsync(DirectoryWebApplicationFactory factory, string keyword, IEnumerable<Guid> ids, CancellationToken ct)
    {
        await using var conn = await factory.OpenTestConnectionAsync(ct);
        await using var cmd = new SqlCommand(KeywordIndexedSql, conn);
        cmd.Parameters.AddWithValue("@Keyword", $"\"{keyword}\"");
        var idParameter = cmd.Parameters.Add("@Id", SqlDbType.UniqueIdentifier);
        foreach (var id in ids)
        {
            idParameter.Value = id;
            int indexed;
            do
            {
                indexed = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
            }
            while (indexed == 0);
        }
    }
}
