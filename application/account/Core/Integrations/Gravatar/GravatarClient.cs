using System.Net;
using System.Security.Cryptography;
using System.Text;
using Polly.CircuitBreaker;
using Polly.Timeout;
using SharedKernel.Domain;

namespace Account.Integrations.Gravatar;

public sealed record Gravatar(Stream Stream, string ContentType);

public sealed class GravatarClient(HttpClient httpClient, ILogger<GravatarClient> logger)
{
    public async Task<Gravatar?> GetGravatar(UserId userId, string email, CancellationToken cancellationToken)
    {
        try
        {
            var hash = Convert.ToHexString(MD5.HashData(Encoding.ASCII.GetBytes(email)));
            var gravatarUrl = $"avatar/{hash.ToLowerInvariant()}?d=404";

            var response = await httpClient.GetAsync(gravatarUrl, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                logger.LogInformation("No Gravatar found for user {UserId}", userId);
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Failed to fetch Gravatar for user {UserId}. Status Code: {StatusCode}", userId, response.StatusCode);
                return null;
            }

            return new Gravatar(
                await response.Content.ReadAsStreamAsync(cancellationToken),
                response.Content.Headers.ContentType?.MediaType!
            );
        }
        catch (TimeoutRejectedException ex)
        {
            // Polly's AttemptTimeout / TotalRequestTimeout strategy raises this on a hung server before any HTTP response.
            logger.LogError(ex, "Resilience timeout fetching gravatar for user {UserId}. Treating as unavailable", userId);
            return null;
        }
        catch (BrokenCircuitException ex)
        {
            // Polly opened the circuit after repeated failures; subsequent calls fail fast until the circuit closes.
            logger.LogError(ex, "Circuit open while fetching gravatar for user {UserId}. Treating as unavailable", userId);
            return null;
        }
        catch (HttpRequestException ex)
        {
            // Surfaced by the standard resilience handler after retries exhaust on transient failures (5xx, network errors).
            logger.LogError(ex, "Failed to fetch gravatar for user {UserId} after retries. Treating as unavailable", userId);
            return null;
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient.Timeout and the resilience pipeline raise OperationCanceledException subtypes
            // (notably TaskCanceledException) on internal timeouts. The when-clause re-throws genuine
            // caller-driven cancellation so MediatR sees it as a real cancellation, not an avatar miss.
            logger.LogError(ex, "Timeout fetching gravatar for user {UserId}. Treating as unavailable", userId);
            return null;
        }
    }
}
