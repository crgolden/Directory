namespace Directory.Tests.Unit.Api;

public sealed class AuthorizationPoliciesTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void ScopeClaimType_MatchesTheClaimIdentityIssues()
    {
        // Act
        var claimType = AuthorizationPolicies.ScopeClaimType;

        // Assert
        Assert.Equal("scope", claimType);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void DirectoryScope_MatchesTheScopeIdentityIssues()
    {
        // Act
        var scope = AuthorizationPolicies.DirectoryScope;

        // Assert
        Assert.Equal("directory", scope);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ChurchesModScope_MatchesTheScopeIdentityIssues()
    {
        // Act
        var scope = AuthorizationPolicies.ChurchesModScope;

        // Assert
        Assert.Equal("churches.mod", scope);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ChurchesModClaim_MatchesTheClaimIdentityIssues()
    {
        // Act
        string[] claim = [AuthorizationPolicies.ChurchesModClaimType, AuthorizationPolicies.ChurchesModClaimValue];

        // Assert
        Assert.Equal(["churches.mod", "true"], claim);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void SubjectClaimType_MatchesTheOidcSubjectClaim()
    {
        // Act
        var claimType = AuthorizationPolicies.SubjectClaimType;

        // Assert
        Assert.Equal("sub", claimType);
    }
}
