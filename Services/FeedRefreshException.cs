namespace RssAsp.Services;

public sealed class FeedRefreshException(string message, Exception innerException)
    : Exception(message, innerException);
