namespace RssAsp.Services;

public interface IFeedRefreshService
{
    Task<string> RefreshFeedAsync(
        int feedId,
        CancellationToken cancellationToken = default);

    Task RefreshTargetFeedsAsync(int userId, CancellationToken cancellationToken = default);
}
