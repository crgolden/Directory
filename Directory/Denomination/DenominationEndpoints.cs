namespace Directory.Denomination;

using System.Diagnostics.CodeAnalysis;

[ExcludeFromCodeCoverage]
public static class DenominationEndpoints
{
    internal const string Route = "/denominations";

    public static IEndpointRouteBuilder MapDenominationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(Route, async (DenominationService service, CancellationToken ct) =>
        {
            var denominations = await service.GetAllAsync(ct);
            return Results.Ok(denominations);
        }).WithTags("Denominations");

        return app;
    }
}
