namespace Directory.Tests.Unit.Api;

using System.Text.Json;
using Directory.Entities;
using Directory.Enums;
using StateCode = Shared.Domain.StateCode;

[Trait("Category", "Unit")]
public sealed class EntityJsonTests
{
    [Fact]
    public void Church_SerializesStateAsItsUspsCode()
    {
        // Arrange
        var state = Generated.NewStateCode();
        var church = NewChurch(state);

        // Act
        var json = JsonSerializer.SerializeToElement(church, JsonSerializerOptions.Web);

        // Assert
        Assert.Equal(state.ToString(), json.GetProperty(ChurchJsonNames.State).GetString());
    }

    [Fact]
    public void Church_SerializesWorshipStyleAsANumber()
    {
        // Arrange
        var worshipStyle = Generated.NewDefinedValue<WorshipStyle>();
        var church = NewChurch(Generated.NewStateCode());
        church.WorshipStyle = worshipStyle;

        // Act
        var json = JsonSerializer.SerializeToElement(church, JsonSerializerOptions.Web);

        // Assert
        Assert.Equal((int)worshipStyle, json.GetProperty(ChurchJsonNames.WorshipStyle).GetInt32());
    }

    [Fact]
    public void Campus_SerializesStateAsItsUspsCode()
    {
        // Arrange
        var state = Generated.NewStateCode();
        var campus = new Campus
        {
            ChurchId = Guid.NewGuid(),
            Name = Generated.NewName(),
            City = Generated.NewCity(),
            State = state,
            Zip = Generated.NewZip(),
            Latitude = Generated.NewLatitude(),
            Longitude = Generated.NewLongitude(),
        };

        // Act
        var json = JsonSerializer.SerializeToElement(campus, JsonSerializerOptions.Web);

        // Assert
        Assert.Equal(state.ToString(), json.GetProperty("state").GetString());
    }

    private static Church NewChurch(StateCode state) => new()
    {
        CanonicalName = Generated.NewName(),
        Slug = Generated.NewSlug(),
        Latitude = Generated.NewLatitude(),
        Longitude = Generated.NewLongitude(),
        City = Generated.NewCity(),
        State = state,
        Zip = Generated.NewZip(),
        PrimaryLanguage = Generated.NewLanguage(),
    };
}
