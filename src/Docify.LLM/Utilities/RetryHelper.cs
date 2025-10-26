using Microsoft.Extensions.Logging;

namespace Docify.LLM.Utilities;

/// <summary>
/// Provides retry logic with exponential backoff for transient failures.
/// Used primarily for LLM API calls that may experience rate limits or network issues.
/// </summary>
public static class RetryHelper
{
    /// <summary>
    /// Executes an async operation with exponential backoff retry logic.
    /// </summary>
    /// <typeparam name="T">The return type of the operation.</typeparam>
    /// <param name="operation">The async operation to execute with retries.</param>
    /// <param name="maxAttempts">Maximum number of attempts (default: 5).</param>
    /// <param name="initialDelay">Initial delay before first retry (default: 1 second).</param>
    /// <param name="backoffMultiplier">Multiplier for exponential backoff (default: 2x).</param>
    /// <param name="logger">Logger for recording retry attempts.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>The result of the successful operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when operation or logger is null.</exception>
    /// <exception cref="ArgumentException">Thrown when maxAttempts is less than 1.</exception>
    public static async Task<T> ExecuteWithRetry<T>(
        Func<Task<T>> operation,
        int maxAttempts = 5,
        TimeSpan? initialDelay = null,
        double backoffMultiplier = 2.0,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (maxAttempts < 1)
        {
            throw new ArgumentException("Max attempts must be at least 1", nameof(maxAttempts));
        }

        var delay = initialDelay ?? TimeSpan.FromSeconds(1);
        Exception? lastException = null;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (IsTransientError(ex) && attempt < maxAttempts)
            {
                lastException = ex;
                var currentDelay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * Math.Pow(backoffMultiplier, attempt - 1));

                logger?.LogWarning(
                    ex,
                    "Attempt {Attempt}/{MaxAttempts} failed with transient error. Retrying in {Delay}ms...",
                    attempt,
                    maxAttempts,
                    currentDelay.TotalMilliseconds);

                await Task.Delay(currentDelay, cancellationToken);
            }
            catch (Exception ex) when (!IsTransientError(ex))
            {
                logger?.LogError(ex, "Non-transient error occurred. Not retrying.");
                throw;
            }
        }

        logger?.LogError(lastException, "All {MaxAttempts} attempts failed", maxAttempts);
        throw lastException!;
    }

    /// <summary>
    /// Determines if an exception represents a transient error that should be retried.
    /// </summary>
    /// <param name="exception">The exception to check.</param>
    /// <returns>True if the error is transient and should be retried; otherwise, false.</returns>
    private static bool IsTransientError(Exception exception)
    {
        // Check for HTTP-related exceptions
        if (exception is HttpRequestException)
        {
            return true;
        }

        // Check for timeout exceptions
        if (exception is TaskCanceledException or TimeoutException)
        {
            return true;
        }

        // Check exception message for rate limit indicators (429)
        var message = exception.Message?.ToLowerInvariant() ?? string.Empty;
        if (message.Contains("429") || message.Contains("rate limit") || message.Contains("too many requests"))
        {
            return true;
        }

        // Check for server errors (5xx)
        if (message.Contains("500") || message.Contains("502") || message.Contains("503") || message.Contains("504") ||
            message.Contains("internal server error") || message.Contains("bad gateway") ||
            message.Contains("service unavailable") || message.Contains("gateway timeout"))
        {
            return true;
        }

        // Do NOT retry auth failures (401), invalid requests (400), or other client errors
        if (message.Contains("401") || message.Contains("unauthorized") ||
            message.Contains("400") || message.Contains("bad request") ||
            message.Contains("403") || message.Contains("forbidden") ||
            message.Contains("404") || message.Contains("not found"))
        {
            return false;
        }

        return false;
    }
}
