namespace Directory.Tests.Unit.TestSupport;

using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

internal sealed class IntegrationAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    internal static readonly string SchemeName = Guid.NewGuid().ToString();

    internal static readonly string TestSub = TestValues.NewUserId();

    public IntegrationAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(AuthorizationPolicies.SubjectClaimType, TestSub),
            new Claim(AuthorizationPolicies.ScopeClaimType, AuthorizationPolicies.DirectoryScope),
            new Claim(AuthorizationPolicies.ScopeClaimType, AuthorizationPolicies.ChurchesModScope),
            new Claim(AuthorizationPolicies.ChurchesModClaimType, AuthorizationPolicies.ChurchesModClaimValue),
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}