namespace Directory.Schedules;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Directory.Church;

[ExcludeFromCodeCoverage]
public static class ScheduleEndpoints
{
    internal const string Route = "/schedules";

    public static IEndpointRouteBuilder MapScheduleEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost($"{ChurchEndpoints.Route}/{{churchId:guid}}{Route}", async (
            Guid churchId,
            ScheduleRequest req,
            ScheduleService service,
            CancellationToken ct) =>
        {
            if (!TimeOnly.TryParse(req.StartTime, CultureInfo.InvariantCulture, out var startTime) || req.DayOfWeek > 6)
            {
                return Results.BadRequest("Invalid dayOfWeek or startTime.");
            }

            var created = await service.CreateAsync(churchId, req.DayOfWeek, startTime, req.Description, ct);
            return Results.Created($"{Route}/{created.Id}", created);
        }).RequireAuthorization(AuthorizationPolicies.ChurchesModPolicy).WithTags("Schedules");

        app.MapPut($"{Route}/{{id:guid}}", async (
            Guid id,
            ScheduleRequest req,
            ScheduleService service,
            CancellationToken ct) =>
        {
            if (!TimeOnly.TryParse(req.StartTime, CultureInfo.InvariantCulture, out var startTime) || req.DayOfWeek > 6)
            {
                return Results.BadRequest("Invalid dayOfWeek or startTime.");
            }

            return await service.UpdateAsync(id, req.DayOfWeek, startTime, req.Description, ct)
                ? Results.NoContent()
                : Results.NotFound();
        }).RequireAuthorization(AuthorizationPolicies.ChurchesModPolicy).WithTags("Schedules");

        app.MapDelete($"{Route}/{{id:guid}}", async (
            Guid id,
            ScheduleService service,
            CancellationToken ct) =>
            await service.DeleteAsync(id, ct) ? Results.NoContent() : Results.NotFound())
            .RequireAuthorization(AuthorizationPolicies.ChurchesModPolicy).WithTags("Schedules");

        return app;
    }
}
