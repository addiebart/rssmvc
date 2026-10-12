using Microsoft.EntityFrameworkCore;
using RssAsp.Data;

namespace RssAsp.Services;

public sealed class FeedRefreshBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<FeedRefreshBackgroundService> logger,
    RefreshConcurrencyCoordinator concurrencyCoordinator)
    : BackgroundService, IFeedRefreshBackgroundService
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan FailureCooldown = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RefreshStaleFeedsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "The feed refresh cycle failed.");
            }

            await Task.Delay(RefreshInterval, stoppingToken);
        }
    }

    private async Task RefreshStaleFeedsAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<RssDbContext>();
        var now = DateTimeOffset.UtcNow;
        var staleBefore = now - RefreshInterval;
        var retryAfter = now - FailureCooldown;
        var feedIds = await context.Feeds
            .AsNoTracking()
            .Where(feed =>
                feed.LastUpdated < staleBefore &&
                (feed.LastFailed == null || feed.LastFailed < retryAfter))
            .Select(feed => feed.Id)
            .ToListAsync(cancellationToken);

        using var semaphore = new SemaphoreSlim(10);
        await Task.WhenAll(feedIds.Select(feedId =>
            RefreshFeedAsync(feedId, semaphore, cancellationToken)));
    }

    private async Task RefreshFeedAsync(
        int feedId,
        SemaphoreSlim semaphore,
        CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var refreshService = scope.ServiceProvider.GetRequiredService<IFeedRefreshService>();
            await refreshService.RefreshFeedAsync(feedId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Feed {FeedId} failed to refresh.", feedId);
            await RecordFailureAsync(feedId, cancellationToken);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task RecordFailureAsync(int feedId, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<RssDbContext>();
        var feed = await context.Feeds.SingleOrDefaultAsync(
            existingFeed => existingFeed.Id == feedId,
            cancellationToken);

        if (feed is null)
        {
            return;
        }

        feed.LastFailed = DateTimeOffset.UtcNow;
        using (await concurrencyCoordinator.EnterDatabaseAsync(cancellationToken))
        {
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
