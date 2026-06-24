namespace Directory.Ministries;

using System.Diagnostics.CodeAnalysis;

[ExcludeFromCodeCoverage]
public static class MinistryEndpoints
{
    public static IEndpointRouteBuilder MapMinistryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/churches/{churchId:guid}/ministries", async (
            Guid churchId,
            MinistryRequest req,
            MinistryService service,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Name))
            {
                return Results.BadRequest("Name is required.");
            }

            var created = await service.CreateAsync(churchId, req.Name, req.Description, ct);
            return Results.Created($"/ministries/{created.Id}", created);
        }).RequireAuthorization("ChurchesMod").WithTags("Ministries");

        app.MapPut("/ministries/{id:guid}", async (
            Guid id,
            MinistryRequest req,
            MinistryService service,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Name))
            {
                return Results.BadRequest("Name is required.");
            }

            return await service.UpdateAsync(id, req.Name, req.Description, ct)
                ? Results.NoContent()
                : Results.NotFound();
        }).RequireAuthorization("ChurchesMod").WithTags("Ministries");

        app.MapDelete("/ministries/{id:guid}", async (
            Guid id,
            MinistryService service,
            CancellationToken ct) =>
            await service.DeleteAsync(id, ct) ? Results.NoContent() : Results.NotFound())
            .RequireAuthorization("ChurchesMod").WithTags("Ministries");

        return app;
    }
}

public record MinistryRequest(string Name, string? Description);
