namespace RssAsp.Models;

public class Feed
{
    public int Id { get; set; }

    public string FeedUrl { get; set; } = string.Empty;

    public DateTimeOffset LastUpdated { get; set; }

    public DateTimeOffset? LastFailed { get; set; }

    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();

    public ICollection<Article> Articles { get; set; } = new List<Article>();
}
