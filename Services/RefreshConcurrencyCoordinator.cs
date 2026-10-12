using System.Collections.Concurrent;

namespace RssAsp.Services;

public sealed class RefreshConcurrencyCoordinator
{
    private readonly SemaphoreSlim databaseSemaphore = new(1, 1);
    private readonly ConcurrentDictionary<int, SemaphoreSlim> feedSemaphores = new();

    public async ValueTask<IDisposable> EnterDatabaseAsync(CancellationToken cancellationToken)
    {
        await databaseSemaphore.WaitAsync(cancellationToken);
        return new SemaphoreReleaser(databaseSemaphore);
    }

    public async ValueTask<IDisposable> EnterFeedAsync(int feedId, CancellationToken cancellationToken)
    {
        var semaphore = feedSemaphores.GetOrAdd(feedId, static _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);
        return new SemaphoreReleaser(semaphore);
    }

    private sealed class SemaphoreReleaser(SemaphoreSlim semaphore) : IDisposable
    {
        public void Dispose() => semaphore.Release();
    }
}
