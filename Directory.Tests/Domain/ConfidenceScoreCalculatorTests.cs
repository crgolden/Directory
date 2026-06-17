namespace Directory.Tests.Domain;

using Entities;
using Enums;
using Services;

public sealed class ConfidenceScoreCalculatorTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Calculate_EmptyChurch_ReturnsZero()
    {
        var score = ConfidenceScoreCalculator.Calculate(EmptyChurch(), 0);
        Assert.Equal(0m, score);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Calculate_AllRequiredFields_ReturnsCappedAtOne()
    {
        var score = ConfidenceScoreCalculator.Calculate(FullChurch(), 0);
        Assert.Equal(1.0m, score);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Calculate_CanonicalNameOnly_AddsPointTwo()
    {
        var church = EmptyChurch();
        church.CanonicalName = "Grace Church";
        var score = ConfidenceScoreCalculator.Calculate(church, 0);
        Assert.Equal(0.2m, score);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Calculate_CityOnly_AddsPointTwo()
    {
        var church = EmptyChurch();
        church.City = "Phoenix";
        var score = ConfidenceScoreCalculator.Calculate(church, 0);
        Assert.Equal(0.2m, score);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Calculate_StateOnly_AddsPointTwo()
    {
        var church = EmptyChurch();
        church.State = "AZ";
        var score = ConfidenceScoreCalculator.Calculate(church, 0);
        Assert.Equal(0.2m, score);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Calculate_ZipOnly_AddsPointTwo()
    {
        var church = EmptyChurch();
        church.Zip = "85001";
        var score = ConfidenceScoreCalculator.Calculate(church, 0);
        Assert.Equal(0.2m, score);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Calculate_NonZeroLatitude_AddsPointTwo()
    {
        var church = EmptyChurch();
        church.Latitude = 33.4;
        var score = ConfidenceScoreCalculator.Calculate(church, 0);
        Assert.Equal(0.2m, score);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Calculate_NonZeroLongitude_AddsPointTwo()
    {
        var church = EmptyChurch();
        church.Longitude = -112.0;
        var score = ConfidenceScoreCalculator.Calculate(church, 0);
        Assert.Equal(0.2m, score);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Calculate_PhoneNumber_AddsPointZeroFive()
    {
        var church = EmptyChurch();
        church.PhoneNumber = "555-1234";
        var score = ConfidenceScoreCalculator.Calculate(church, 0);
        Assert.Equal(0.05m, score);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Calculate_Website_AddsPointZeroFive()
    {
        var church = EmptyChurch();
        church.Website = "https://gracechurch.org";
        var score = ConfidenceScoreCalculator.Calculate(church, 0);
        Assert.Equal(0.05m, score);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Calculate_EmailAddress_AddsPointZeroFive()
    {
        var church = EmptyChurch();
        church.EmailAddress = "info@gracechurch.org";
        var score = ConfidenceScoreCalculator.Calculate(church, 0);
        Assert.Equal(0.05m, score);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Calculate_DenominationId_AddsPointZeroFive()
    {
        var church = EmptyChurch();
        church.DenominationId = Guid.NewGuid();
        var score = ConfidenceScoreCalculator.Calculate(church, 0);
        Assert.Equal(0.05m, score);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Calculate_NonDefaultWorshipStyle_AddsPointZeroFive()
    {
        var church = EmptyChurch();
        church.WorshipStyle = WorshipStyle.Contemporary;
        var score = ConfidenceScoreCalculator.Calculate(church, 0);
        Assert.Equal(0.05m, score);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Calculate_FiveAttributes_AddsPointZeroFive()
    {
        var score = ConfidenceScoreCalculator.Calculate(EmptyChurch(), 5);
        Assert.Equal(0.05m, score);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Calculate_TwentyFiveAttributes_CapsAttributeBonusAtPointTwo()
    {
        var score = ConfidenceScoreCalculator.Calculate(EmptyChurch(), 25);
        Assert.Equal(0.2m, score);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Calculate_RecentVerification_AddsPointOne()
    {
        var church = EmptyChurch();
        church.LastVerifiedAt = DateTimeOffset.UtcNow.AddDays(-30);
        var score = ConfidenceScoreCalculator.Calculate(church, 0);
        Assert.Equal(0.1m, score);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Calculate_VerificationOlderThanOneYear_DoesNotAddBonus()
    {
        var church = EmptyChurch();
        church.LastVerifiedAt = DateTimeOffset.UtcNow.AddDays(-400);
        var score = ConfidenceScoreCalculator.Calculate(church, 0);
        Assert.Equal(0m, score);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Calculate_ScoreNeverExceedsOne()
    {
        var church = FullChurch();
        church.PhoneNumber = "555-1234";
        church.Website = "https://gracechurch.org";
        church.EmailAddress = "info@gracechurch.org";
        church.DenominationId = Guid.NewGuid();
        church.WorshipStyle = WorshipStyle.Contemporary;
        church.LastVerifiedAt = DateTimeOffset.UtcNow.AddDays(-30);
        var score = ConfidenceScoreCalculator.Calculate(church, 30);
        Assert.Equal(1.0m, score);
    }

    private static Church EmptyChurch() => new Church
    {
        CanonicalName = string.Empty,
        Slug = string.Empty,
        Latitude = 0,
        Longitude = 0,
        City = string.Empty,
        State = string.Empty,
        Zip = string.Empty,
        PrimaryLanguage = string.Empty,
    };

    private static Church FullChurch() => new Church
    {
        CanonicalName = "Grace Church",
        Slug = "grace-church-phoenix-az",
        Latitude = 33.4,
        Longitude = -112.0,
        City = "Phoenix",
        State = "AZ",
        Zip = "85001",
        PrimaryLanguage = "English",
    };
}
