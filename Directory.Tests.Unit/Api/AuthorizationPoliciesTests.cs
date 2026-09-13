namespace Directory.Tests.Unit.Api;

public sealed class AuthorizationPoliciesTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void ScopeClaimType_MatchesTheClaimIdentityIssues()
    {
        Assert.Equal("scope", AuthorizationPolicies.ScopeClaimType);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void DirectoryScope_MatchesTheScopeIdentityIssues()
    {
        Assert.Equal("directory", AuthorizationPolicies.DirectoryScope);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ChurchesModScope_MatchesTheScopeIdentityIssues()
    {
        Assert.Equal("churches.mod", AuthorizationPolicies.ChurchesModScope);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ChurchesModClaim_MatchesTheClaimIdentityIssues()
    {
        Assert.Equal("churches.mod", AuthorizationPolicies.ChurchesModClaimType);
        Assert.Equal("true", AuthorizationPolicies.ChurchesModClaimValue);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void SubjectClaimType_MatchesTheOidcSubjectClaim()
    {
        Assert.Equal("sub", AuthorizationPolicies.SubjectClaimType);
    }
}
