namespace Directory.Tests.Unit.Api;

using System.Security.Claims;
using Moderation;
using TestSupport;

public sealed class SubjectClaimsTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void TryRead_ReadsASubjectThatIsAGuid()
    {
        // Arrange
        var expectedSubject = TestValues.NewUserId();
        var user = UserWithSubject(expectedSubject.ToString());

        // Act
        var read = SubjectClaims.TryRead(user, out var subject);

        // Assert
        Assert.True(read);
        Assert.Equal(expectedSubject, subject);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void TryRead_RefusesASubjectThatIsNotAGuid()
    {
        // Arrange
        var user = UserWithSubject(TestValues.NewNonGuidSubject());

        // Act
        var read = SubjectClaims.TryRead(user, out _);

        // Assert
        Assert.False(read);
    }

    [Fact]
    [Trait("Category", "Unit")]
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
