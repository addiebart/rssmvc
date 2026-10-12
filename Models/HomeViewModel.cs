namespace RssAsp.Models;

public class HomeViewModel
{
    public IReadOnlyList<Subscription> Subscriptions { get; init; } = [];

    public IReadOnlyList<Article> Articles { get; init; } = [];

    public IReadOnlyDictionary<string, string> FeedNamesByUrl { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public int? SelectedSubscriptionId { get; init; }

    public string SortCriteria { get; init; } = "date";

    public string SortDirection { get; init; } = "desc";

    public string ViewMode { get; init; } = "list";
}
