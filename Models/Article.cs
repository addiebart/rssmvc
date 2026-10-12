namespace RssAsp.Models;

public class Article
{
    public int Id { get; set; }

    public int FeedId { get; set; }

    public string? FeedItemId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Url { get; set; }

    public string? ImageUrl { get; set; }

    public string? ThumbnailPath { get; set; }

    public string? Author { get; set; }

    public string? Summary { get; set; }

    public string? Content { get; set; }

    public DateTimeOffset PublishedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Feed Feed { get; set; } = null!;
}
