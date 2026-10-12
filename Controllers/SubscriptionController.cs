using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RssAsp.Data;
using RssAsp.Models;
using RssAsp.Services;

namespace RssAsp.Controllers;

[Authorize]
public class SubscriptionController : Controller
{
    private readonly RssDbContext _context;
    private readonly UserManager<User> _userManager;
    private readonly IFeedRefreshService _feedRefreshService;

    public SubscriptionController(
        RssDbContext context,
        UserManager<User> userManager,
        IFeedRefreshService feedRefreshService)
    {
        _context = context;
        _userManager = userManager;
        _feedRefreshService = feedRefreshService;
    }

    // POST: /Subscription/Create
    [HttpPost]
    public async Task<IActionResult> Create(SubscriptionInputModel model)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var feedUrl = model.FeedUrl.Trim();
        if (!Uri.TryCreate(feedUrl, UriKind.Absolute, out var parsedFeedUrl) ||
            (parsedFeedUrl.Scheme != Uri.UriSchemeHttp && parsedFeedUrl.Scheme != Uri.UriSchemeHttps) ||
            string.IsNullOrWhiteSpace(parsedFeedUrl.Host))
        {
            ModelState.AddModelError(
                nameof(model.FeedUrl),
                "FeedUrl must be an HTTP or HTTPS URL.");
            return BadRequest(ModelState);
        }

        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(HttpContext.RequestAborted);
        var feed = await _context.Feeds
            .SingleOrDefaultAsync(existingFeed => existingFeed.FeedUrl == feedUrl, HttpContext.RequestAborted);

        if (feed is not null)
        {
            var alreadySubscribed = await _context.Subscriptions
                .AnyAsync(
                    subscription => subscription.UserId == user.Id && subscription.FeedId == feed.Id,
                    HttpContext.RequestAborted);
            if (alreadySubscribed)
            {
                ModelState.AddModelError(nameof(model.FeedUrl), "You are already subscribed to this feed.");
                return BadRequest(ModelState);
            }
        }
        else
        {
            feed = new Feed
            {
                FeedUrl = feedUrl,
                LastUpdated = DateTimeOffset.UtcNow
            };
            _context.Feeds.Add(feed);
        }

        var subscription = new Subscription
        {
            UserId = user.Id,
            Feed = feed,
            DisplayTitle = model.DisplayTitle.Trim()
        };
        _context.Subscriptions.Add(subscription);
        await _context.SaveChangesAsync(HttpContext.RequestAborted);
        try
        {
            await _feedRefreshService.RefreshFeedAsync(
                feed.Id,
                HttpContext.RequestAborted);
        }
        catch (FeedRefreshException)
        {
            ModelState.AddModelError(
                nameof(model.FeedUrl),
                "The feed could not be reached or did not contain a valid feed.");
            return BadRequest(ModelState);
        }
        await transaction.CommitAsync(HttpContext.RequestAborted);

        return RedirectToAction("Index", "Home", new { subscriptionId = subscription.Id });
    }

    // POST: /Subscription/Edit/5
    [HttpPost]
    public async Task<IActionResult> Edit(int id, SubscriptionEditInputModel model, int? subscriptionId)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var subscription = await FindSubscription(id);
        if (subscription is null)
        {
            return NotFound();
        }

        subscription.DisplayTitle = model.DisplayTitle.Trim();
        await _context.SaveChangesAsync();

        return RedirectToAction("Index", "Home", new { subscriptionId });
    }

    // POST: /Subscription/Delete/5
    [HttpPost]
    public async Task<IActionResult> Delete(int id, int? subscriptionId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        var subscription = await _context.Subscriptions
            .Include(subscription => subscription.Feed)
            .SingleOrDefaultAsync(subscription =>
                subscription.Id == id &&
                subscription.UserId == user.Id);

        if (subscription is null)
        {
            return NotFound();
        }

        var feed = subscription.Feed;
        var subscriptionCount = await _context.Subscriptions
            .CountAsync(existingSubscription => existingSubscription.FeedId == subscription.FeedId);

        _context.Subscriptions.Remove(subscription);
        if (subscriptionCount <= 1)
        {
            _context.Feeds.Remove(feed);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(
            "Index",
            "Home",
            subscriptionId == id ? null : new { subscriptionId });
    }

    private async Task<Subscription?> FindSubscription(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return null;
        }

        return await _context.Subscriptions
            .Include(subscription => subscription.Feed)
            .SingleOrDefaultAsync(subscription => subscription.Id == id && subscription.UserId == user.Id);
    }
}
