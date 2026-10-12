using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using CodeHollow.FeedReader;
using CodeHollow.FeedReader.Feeds;
using CodeHollow.FeedReader.Feeds.MediaRSS;
using Ganss.Xss;
using ImageMagick;
using Microsoft.EntityFrameworkCore;
using RssAsp.Data;
using RssAsp.Models;
using RssFeed = RssAsp.Models.Feed;

namespace RssAsp.Services;

public class FeedRefreshService(
    RssDbContext context,
    HttpClient httpClient,
    IWebHostEnvironment environment,
    RefreshConcurrencyCoordinator concurrencyCoordinator) : IFeedRefreshService
{
    private static readonly TimeSpan XmlFetchTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ImageFetchTimeout = TimeSpan.FromSeconds(10);
    private const long MaxXmlBytes = 256 * 1024;
    private const long MaxImageBytes = 20 * 1024 * 1024;
    private const string ThumbnailDirectory = "article-thumbnails";
    private const string MissingThumbnailPath = "/images/image-missing.svg";
    public async Task<string> RefreshFeedAsync(
        int feedId,
        CancellationToken cancellationToken = default)
    {
        using var feedLock = await concurrencyCoordinator.EnterFeedAsync(feedId, cancellationToken);
        var feed = await context.Feeds
            .SingleOrDefaultAsync(existingFeed => existingFeed.Id == feedId, cancellationToken);

        if (feed is null)
        {
            throw new KeyNotFoundException($"Feed with ID {feedId} was not found.");
        }

        try
        {
            var xml = await DownloadXmlAsync(feed.FeedUrl, cancellationToken);
            await UpdateFeedAsync(feed, xml, cancellationToken);
            return xml;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (FeedRefreshException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new FeedRefreshException($"Feed {feed.FeedUrl} failed to refresh.", exception);
        }
    }

    public async Task RefreshTargetFeedsAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-5);
        var failureCutoff = DateTimeOffset.UtcNow.AddHours(-6);
        var feeds = await context.Subscriptions
            .AsNoTracking()
            .Where(subscription =>
                subscription.UserId == userId &&
                (subscription.Feed.LastFailed == null || subscription.Feed.LastFailed < failureCutoff) &&
                subscription.Feed.LastUpdated < cutoff)
            .Select(subscription => new
            {
                subscription.FeedId,
                subscription.Feed.FeedUrl
            })
            .Select(feed => new FeedTarget(feed.FeedId, feed.FeedUrl))
            .Distinct()
            .ToListAsync(cancellationToken);

        using var semaphore = new SemaphoreSlim(10);
        try
        {
            var refreshedFeeds = await Task.WhenAll(
                feeds.Select(feed => DownloadFeedSemaphoreAsync(feed, semaphore, cancellationToken)));

            foreach (var refreshedFeed in refreshedFeeds)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var feedLock = await concurrencyCoordinator.EnterFeedAsync(
                    refreshedFeed.FeedId,
                    cancellationToken);
                var feed = await context.Feeds
                    .SingleAsync(existingFeed => existingFeed.Id == refreshedFeed.FeedId, cancellationToken);
                await UpdateFeedAsync(feed, refreshedFeed.Xml, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (FeedRefreshException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new FeedRefreshException("One or more feeds failed to refresh.", exception);
        }
    }

    private async Task<RefreshedFeed> DownloadFeedSemaphoreAsync(
        FeedTarget feed,
        SemaphoreSlim semaphore,
        CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            try
            {
                var xml = await DownloadXmlAsync(feed.FeedUrl, cancellationToken);
                return new RefreshedFeed(feed.FeedId, xml);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new FeedRefreshException($"Feed {feed.FeedUrl} failed to refresh.", exception);
            }
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task<string> DownloadXmlAsync(
        string feedUrl,
        CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(XmlFetchTimeout);
        using var response = await httpClient.GetAsync(
            feedUrl,
            HttpCompletionOption.ResponseHeadersRead,
            timeoutSource.Token);
        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentType?.MediaType is not { } mediaType ||
            !IsXmlContentType(mediaType))
        {
            throw new InvalidDataException("Feed response did not have an XML content type.");
        }

        if (response.Content.Headers.ContentLength > MaxXmlBytes)
        {
            throw new InvalidDataException($"XML response exceeded the {MaxXmlBytes} byte limit.");
        }

        var xmlBytes = await ReadContentWithLimitAsync(
            response.Content,
            MaxXmlBytes,
            timeoutSource.Token);
        return System.Text.Encoding.UTF8.GetString(xmlBytes);
    }

    private static bool IsXmlContentType(string mediaType)
    {
        return mediaType.Equals("application/xml", StringComparison.OrdinalIgnoreCase) ||
               mediaType.Equals("text/xml", StringComparison.OrdinalIgnoreCase) ||
               mediaType.EndsWith("+xml", StringComparison.OrdinalIgnoreCase);
    }

    private async Task UpdateFeedAsync(
        RssFeed feed,
        string xml,
        CancellationToken cancellationToken)
    {
        try
        {
            var articles = ParseArticles(feed.Id, xml);
            var articleIds = articles
                .Select(article => article.FeedItemId!)
                .ToList();
            var existingArticles = await context.Articles
                .Where(article => article.FeedId == feed.Id && article.FeedItemId != null && articleIds.Contains(article.FeedItemId))
                .ToDictionaryAsync(article => article.FeedItemId!, cancellationToken);
            var previousImageUrls = existingArticles.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.ImageUrl,
                StringComparer.OrdinalIgnoreCase);
            var trackedArticles = new Dictionary<string, Article>(StringComparer.OrdinalIgnoreCase);

            foreach (var article in articles)
            {
                if (existingArticles.TryGetValue(article.FeedItemId!, out var existingArticle))
                {
                    existingArticle.Title = article.Title;
                    existingArticle.FeedItemId = article.FeedItemId;
                    existingArticle.ImageUrl = article.ImageUrl;
                    existingArticle.Author = article.Author;
                    existingArticle.Summary = article.Summary;
                    existingArticle.Content = article.Content;
                    existingArticle.PublishedAt = article.PublishedAt;
                }
                else
                {
                    context.Articles.Add(article);
                    existingArticle = article;
                }

                trackedArticles[article.FeedItemId!] = existingArticle;
            }

            using var thumbnailSemaphore = new SemaphoreSlim(10);
            var thumbnailGroups = articles
                .Where(article => !string.IsNullOrWhiteSpace(article.ImageUrl))
                .GroupBy(article => article.ImageUrl!, StringComparer.Ordinal)
                .ToList();
            await Task.WhenAll(thumbnailGroups.Select(async group =>
            {
                await thumbnailSemaphore.WaitAsync(cancellationToken);
                try
                {
                    var groupArticles = group
                        .Select(article => trackedArticles[article.FeedItemId!])
                        .ToList();
                    var representative = groupArticles.FirstOrDefault(article =>
                        previousImageUrls.TryGetValue(article.FeedItemId!, out var previousImageUrl) &&
                        string.Equals(previousImageUrl, group.Key, StringComparison.Ordinal) &&
                        article.ThumbnailPath is not null &&
                        File.Exists(GetThumbnailFile(article.ThumbnailPath))) ?? groupArticles[0];

                    previousImageUrls.TryGetValue(representative.FeedItemId!, out var previousImageUrl);
                    try
                    {
                        await UpdateThumbnailAsync(representative, previousImageUrl, cancellationToken);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception)
                    {
                        representative.ThumbnailPath = MissingThumbnailPath;
                    }

                    foreach (var article in groupArticles)
                    {
                        article.ThumbnailPath = representative.ThumbnailPath;
                    }
                }
                finally
                {
                    thumbnailSemaphore.Release();
                }
            }));

            await Task.WhenAll(articles
                .Where(article => string.IsNullOrWhiteSpace(article.ImageUrl))
                .Select(async article =>
                {
                    await thumbnailSemaphore.WaitAsync(cancellationToken);
                    try
                    {
                        var trackedArticle = trackedArticles[article.FeedItemId!];
                        previousImageUrls.TryGetValue(article.FeedItemId!, out var previousImageUrl);
                        try
                        {
                            await UpdateThumbnailAsync(trackedArticle, previousImageUrl, cancellationToken);
                        }
                        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                        {
                            throw;
                        }
                        catch (Exception)
                        {
                            trackedArticle.ThumbnailPath = null;
                        }
                    }
                    finally
                    {
                        thumbnailSemaphore.Release();
                    }
                }));

            feed.LastUpdated = DateTimeOffset.UtcNow;
            feed.LastFailed = null;
            using (await concurrencyCoordinator.EnterDatabaseAsync(cancellationToken))
            {
                await context.SaveChangesAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            feed.LastFailed = DateTimeOffset.UtcNow;
            using (await concurrencyCoordinator.EnterDatabaseAsync(cancellationToken))
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            throw;
        }
    }

    private async Task UpdateThumbnailAsync(
        Article article,
        string? previousImageUrl,
        CancellationToken cancellationToken)
    {
        var thumbnailPath = article.ThumbnailPath;
        var thumbnailFile = GetThumbnailFile(thumbnailPath);
        var imageChanged = !string.Equals(previousImageUrl, article.ImageUrl, StringComparison.Ordinal);

        if (string.IsNullOrWhiteSpace(article.ImageUrl))
        {
            DeleteThumbnail(thumbnailFile);
            article.ThumbnailPath = null;
            return;
        }

        if (!imageChanged && thumbnailFile is not null && File.Exists(thumbnailFile))
        {
            return;
        }

        if (!Uri.TryCreate(article.ImageUrl, UriKind.Absolute, out var imageUri) ||
            (imageUri.Scheme != Uri.UriSchemeHttp && imageUri.Scheme != Uri.UriSchemeHttps))
        {
            article.ThumbnailPath = MissingThumbnailPath;
            return;
        }

        var thumbnailDirectory = Path.Combine(environment.WebRootPath, ThumbnailDirectory);
        Directory.CreateDirectory(thumbnailDirectory);
        var fileName = $"{Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(article.ImageUrl))).ToLowerInvariant()}.avif";
        var outputPath = Path.Combine(thumbnailDirectory, fileName);

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(ImageFetchTimeout);
        using var response = await httpClient.GetAsync(
            imageUri,
            HttpCompletionOption.ResponseHeadersRead,
            timeoutSource.Token);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentType?.MediaType is not { } mediaType ||
            !mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            article.ThumbnailPath = MissingThumbnailPath;
            return;
        }

        if (response.Content.Headers.ContentLength > MaxImageBytes)
        {
            article.ThumbnailPath = MissingThumbnailPath;
            return;
        }

        var imageBytes = await ReadContentWithLimitAsync(
            response.Content,
            MaxImageBytes,
            timeoutSource.Token);
        using var imageStream = new MemoryStream(imageBytes, writable: false);
        using var image = new MagickImage(imageStream);
        image.Resize(new MagickGeometry(0, 256));
        image.Format = MagickFormat.Avif;
        image.Quality = 60;
        DeleteThumbnail(thumbnailFile);
        image.Write(outputPath);
        article.ThumbnailPath = $"/{ThumbnailDirectory}/{fileName}";
    }

    private string? GetThumbnailFile(string? thumbnailPath)
    {
        return thumbnailPath is null
            ? null
            : Path.Combine(environment.WebRootPath, thumbnailPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
    }

    private static async Task<byte[]> ReadContentWithLimitAsync(
        HttpContent content,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        await using var contentStream = await content.ReadAsStreamAsync(cancellationToken);
        await using var buffer = new MemoryStream();
        var chunkSize = 81920;
        var chunk = new byte[chunkSize];

        while (true)
        {
            var bytesRead = await contentStream.ReadAsync(chunk, cancellationToken);
            if (bytesRead == 0)
            {
                return buffer.ToArray();
            }

            if (buffer.Length > maxBytes - bytesRead)
            {
                throw new InvalidDataException($"Response exceeded the {maxBytes} byte limit.");
            }

            await buffer.WriteAsync(chunk.AsMemory(0, bytesRead), cancellationToken);
        }
    }

    private static void DeleteThumbnail(string? path)
    {
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static IReadOnlyList<Article> ParseArticles(int feedId, string xml)
    {
        var parsedFeed = FeedReader.ReadFromString(xml);

        return parsedFeed.Items
            .Where(item => !string.IsNullOrWhiteSpace(item.Id) &&
                           !string.IsNullOrWhiteSpace(item.Title))
            .Select(item => new Article
            {
                FeedId = feedId,
                FeedItemId = item.Id.Trim(),
                Title = StripPlainText(item.Title),
                Url = string.IsNullOrWhiteSpace(item.Link) ? null : item.Link.Trim(),
                ImageUrl = GetArticleImageUrl(item),
                Author = item.Author,
                Summary = item.Description is null ? null : SanitizeSummary(item.Description),
                Content = item.Content,
                PublishedAt = item.PublishingDate ?? DateTimeOffset.UtcNow
            })
            .GroupBy(article => article.FeedItemId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    private static string? GetArticleImageUrl(FeedItem item)
    {
        if (item.SpecificItem is MediaRssFeedItem mediaItem)
        {
            var media = mediaItem.Media?
                .FirstOrDefault(candidate =>
                    candidate.Medium == Medium.Image ||
                    candidate.Type?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true);

            if (!string.IsNullOrWhiteSpace(media?.Url))
            {
                return media.Url.Trim();
            }

            var thumbnail = media?.Thumbnails?.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(thumbnail?.Url))
            {
                return thumbnail.Url.Trim();
            }

            if (mediaItem.Enclosure?.MediaType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true)
            {
                return mediaItem.Enclosure.Url?.Trim();
            }
        }

        if (item.SpecificItem is Rss20FeedItem rssItem &&
            rssItem.Enclosure?.MediaType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true)
        {
            return rssItem.Enclosure.Url?.Trim();
        }

        foreach (var post in new[] { item.Content, item.Description })
        {
            if (string.IsNullOrWhiteSpace(post))
            {
                continue;
            }

            var imageMatch = Regex.Match(
                post,
                """<(?:img|image)\b[^>]*?\b(?:href|src)\s*=\s*["']([^"']+)["']""",
                RegexOptions.IgnoreCase);
            if (imageMatch.Success)
            {
                return WebUtility.HtmlDecode(imageMatch.Groups[1].Value).Trim();
            }
        }

        return null;
    }

    private static string StripPlainText(string value)
    {
        var decoded = WebUtility.HtmlDecode(value);
        return Regex.Replace(decoded, "<[^>]*>", " ").Trim();
    }

    private static string SanitizeSummary(string value)
    {
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedTags.Clear();
        sanitizer.AllowedTags.Add("a");
        sanitizer.AllowedTags.Add("p");
        sanitizer.AllowedAttributes.Clear();
        sanitizer.AllowedAttributes.Add("href");
        sanitizer.AllowedSchemes.Clear();
        sanitizer.AllowedSchemes.Add("http");
        sanitizer.AllowedSchemes.Add("https");

        var sanitized = sanitizer.Sanitize(value);
        return Regex.Replace(sanitized, "<(?!/?a(?:\\s|>))[^>]*>", " ").Trim();
    }
    
    private sealed record FeedTarget(int FeedId, string FeedUrl);

    private sealed record RefreshedFeed(int FeedId, string Xml);
}
