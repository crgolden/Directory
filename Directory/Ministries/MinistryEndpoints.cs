namespace Directory.Ministries;

using System.Diagnostics.CodeAnalysis;
using Directory.Church;

[ExcludeFromCodeCoverage]
public static class MinistryEndpoints
{
    internal const string Route = "/ministries";

    public static IEndpointRouteBuilder MapMinistryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost($"{ChurchEndpoints.Route}/{{churchId:guid}}{Route}", async (
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
            return Results.Created($"{Route}/{created.Id}", created);
        }).RequireAuthorization(AuthorizationPolicies.ChurchesModPolicy).WithTags("Ministries");

        app.MapPut($"{Route}/{{id:guid}}", async (
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
        }).RequireAuthorization(AuthorizationPolicies.ChurchesModPolicy).WithTags("Ministries");

        app.MapDelete($"{Route}/{{id:guid}}", async (
            Guid id,
            MinistryService service,
            CancellationToken ct) =>
            await service.DeleteAsync(id, ct) ? Results.NoContent() : Results.NotFound())
            .RequireAuthorization(AuthorizationPolicies.ChurchesModPolicy).WithTags("Ministries");

        return app;
    }
}
