namespace Directory.Admin;
using System.Diagnostics.CodeAnalysis;

[ExcludeFromCodeCoverage]
public static class AdminEndpoints
{
    private static readonly HashSet<string> AllowedCsvTypes =
    [
        "text/csv", "application/csv", "application/octet-stream",
    ];

    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin")
            .WithTags("Admin")
            .RequireAuthorization("ChurchesMod");

        group.MapPost("/import", async (
            IFormFile file,
            AdminService service,
            CancellationToken ct) =>
        {
            if (file is null || !AllowedCsvTypes.Contains(file.ContentType))
            {
                return Results.BadRequest("A CSV file is required.");
            }

            using var reader = new StreamReader(file.OpenReadStream());
            var csv = await reader.ReadToEndAsync(ct);
            var published = await service.ImportCsvAsync(csv, ct);
            return Results.Ok(new { published });
        }).DisableAntiforgery();

        group.MapGet("/export", async (AdminService service, CancellationToken ct) =>
        {
            var csv = await service.ExportCsvAsync(ct);
            return Results.Text(csv, "text/csv", System.Text.Encoding.UTF8);
        });

        return app;
    }
}
