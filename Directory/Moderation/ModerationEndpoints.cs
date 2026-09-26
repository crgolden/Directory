namespace Directory.Moderation;

using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using Directory.Church;
using Directory.Entities;
using Directory.Enums;

[ExcludeFromCodeCoverage]
public static class ModerationEndpoints
{
    internal const string Route = "/corrections";

    internal const string ApproveSegment = "/approve";

    internal const string RejectSegment = "/reject";

    internal const string MergeSegment = "/merge";

    private const string ChurchesModPolicy = AuthorizationPolicies.ChurchesModPolicy;

    public static IEndpointRouteBuilder MapModerationEndpoints(this IEndpointRouteBuilder app)
    {
        var modGroup = app.MapGroup(Route)
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
        }).RequireAuthorization(ChurchesModPolicy);

        modGroup.MapGet("/{id:guid}", async (
            Guid id,
            ModerationService service,
            CancellationToken ct) =>
        {
            var correction = await service.GetCorrectionByIdAsync(id, ct);
            return correction is null ? Results.NotFound() : Results.Ok(correction);
        }).RequireAuthorization(ChurchesModPolicy);

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

            if (!SubjectClaims.TryRead(user, out var userId))
            {
                return Results.Unauthorized();
            }

            var id = await service.SubmitCorrectionAsync(
                req.ChurchId, userId, req.Field, req.OldValue, req.NewValue, ct);
            return Results.Accepted($"{Route}/{id}", new { Id = id });
        }).RequireAuthorization(AuthorizationPolicies.DirectoryPolicy);

        modGroup.MapPatch($"/{{id:guid}}{ApproveSegment}", async (
            Guid id,
            Guid? survivingId,
            ClaimsPrincipal user,
            ModerationService service,
            CancellationToken ct) =>
        {
            if (!SubjectClaims.TryRead(user, out var reviewedBy))
            {
                return Results.Unauthorized();
            }

            var correction = await service.GetCorrectionByIdAsync(id, ct);
            if (correction is null || correction.Status != CorrectionStatus.Pending)
            {
                return Results.NotFound();
            }

            if (await service.ApplyCorrectionAsync(correction, reviewedBy, survivingId, ct) is { } refusal)
            {
                return Results.BadRequest(refusal);
            }

            var updated = await service.ReviewCorrectionAsync(id, CorrectionStatus.Approved, reviewedBy, ct);
            return updated ? Results.NoContent() : Results.NotFound();
        }).RequireAuthorization(ChurchesModPolicy);

        modGroup.MapPatch($"/{{id:guid}}{RejectSegment}", async (
            Guid id,
            ClaimsPrincipal user,
            ModerationService service,
            CancellationToken ct) =>
        {
            if (!SubjectClaims.TryRead(user, out var reviewedBy))
            {
                return Results.Unauthorized();
            }

            var updated = await service.ReviewCorrectionAsync(id, CorrectionStatus.Rejected, reviewedBy, ct);
            return updated ? Results.NoContent() : Results.NotFound();
        }).RequireAuthorization(ChurchesModPolicy);

        app.MapPost($"{ChurchEndpoints.Route}/{{survivingId:guid}}{MergeSegment}/{{absorbedId:guid}}", async (
            Guid survivingId,
            Guid absorbedId,
            ClaimsPrincipal user,
            ModerationService service,
            CancellationToken ct) =>
        {
            if (!SubjectClaims.TryRead(user, out var mergedBy))
            {
                return Results.Unauthorized();
            }

            await service.MergeAsync(survivingId, absorbedId, mergedBy, ct);
            return Results.NoContent();
        }).WithTags("Moderation").RequireAuthorization(ChurchesModPolicy);

        return app;
    }
}
