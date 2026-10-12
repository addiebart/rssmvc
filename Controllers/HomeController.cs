using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RssAsp.Data;
using RssAsp.Models;

namespace RssAsp.Controllers;

public class HomeController : Controller
{
    private const string SortCookie = "rssasp.sort";
    private const string DirectionCookie = "rssasp.direction";
    private const string ViewCookie = "rssasp.view";

    private readonly RssDbContext _context;
    private readonly UserManager<User> _userManager;

    public HomeController(RssDbContext context, UserManager<User> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    [Authorize]
    public async Task<IActionResult> Index(
        [FromQuery] int? subscriptionId = null)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        var subscriptions = await _context.Subscriptions
            .AsNoTracking()
            .Include(subscription => subscription.Feed)
            .Where(subscription => subscription.UserId == user.Id)
            .OrderBy(subscription => subscription.DisplayTitle)
            .ToListAsync();

        var selectedSubscription = subscriptionId.HasValue
            ? subscriptions.SingleOrDefault(subscription => subscription.Id == subscriptionId.Value)
            : null;

        if (subscriptionId.HasValue && selectedSubscription is null)
        {
            return NotFound();
        }

        var articles = _context.Articles
            .AsNoTracking()
            .Include(article => article.Feed)
            .Where(article => article.Feed.Subscriptions.Any(subscription => subscription.UserId == user.Id));

        if (selectedSubscription is not null)
        {
            articles = articles.Where(article => article.FeedId == selectedSubscription.FeedId);
        }

        var sortCriteria = Request.Cookies[SortCookie]?.Trim().ToLowerInvariant();
        sortCriteria = sortCriteria is "date" or "title" or "description" ? sortCriteria : "date";
        var sortDirection = Request.Cookies[DirectionCookie]?.Trim().ToLowerInvariant();
        sortDirection = sortDirection is "asc" or "desc" ? sortDirection : "desc";
        var sortDescending = sortDirection == "desc";

        articles = sortCriteria switch
        {
            "date" => sortDescending
                ? articles.OrderByDescending(article => article.PublishedAt)
                : articles.OrderBy(article => article.PublishedAt),
            "title" => sortDescending
                ? articles.OrderByDescending(article => article.Title)
                : articles.OrderBy(article => article.Title),
            "description" => sortDescending
                ? articles.OrderByDescending(article => article.Summary)
                : articles.OrderBy(article => article.Summary),
            _ => articles
        };

        var articleList = await articles.ToListAsync();
        var viewMode = Request.Cookies[ViewCookie]?.Trim().ToLowerInvariant();
        viewMode = viewMode is "list" or "grid" ? viewMode : "list";

        return View(new HomeViewModel
        {
            Subscriptions = subscriptions,
            Articles = articleList,
            FeedNamesByUrl = subscriptions.ToDictionary(
                subscription => subscription.Feed.FeedUrl,
                subscription => subscription.DisplayTitle,
                StringComparer.OrdinalIgnoreCase),
            SelectedSubscriptionId = selectedSubscription?.Id,
            SortCriteria = sortCriteria,
            SortDirection = sortDirection,
            ViewMode = viewMode
        });
    }

    [Authorize]
    public async Task<IActionResult> Article(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        var article = await _context.Articles
            .AsNoTracking()
            .Include(article => article.Feed)
            .Where(article => article.Id == id &&
                             article.Feed.Subscriptions.Any(subscription => subscription.UserId == user.Id))
            .SingleOrDefaultAsync();

        if (article is null)
        {
            return NotFound();
        }

        var feedName = await _context.Subscriptions
            .Where(subscription => subscription.UserId == user.Id && subscription.FeedId == article.FeedId)
            .Select(subscription => subscription.DisplayTitle)
            .FirstOrDefaultAsync() ?? article.Feed.FeedUrl;

        return View(new ArticleDetailsViewModel
        {
            Article = article,
            FeedName = feedName
        });
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [ActionName("StatusCode")]
    public IActionResult StatusCodePage(int code)
    {
        if (code is < 400 or > 599)
        {
            code = StatusCodes.Status500InternalServerError;
        }

        Response.StatusCode = code;
        return View(new ErrorViewModel
        {
            StatusCode = code,
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
        });
    }
}