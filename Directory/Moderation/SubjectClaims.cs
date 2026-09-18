namespace Directory.Moderation;

using System.Security.Claims;

internal static class SubjectClaims
{
    internal static bool TryRead(ClaimsPrincipal user, out Guid subject) =>
        Guid.TryParse(user.FindFirstValue(AuthorizationPolicies.SubjectClaimType), out subject);
}
