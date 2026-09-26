namespace Directory.Tests.Unit.Api;

using System.Security.Claims;
using Directory.Moderation;

[Trait("Category", "Unit")]
public sealed class SubjectClaimsTests
{
    [Fact]
    public void TryRead_ReadsASubjectThatIsAGuid()
    {
        // Arrange
        var expectedSubject = Generated.NewUserId();
        var user = UserWithSubject(expectedSubject.ToString());

        // Act
        var read = SubjectClaims.TryRead(user, out var subject);

        // Assert
        Assert.True(read);
        Assert.Equal(expectedSubject, subject);
    }

    [Fact]
    public void TryRead_RefusesASubjectThatIsNotAGuid()
    {
        // Arrange
        var user = UserWithSubject(Generated.NewNonGuidSubject());

        // Act
        var read = SubjectClaims.TryRead(user, out _);

        // Assert
        Assert.False(read);
    }

    [Fact]
    public void TryRead_RefusesAUserWithNoSubject()
    {
        // Arrange
        var user = new ClaimsPrincipal(new ClaimsIdentity());

        // Act
        var read = SubjectClaims.TryRead(user, out _);

        // Assert
        Assert.False(read);
    }

    private static ClaimsPrincipal UserWithSubject(string subject) =>
        new(new ClaimsIdentity([new Claim(AuthorizationPolicies.SubjectClaimType, subject)]));
}
