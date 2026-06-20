namespace Directory.Denomination;
using System.Diagnostics.CodeAnalysis;

[ExcludeFromCodeCoverage]
public static class DenominationEndpoints
{
    public static IEndpointRouteBuilder MapDenominationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/denominations", async (DenominationService service, CancellationToken ct) =>
        {
            var denominations = await service.GetAllAsync(ct);
            return Results.Ok(denominations);
        }).WithTags("Denominations");

        return app;
    }
}
