namespace Directory;

using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

internal static class AuthorizationPolicies
{
    internal const string DirectoryPolicy = "Directory";

    internal const string ChurchesModPolicy = "ChurchesMod";

    internal const string ScopeClaimType = OpenIdConnectParameterNames.Scope;

    internal const string DirectoryScope = "directory";

    internal const string ChurchesModClaimType = "churches.mod";

    internal const string ChurchesModClaimValue = "true";

    internal const string SubjectClaimType = JwtRegisteredClaimNames.Sub;

    internal static bool GrantsChurchesMod(ClaimsPrincipal user) =>
        user.HasClaim(ChurchesModClaimType, ChurchesModClaimValue);
}
