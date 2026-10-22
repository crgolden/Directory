namespace Directory.Admin;

using System.Diagnostics.CodeAnalysis;

[ExcludeFromCodeCoverage]
public static class AdminEndpoints
{
    public const string Route = "/admin";

    public const string ExportSegment = "/export";

    private static readonly HashSet<string> AllowedCsvTypes =
    [
        "text/csv", "application/csv", "application/octet-stream",
    ];

    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(Route)
            .WithTags("Admin")
            .RequireAuthorization(AuthorizationPolicies.ChurchesModPolicy);

        group.MapPost("/import", async (
            IFormFile file,
            AdminService service,
            CancellationToken ct) =>
        {
            if (!AllowedCsvTypes.Contains(file.ContentType))
            {
                return Results.BadRequest("A CSV file is required.");
            }

            using var reader = new StreamReader(file.OpenReadStream());
            var published = await service.ImportCsvAsync(reader, ct);
            return Results.Ok(new { published });
        }).DisableAntiforgery();

        group.MapGet(ExportSegment, async (AdminService service, CancellationToken ct) =>
        {
            var csv = await service.ExportCsvAsync(ct);
            return Results.Text(csv, "text/csv", System.Text.Encoding.UTF8);
        });

        return app;
    }
}
