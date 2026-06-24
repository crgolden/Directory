namespace Directory.Campuses;

using System.Diagnostics.CodeAnalysis;
using Entities;

[ExcludeFromCodeCoverage]
public static class CampusEndpoints
{
    public static IEndpointRouteBuilder MapCampusEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/churches/{churchId:guid}/campuses", async (
            Guid churchId,
            CampusRequest req,
            CampusService service,
            CancellationToken ct) =>
        {
            if (!IsValid(req))
            {
                return Results.BadRequest("Name, City, State, and Zip are required.");
            }

            var created = await service.CreateAsync(churchId, ToCampus(churchId, req), ct);
            return Results.Created($"/campuses/{created.Id}", created);
        }).RequireAuthorization("ChurchesMod").WithTags("Campuses");

        app.MapPut("/campuses/{id:guid}", async (
            Guid id,
            CampusRequest req,
            CampusService service,
            CancellationToken ct) =>
        {
            if (!IsValid(req))
            {
                return Results.BadRequest("Name, City, State, and Zip are required.");
            }

            return await service.UpdateAsync(id, ToCampus(Guid.Empty, req), ct)
                ? Results.NoContent()
                : Results.NotFound();
        }).RequireAuthorization("ChurchesMod").WithTags("Campuses");

        app.MapDelete("/campuses/{id:guid}", async (
            Guid id,
            CampusService service,
            CancellationToken ct) =>
            await service.DeleteAsync(id, ct) ? Results.NoContent() : Results.NotFound())
            .RequireAuthorization("ChurchesMod").WithTags("Campuses");

        return app;
    }

    private static bool IsValid(CampusRequest req) =>
        !string.IsNullOrWhiteSpace(req.Name) && !string.IsNullOrWhiteSpace(req.City)
        && !string.IsNullOrWhiteSpace(req.State) && !string.IsNullOrWhiteSpace(req.Zip);

    private static Campus ToCampus(Guid churchId, CampusRequest req) => new Campus
    {
        ChurchId = churchId,
        Name = req.Name,
        Street = req.Street,
        City = req.City,
        State = req.State,
        Zip = req.Zip,
        Latitude = req.Latitude,
        Longitude = req.Longitude,
    };
}

public record CampusRequest(
    string Name,
    string? Street,
    string City,
    string State,
    string Zip,
    double Latitude,
    double Longitude);
