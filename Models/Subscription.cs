namespace RssAsp.Models;

public class Subscription
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public int FeedId { get; set; }

    public string DisplayTitle { get; set; } = string.Empty;

    public DateTimeOffset SubscribedAt { get; set; } = DateTimeOffset.UtcNow;

    public User User { get; set; } = null!;

    public Feed Feed { get; set; } = null!;
}
