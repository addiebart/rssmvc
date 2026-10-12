using System.ComponentModel.DataAnnotations;

namespace RssAsp.Models;

public class SubscriptionInputModel
{
    [Required]
    [Url]
    public string FeedUrl { get; set; } = string.Empty;

    [Required]
    [StringLength(250)]
    public string DisplayTitle { get; set; } = string.Empty;
}

public class SubscriptionEditInputModel
{
    [Required]
    [StringLength(250)]
    public string DisplayTitle { get; set; } = string.Empty;
}
