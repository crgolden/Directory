namespace Directory.Church;

using System.Diagnostics.CodeAnalysis;
using Directory.Entities;

[ExcludeFromCodeCoverage]
public static class ChurchEndpoints
{
    internal const string Route = "/churches";

    private const string ChurchesModPolicy = AuthorizationPolicies.ChurchesModPolicy;

    public static IEndpointRouteBuilder MapChurchEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(Route).WithTags("Churches");

        group.MapGet("/", GetPageAsync);
        group.MapGet("/{slug}", GetBySlugAsync);
        group.MapPost("/", CreateAsync).RequireAuthorization(ChurchesModPolicy);
        group.MapPut("/{id:guid}", ReplaceAsync).RequireAuthorization(ChurchesModPolicy);
        group.MapPatch("/{id:guid}", PatchAsync).RequireAuthorization(ChurchesModPolicy);
        group.MapDelete("/{id:guid}", DeleteAsync).RequireAuthorization(ChurchesModPolicy);

        return app;
    }

    private static async Task<IResult> GetPageAsync(int page, int pageSize, ChurchService service, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var (items, totalCount) = await service.GetPageAsync(page, pageSize, ct);
        return Results.Ok(new PagedResult<Church>(items, totalCount, page, pageSize));
    }

    private static async Task<IResult> GetBySlugAsync(string slug, ChurchService service, CancellationToken ct)
    {
        var church = await service.GetBySlugAsync(slug, ct);
        return church is null ? Results.NotFound() : Results.Ok(church);
    }

    private static async Task<IResult> CreateAsync(ChurchRequest req, ChurchService service, CancellationToken ct)
    {
        var created = await service.CreateAsync(req, ct);
        return Results.Created($"{Route}/{created.Slug}", created);
    }

    private static async Task<IResult> ReplaceAsync(Guid id, ChurchRequest req, ChurchService service, CancellationToken ct)
    {
        var existing = await service.GetByIdAsync(id, ct);
        if (existing is null)
        {
            return Results.NotFound();
        }

        if (!Shared.Domain.StateCodes.TryParse(req.State, out var state))
        {
            return Results.BadRequest($"Unknown state code '{req.State}'.");
        }

        existing.CanonicalName = req.CanonicalName;
        existing.Latitude = req.Latitude;
        existing.Longitude = req.Longitude;
        existing.Street = req.Street;
        existing.City = req.City;
        existing.State = state;
        existing.Zip = req.Zip;
        existing.PhoneNumber = req.PhoneNumber;
        existing.Website = req.Website;
        existing.EmailAddress = req.EmailAddress;
        existing.DenominationId = req.DenominationId;
        existing.WorshipStyle = req.WorshipStyle;
        existing.PrimaryLanguage = req.PrimaryLanguage;
        existing.AcceptsLGBTQ = req.AcceptsLGBTQ;
        existing.WheelchairAccessible = req.WheelchairAccessible;
        existing.HasNursery = req.HasNursery;
        existing.HasYouthProgram = req.HasYouthProgram;
        await service.UpdateAsync(existing, ct);
        return Results.Ok(existing);
    }

    private static async Task<IResult> PatchAsync(Guid id, PatchChurchRequest req, ChurchService service, CancellationToken ct)
    {
        var existing = await service.GetByIdAsync(id, ct);
        if (existing is null)
        {
            return Results.NotFound();
        }

        Shared.Domain.StateCode? state = null;
        if (req.State.HasValue)
        {
            if (req.State.Value is null)
            {
                return Results.BadRequest("state cannot be cleared; a church must have one.");
            }

            if (!Shared.Domain.StateCodes.TryParse(req.State.Value, out var parsed))
            {
                return Results.BadRequest($"Unknown state code '{req.State.Value}'.");
            }

            state = parsed;
        }

        ApplyPatch(existing, req, state);
        await service.UpdateAsync(existing, ct);
        return Results.Ok(existing);
    }

    private static void ApplyPatch(Church existing, PatchChurchRequest req, Shared.Domain.StateCode? state)
    {
        existing.CanonicalName = req.CanonicalName.Or(existing.CanonicalName) ?? existing.CanonicalName;
        existing.Latitude = req.Latitude.Or(existing.Latitude) ?? existing.Latitude;
        existing.Longitude = req.Longitude.Or(existing.Longitude) ?? existing.Longitude;
        existing.Street = req.Street.Or(existing.Street);
        existing.City = req.City.Or(existing.City) ?? existing.City;
        existing.State = req.State.HasValue ? state ?? existing.State : existing.State;
        existing.Zip = req.Zip.Or(existing.Zip) ?? existing.Zip;
        existing.PhoneNumber = req.PhoneNumber.Or(existing.PhoneNumber);
        existing.Website = req.Website.Or(existing.Website);
        existing.EmailAddress = req.EmailAddress.Or(existing.EmailAddress);
        existing.DenominationId = req.DenominationId.Or(existing.DenominationId);
        existing.WorshipStyle = req.WorshipStyle.Or(existing.WorshipStyle) ?? existing.WorshipStyle;
        existing.PrimaryLanguage = req.PrimaryLanguage.Or(existing.PrimaryLanguage) ?? existing.PrimaryLanguage;
        existing.AcceptsLGBTQ = req.AcceptsLGBTQ.Or(existing.AcceptsLGBTQ);
        existing.WheelchairAccessible = req.WheelchairAccessible.Or(existing.WheelchairAccessible);
        existing.HasNursery = req.HasNursery.Or(existing.HasNursery);
        existing.HasYouthProgram = req.HasYouthProgram.Or(existing.HasYouthProgram);
    }

    private static async Task<IResult> DeleteAsync(Guid id, ChurchService service, CancellationToken ct)
    {
        var deleted = await service.DeleteAsync(id, ct);
        return deleted ? Results.NoContent() : Results.NotFound();
    }
}
