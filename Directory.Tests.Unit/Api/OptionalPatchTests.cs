namespace Directory.Tests.Unit.Api;

using System.Text.Json;
using Directory.Church;

[Trait("Category", "Unit")]
public sealed class OptionalPatchTests
{
    private static readonly JsonSerializerOptions Options = BuildOptions();

    [Fact]
    public void AbsentMember_HasNoValue_SoApplyPatchLeavesTheStoredValueAlone()
    {
        // Arrange
        var json = JsonPayload.WithNoMembers();

        // Act
        var request = JsonSerializer.Deserialize<PatchChurchRequest>(json, Options);

        // Assert
        Assert.NotNull(request);
        Assert.False(request.PhoneNumber.HasValue);
    }

    [Fact]
    public void ExplicitNull_HasAValue_SoApplyPatchClearsTheColumn()
    {
        // Arrange
        var json = JsonSerializer.Serialize(new { PhoneNumber = (string?)null }, Options);

        // Act
        var request = JsonSerializer.Deserialize<PatchChurchRequest>(json, Options);

        // Assert
        Assert.NotNull(request);
        Assert.True(request.PhoneNumber.HasValue);
        Assert.Null(request.PhoneNumber.Value);
    }

    [Fact]
    public void AbsentAndExplicitNull_AreDistinguishable_WhichIsTheWholePointOfTheType()
    {
        // Arrange
        var absentMember = JsonPayload.WithNoMembers();
        var explicitNullMember = JsonSerializer.Serialize(new { Website = (string?)null }, Options);

        // Act
        var absent = JsonSerializer.Deserialize<PatchChurchRequest>(absentMember, Options);
        var explicitNull = JsonSerializer.Deserialize<PatchChurchRequest>(explicitNullMember, Options);

        // Assert
        Assert.NotNull(absent);
        Assert.NotNull(explicitNull);
        Assert.NotEqual(absent.Website.HasValue, explicitNull.Website.HasValue);
    }

    [Fact]
    public void SuppliedValue_CarriesThroughUnchanged()
    {
        // Arrange
        var phoneNumber = Generated.NewPhoneNumber();
        var json = JsonSerializer.Serialize(new { PhoneNumber = phoneNumber }, Options);

        // Act
        var request = JsonSerializer.Deserialize<PatchChurchRequest>(json, Options);

        // Assert
        Assert.NotNull(request);
        Assert.True(request.PhoneNumber.HasValue);
        Assert.Equal(phoneNumber, request.PhoneNumber.Value);
    }

    [Fact]
    public void Or_ReturnsTheStoredValue_WhenTheMemberWasAbsent()
    {
        // Arrange
        var stored = Generated.NewPhoneNumber();
        var absent = default(Optional<string>);

        // Act
        var result = absent.Or(stored);

        // Assert
        Assert.Equal(stored, result);
    }

    [Fact]
    public void Or_ReturnsNull_WhenTheMemberWasExplicitlyNull()
    {
        // Arrange
        var stored = Generated.NewPhoneNumber();
        var cleared = new Optional<string>(null);

        // Act
        var result = cleared.Or(stored);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Write_AbsentValue_WritesJsonNull()
    {
        // Arrange
        var absent = default(Optional<string>);

        // Act
        var json = JsonSerializer.Serialize(absent, Options);

        // Assert
        Assert.Equal(JsonSerializer.Serialize<string?>(null, Options), json);
    }

    [Fact]
    public void Write_SuppliedValue_WritesTheValue()
    {
        // Arrange
        var phoneNumber = Generated.NewPhoneNumber();
        var supplied = new Optional<string>(phoneNumber);

        // Act
        var json = JsonSerializer.Serialize(supplied, Options);

        // Assert
        Assert.Equal(JsonSerializer.Serialize(phoneNumber, Options), json);
    }

    [Fact]
    public void CreateConverter_OptionalOfAType_ReturnsTheConverterForThatType()
    {
        // Arrange
        var factory = new OptionalConverterFactory();
        var options = new JsonSerializerOptions();

        // Act
        var converter = factory.CreateConverter(typeof(Optional<string>), options);

        // Assert
        Assert.IsType<OptionalConverter<string>>(converter);
    }

    [Fact]
    public void CanConvert_TypeThatIsNotGeneric_IsFalse()
    {
        // Arrange
        var factory = new OptionalConverterFactory();

        // Act
        var canConvert = factory.CanConvert(typeof(string));

        // Assert
        Assert.False(canConvert);
    }

    [Fact]
    public void CanConvert_GenericTypeOtherThanOptional_IsFalse()
    {
        // Arrange
        var factory = new OptionalConverterFactory();

        // Act
        var canConvert = factory.CanConvert(typeof(List<string>));

        // Assert
        Assert.False(canConvert);
    }

    private static JsonSerializerOptions BuildOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new OptionalConverterFactory());
        return options;
    }
}
