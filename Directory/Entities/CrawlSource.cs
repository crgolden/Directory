namespace Directory.Entities;

using Enums;

public sealed class CrawlSource
{
    public Guid Id { get; init; } = Guid.CreateVersion7(DateTimeOffset.UtcNow);

    public Guid? ChurchId { get; set; }

    required public string Url { get; set; };

    public DateTimeOffset? LastCrawledAt { get; set; }

    public CrawlStatus LastStatus { get; set; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
