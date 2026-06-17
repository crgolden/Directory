namespace Directory.Moderation;

using System.Security.Claims;
using Church;
using Entities;
using Enums;

public static class ModerationEndpoints
{
    public static IEndpointRouteBuilder MapModerationEndpoints(this IEndpointRouteBuilder app)
    {
        var modGroup = app.MapGroup("/corrections")
            .WithTags("Moderation");

        modGroup.MapGet("/", async (
            CorrectionStatus? status,
            int page,
            int pageSize,
            ModerationService service,
            CancellationToken ct) =>
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 50);
            var (items, total) = await service.GetCorrectionsAsync(status, page, pageSize, ct);
            return Results.Ok(new PagedResult<UserCorrection>(items, total, page, pageSize));
        }).RequireAuthorization("ChurchesMod");

        modGroup.MapGet("/{id:guid}", async (
            Guid id,
            ModerationService service,
            CancellationToken ct) =>
        {
            var correction = await service.GetCorrectionByIdAsync(id, ct);
            return correction is null ? Results.NotFound() : Results.Ok(correction);
        }).RequireAuthorization("ChurchesMod");

        modGroup.MapPost("/", async (
            SubmitCorrectionRequest req,
            ClaimsPrincipal user,
            ChurchService churches,
            ModerationService service,
            CancellationToken ct) =>
        {
            if (!await churches.ExistsAsync(req.ChurchId, ct))
            {
                return Results.NotFound();
            }

            var userId = user.FindFirstValue("sub")
                ?? throw new InvalidOperationException("Missing 'sub' claim.");
            var id = await service.SubmitCorrectionAsync(
                req.ChurchId, userId, req.Field, req.OldValue, req.NewValue, ct);
            return Results.Accepted($"/corrections/{id}", new { Id = id });
        }).RequireAuthorization("Directory");

        modGroup.MapPatch("/{id:guid}/approve", async (
            Guid id,
            ClaimsPrincipal user,
            ModerationService service,
            CancellationToken ct) =>
        {
            var reviewedBy = user.FindFirstValue("sub")
                ?? throw new InvalidOperationException("Missing 'sub' claim.");
            var updated = await service.ReviewCorrectionAsync(id, CorrectionStatus.Approved, reviewedBy, ct);
            return updated ? Results.NoContent() : Results.NotFound();
        }).RequireAuthorization("ChurchesMod");

        modGroup.MapPatch("/{id:guid}/reject", async (
            Guid id,
            ClaimsPrincipal user,
            ModerationService service,
            CancellationToken ct) =>
        {
            var reviewedBy = user.FindFirstValue("sub")
                ?? throw new InvalidOperationException("Missing 'sub' claim.");
            var updated = await service.ReviewCorrectionAsync(id, CorrectionStatus.Rejected, reviewedBy, ct);
            return updated ? Results.NoContent() : Results.NotFound();
        }).RequireAuthorization("ChurchesMod");

        app.MapPost("/churches/{survivingId:guid}/merge/{absorbedId:guid}", async (
            Guid survivingId,
            Guid absorbedId,
            ClaimsPrincipal user,
            ModerationService service,
            CancellationToken ct) =>
        {
            var mergedBy = user.FindFirstValue("sub")
                ?? throw new InvalidOperationException("Missing 'sub' claim.");
            await service.MergeAsync(survivingId, absorbedId, mergedBy, ct);
            return Results.NoContent();
        }).WithTags("Moderation").RequireAuthorization("ChurchesMod");

        return app;
    }
}

public record SubmitCorrectionRequest(Guid ChurchId, string Field, string? OldValue, string NewValue);
