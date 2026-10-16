namespace Directory.Tests.Unit.Api;

using Directory.Entities;

[Trait("Category", "Unit")]
public sealed class ChurchDefaultsTests
{
    [Fact]
    public void NewChurch_IsActiveWithNoChildren()
    {
        // Act
        var church = new Church
        {
            CanonicalName = Generated.NewName(),
            Slug = Generated.NewSlug(),
            Latitude = Generated.NewLatitude(),
            Longitude = Generated.NewLongitude(),
            City = Generated.NewCity(),
            State = Generated.NewStateCode(),
            Zip = Generated.NewZip(),
            PrimaryLanguage = Generated.NewLanguage(),
        };

        // Assert
        Assert.True(church.IsActive);
        Assert.Empty(church.Schedules);
        Assert.Empty(church.Ministries);
        Assert.Empty(church.Campuses);
    }
}
