namespace Directory.User;

using System.Security.Claims;

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
                Sub = user.FindFirstValue("sub"),
                Email = user.FindFirstValue("email"),
                Name = user.FindFirstValue("name"),
                HasModerationScope = user.HasClaim("scope", "churches.mod"),
            });
        }).WithTags("User").AllowAnonymous();

        return app;
    }
}
