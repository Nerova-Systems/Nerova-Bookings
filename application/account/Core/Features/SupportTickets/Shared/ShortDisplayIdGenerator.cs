using System.Security.Cryptography;
using Account.Features.SupportTickets.Domain;

namespace Account.Features.SupportTickets.Shared;

// Generates a six-character uppercase alphanumeric (A-Z, 0-9) short display ID for a ticket.
// Six characters give ~2.1 billion permutations — collisions inside a single tenant are vanishingly
// rare, but the generator still probes the repository up to MaxAttempts times to keep the contract
// "unique within the tenant" airtight.
public sealed class ShortDisplayIdGenerator(ISupportTicketRepository ticketRepository)
{
    private const int MaxAttempts = 10;
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    public async Task<string> GenerateUniqueForTenantAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var candidate = GenerateRandom();
            if (await ticketRepository.IsShortDisplayIdFreeForTenantAsync(candidate, cancellationToken))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Failed to generate a unique support ticket short display ID after several attempts.");
    }

    private static string GenerateRandom()
    {
        Span<char> buffer = stackalloc char[SupportTicket.ShortDisplayIdLength];
        for (var i = 0; i < buffer.Length; i++)
        {
            buffer[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return new string(buffer);
    }
}
