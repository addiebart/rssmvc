using Microsoft.AspNetCore.Identity;

namespace RssAsp.Models;

public class User : IdentityUser<int>
{
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
}
