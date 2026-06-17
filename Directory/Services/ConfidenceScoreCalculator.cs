namespace Directory.Services;

using Entities;

public static class ConfidenceScoreCalculator
{
    private const double CoordinateEpsilon = 1e-9;

    public static decimal Calculate(Church church, int attributeCount)
    {
        var score = 0m;

        if (!string.IsNullOrWhiteSpace(church.CanonicalName))
        {
            score += 0.2m;
        }

        if (!string.IsNullOrWhiteSpace(church.City))
        {
            score += 0.2m;
        }

        if (!string.IsNullOrWhiteSpace(church.State))
        {
            score += 0.2m;
        }

        if (!string.IsNullOrWhiteSpace(church.Zip))
        {
            score += 0.2m;
        }

        if (Math.Abs(church.Latitude) > CoordinateEpsilon || Math.Abs(church.Longitude) > CoordinateEpsilon)
        {
            score += 0.2m;
        }

        if (!string.IsNullOrWhiteSpace(church.PhoneNumber))
        {
            score += 0.05m;
        }

        if (!string.IsNullOrWhiteSpace(church.Website))
        {
            score += 0.05m;
        }

        if (!string.IsNullOrWhiteSpace(church.EmailAddress))
        {
            score += 0.05m;
        }

        if (church.DenominationId.HasValue)
        {
            score += 0.05m;
        }

        if (church.WorshipStyle != Enums.WorshipStyle.Unknown)
        {
            score += 0.05m;
        }

        score += Math.Min(attributeCount * 0.01m, 0.2m);

        if (church.LastVerifiedAt.HasValue &&
            DateTimeOffset.UtcNow - church.LastVerifiedAt.Value <= TimeSpan.FromDays(365))
        {
            score += 0.1m;
        }

        return Math.Min(score, 1.0m);
    }
}
