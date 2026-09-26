namespace Directory.User;

using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

[ExcludeFromCodeCoverage]
public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/me", (ClaimsPrincipal user) =>
        {
            if (user.Identity?.IsAuthenticated != true)
            {
                return Results.Ok(new { IsAuthenticated = false });
            }

            return Results.Ok(new
            {
                IsAuthenticated = true,
                Sub = user.FindFirstValue(AuthorizationPolicies.SubjectClaimType),
                Email = user.FindFirstValue(JwtRegisteredClaimNames.Email),
                Name = user.FindFirstValue(JwtRegisteredClaimNames.Name),
            });
        }).WithTags("User").AllowAnonymous();

        return app;
    }
}
