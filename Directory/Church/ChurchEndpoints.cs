namespace Directory.Church;

using Entities;
using Enums;

public static class ChurchEndpoints
{
    public static IEndpointRouteBuilder MapChurchEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/churches").WithTags("Churches");

        group.MapGet("/", async (
            int page,
            int pageSize,
            ChurchService service,
            CancellationToken ct) =>
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 50);
            var (items, totalCount) = await service.GetPageAsync(page, pageSize, ct);
            return Results.Ok(new PagedResult<Church>(items, totalCount, page, pageSize));
        });

        group.MapGet("/{slug}", async (
            string slug,
            ChurchService service,
            CancellationToken ct) =>
        {
            var church = await service.GetBySlugAsync(slug, ct);
            return church is null ? Results.NotFound() : Results.Ok(church);
        });

        group.MapPost("/", async (
            ChurchRequest req,
            ChurchService service,
            CancellationToken ct) =>
        {
            var church = new Church
            {
                CanonicalName = req.CanonicalName,
                Slug = string.Empty,
                Latitude = req.Latitude,
                Longitude = req.Longitude,
                Street = req.Street,
                City = req.City,
                State = req.State,
                Zip = req.Zip,
                PhoneNumber = req.PhoneNumber,
                Website = req.Website,
                EmailAddress = req.EmailAddress,
                DenominationId = req.DenominationId,
                WorshipStyle = req.WorshipStyle,
                PrimaryLanguage = req.PrimaryLanguage,
                AcceptsLGBTQ = req.AcceptsLGBTQ,
                WheelchairAccessible = req.WheelchairAccessible,
                HasNursery = req.HasNursery,
                HasYouthProgram = req.HasYouthProgram,
            };
            var created = await service.CreateAsync(church, ct);
            return Results.Created($"/churches/{created.Slug}", created);
        }).RequireAuthorization("ChurchesMod");

        group.MapPut("/{id:guid}", async (
            Guid id,
            ChurchRequest req,
            ChurchService service,
            CancellationToken ct) =>
        {
            var existing = await service.GetByIdAsync(id, ct);
            if (existing is null)
            {
                return Results.NotFound();
            }

            existing.CanonicalName = req.CanonicalName;
            existing.Latitude = req.Latitude;
            existing.Longitude = req.Longitude;
            existing.Street = req.Street;
            existing.City = req.City;
            existing.State = req.State;
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
        }).RequireAuthorization("ChurchesMod");

        group.MapPatch("/{id:guid}", async (
            Guid id,
            PatchChurchRequest req,
            ChurchService service,
            CancellationToken ct) =>
        {
            var existing = await service.GetByIdAsync(id, ct);
            if (existing is null)
            {
                return Results.NotFound();
            }

            if (req.CanonicalName is not null)
            {
                existing.CanonicalName = req.CanonicalName;
            }

            if (req.Latitude is not null)
            {
                existing.Latitude = req.Latitude.Value;
            }

            if (req.Longitude is not null)
            {
                existing.Longitude = req.Longitude.Value;
            }

            if (req.Street is not null)
            {
                existing.Street = req.Street;
            }

            if (req.City is not null)
            {
                existing.City = req.City;
            }

            if (req.State is not null)
            {
                existing.State = req.State;
            }

            if (req.Zip is not null)
            {
                existing.Zip = req.Zip;
            }

            if (req.PhoneNumber is not null)
            {
                existing.PhoneNumber = req.PhoneNumber;
            }

            if (req.Website is not null)
            {
                existing.Website = req.Website;
            }

            if (req.EmailAddress is not null)
            {
                existing.EmailAddress = req.EmailAddress;
            }

            if (req.DenominationId is not null)
            {
                existing.DenominationId = req.DenominationId;
            }

            if (req.WorshipStyle is not null)
            {
                existing.WorshipStyle = req.WorshipStyle.Value;
            }

            if (req.PrimaryLanguage is not null)
            {
                existing.PrimaryLanguage = req.PrimaryLanguage;
            }

            if (req.AcceptsLGBTQ is not null)
            {
                existing.AcceptsLGBTQ = req.AcceptsLGBTQ;
            }

            if (req.WheelchairAccessible is not null)
            {
                existing.WheelchairAccessible = req.WheelchairAccessible;
            }

            if (req.HasNursery is not null)
            {
                existing.HasNursery = req.HasNursery;
            }

            if (req.HasYouthProgram is not null)
            {
                existing.HasYouthProgram = req.HasYouthProgram;
            }

            await service.UpdateAsync(existing, ct);
            return Results.Ok(existing);
        }).RequireAuthorization("ChurchesMod");

        group.MapDelete("/{id:guid}", async (
            Guid id,
            ChurchService service,
            CancellationToken ct) =>
        {
            var deleted = await service.DeleteAsync(id, ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        }).RequireAuthorization("ChurchesMod");

        return app;
    }
}

public record ChurchRequest(
    string CanonicalName,
    double Latitude,
    double Longitude,
    string? Street,
    string City,
    string State,
    string Zip,
    string? PhoneNumber,
    string? Website,
    string? EmailAddress,
    Guid? DenominationId,
    WorshipStyle WorshipStyle,
    string PrimaryLanguage,
    bool? AcceptsLGBTQ,
    bool? WheelchairAccessible,
    bool? HasNursery,
    bool? HasYouthProgram);

public record PatchChurchRequest(
    string? CanonicalName,
    double? Latitude,
    double? Longitude,
    string? Street,
    string? City,
    string? State,
    string? Zip,
    string? PhoneNumber,
    string? Website,
    string? EmailAddress,
    Guid? DenominationId,
    WorshipStyle? WorshipStyle,
    string? PrimaryLanguage,
    bool? AcceptsLGBTQ,
    bool? WheelchairAccessible,
    bool? HasNursery,
    bool? HasYouthProgram);

public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);
