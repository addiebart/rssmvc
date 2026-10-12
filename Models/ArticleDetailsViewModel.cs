namespace RssAsp.Models;

public class ArticleDetailsViewModel
{
    public Article Article { get; set; } = null!;

    public string FeedName { get; set; } = string.Empty;
}
