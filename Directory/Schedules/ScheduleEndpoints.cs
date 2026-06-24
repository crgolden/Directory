namespace Directory.Schedules;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

[ExcludeFromCodeCoverage]
public static class ScheduleEndpoints
{
    public static IEndpointRouteBuilder MapScheduleEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/churches/{churchId:guid}/schedules", async (
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
            return Results.Created($"/schedules/{created.Id}", created);
        }).RequireAuthorization("ChurchesMod").WithTags("Schedules");

        app.MapPut("/schedules/{id:guid}", async (
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
        }).RequireAuthorization("ChurchesMod").WithTags("Schedules");

        app.MapDelete("/schedules/{id:guid}", async (
            Guid id,
            ScheduleService service,
            CancellationToken ct) =>
            await service.DeleteAsync(id, ct) ? Results.NoContent() : Results.NotFound())
            .RequireAuthorization("ChurchesMod").WithTags("Schedules");

        return app;
    }
}

public record ScheduleRequest(byte DayOfWeek, string StartTime, string? Description);
