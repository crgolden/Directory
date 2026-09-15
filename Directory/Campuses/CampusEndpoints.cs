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
            if (!IsValid(req) || !Shared.Domain.StateCodes.TryParse(req.State, out var state))
            {
                return Results.BadRequest("Name, City, State, and Zip are required, and State must be a USPS code.");
            }

            var created = await service.CreateAsync(churchId, ToCampus(churchId, req, state), ct);
            return Results.Created($"/campuses/{created.Id}", created);
        }).RequireAuthorization(AuthorizationPolicies.ChurchesModPolicy).WithTags("Campuses");

        app.MapPut("/campuses/{id:guid}", async (
            Guid id,
            CampusRequest req,
            CampusService service,
            CancellationToken ct) =>
        {
            if (!IsValid(req) || !Shared.Domain.StateCodes.TryParse(req.State, out var state))
            {
                return Results.BadRequest("Name, City, State, and Zip are required, and State must be a USPS code.");
            }

            return await service.UpdateAsync(id, ToCampus(Guid.Empty, req, state), ct)
                ? Results.NoContent()
                : Results.NotFound();
        }).RequireAuthorization(AuthorizationPolicies.ChurchesModPolicy).WithTags("Campuses");

        app.MapDelete("/campuses/{id:guid}", async (
            Guid id,
            CampusService service,
            CancellationToken ct) =>
            await service.DeleteAsync(id, ct) ? Results.NoContent() : Results.NotFound())
            .RequireAuthorization(AuthorizationPolicies.ChurchesModPolicy).WithTags("Campuses");

        return app;
    }

    private static bool IsValid(CampusRequest req) =>
        !string.IsNullOrWhiteSpace(req.Name) && !string.IsNullOrWhiteSpace(req.City)
        && !string.IsNullOrWhiteSpace(req.State) && !string.IsNullOrWhiteSpace(req.Zip);

    private static Campus ToCampus(Guid churchId, CampusRequest req, Shared.Domain.StateCode state) => new Campus
    {
        ChurchId = churchId,
        Name = req.Name,
        Street = req.Street,
        City = req.City,
        State = state,
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