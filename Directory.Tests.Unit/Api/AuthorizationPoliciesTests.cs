namespace Directory.Tests.Unit.Api;

using System.Security.Claims;

[Trait("Category", "Unit")]
public sealed class AuthorizationPoliciesTests
{
    [Fact]
    public void ScopeClaimType_MatchesTheClaimIdentityIssues()
    {
        // Act
        var claimType = AuthorizationPolicies.ScopeClaimType;

        // Assert
        Assert.Equal("scope", claimType);
    }

    [Fact]
    public void DirectoryScope_MatchesTheScopeIdentityIssues()
    {
        // Act
        var scope = AuthorizationPolicies.DirectoryScope;

        // Assert
        Assert.Equal("directory", scope);
    }

    [Fact]
    public void ChurchesModClaim_MatchesTheClaimIdentityIssues()
    {
        // Act
        string[] claim = [AuthorizationPolicies.ChurchesModClaimType, AuthorizationPolicies.ChurchesModClaimValue];

        // Assert
        Assert.Equal(["churches.mod", "true"], claim);
    }

    [Fact]
    public void GrantsChurchesMod_AcceptsTheClaimIdentityIssues()
    {
        // Arrange
        var user = PrincipalWith(
            new Claim(AuthorizationPolicies.ChurchesModClaimType, AuthorizationPolicies.ChurchesModClaimValue));

        // Act
        var granted = AuthorizationPolicies.GrantsChurchesMod(user);

        // Assert
        Assert.True(granted);
    }

    [Fact]
    public void GrantsChurchesMod_RefusesAUserCarryingNoClaims()
    {
        // Arrange
        var user = PrincipalWith();

        // Act
        var granted = AuthorizationPolicies.GrantsChurchesMod(user);

        // Assert
        Assert.False(granted);
    }

    [Fact]
    public void GrantsChurchesMod_RefusesTheClaimTypeCarryingAnotherValue()
    {
        // Arrange
        var user = PrincipalWith(
            new Claim(AuthorizationPolicies.ChurchesModClaimType, Generated.NewTokenOtherThan(AuthorizationPolicies.ChurchesModClaimValue)));

        // Act
        var granted = AuthorizationPolicies.GrantsChurchesMod(user);

        // Assert
        Assert.False(granted);
    }

    [Fact]
    public void GrantsChurchesMod_RefusesAnotherClaimTypeCarryingTheValue()
    {
        // Arrange
        var user = PrincipalWith(
            new Claim(Generated.NewTokenOtherThan(AuthorizationPolicies.ChurchesModClaimType), AuthorizationPolicies.ChurchesModClaimValue));

        // Act
        var granted = AuthorizationPolicies.GrantsChurchesMod(user);

        // Assert
        Assert.False(granted);
    }

    [Fact]
    public void GrantsChurchesMod_RefusesTheScopeShapeIdentityNeverIssues()
    {
        // Arrange
        var user = PrincipalWith(
            new Claim(AuthorizationPolicies.ScopeClaimType, AuthorizationPolicies.ChurchesModClaimType));

        // Act
        var granted = AuthorizationPolicies.GrantsChurchesMod(user);

        // Assert
        Assert.False(granted);
    }

    [Fact]
    public void SubjectClaimType_MatchesTheOidcSubjectClaim()
    {
        // Act
        var claimType = AuthorizationPolicies.SubjectClaimType;

        // Assert
        Assert.Equal("sub", claimType);
    }

    private static ClaimsPrincipal PrincipalWith(params Claim[] claims) =>
        new ClaimsPrincipal(new ClaimsIdentity(claims, AuthorizationPolicies.ChurchesModPolicy));
}
