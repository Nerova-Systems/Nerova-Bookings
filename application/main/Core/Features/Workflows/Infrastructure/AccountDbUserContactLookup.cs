using Main.Features.Workflows.Senders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using SharedKernel.Domain;

namespace Main.Features.Workflows.Infrastructure;

/// <summary>
///     Real <see cref="IUserContactLookup" /> backed by a direct read against the account-database
///     <c>users</c> table. Cross-SCS by necessity: booking notifications are owned by main, but the
///     host's email/locale live in the account SCS. Read-only, single-statement; no cross-SCS writes.
///     <para>
///         Connection string is sourced from <c>ConnectionStrings:account-database</c> (wired via the
///         AppHost <c>.WithReference(accountDatabase)</c> on main-workers). Returns null when the
///         connection string is missing (allows main-api hosts that don't reference the account DB
///         to still resolve <see cref="IUserContactLookup" /> without throwing).
///     </para>
/// </summary>
public sealed class AccountDbUserContactLookup(
    IConfiguration configuration,
    ILogger<AccountDbUserContactLookup> logger
) : IUserContactLookup
{
    private const string ConnectionName = "account-database";

    public async Task<UserContactInfo?> GetAsync(UserId userId, CancellationToken cancellationToken)
    {
        var connectionString = configuration.GetConnectionString(ConnectionName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            logger.LogWarning(
                "Cross-SCS user contact lookup skipped: connection string '{ConnectionName}' is not configured.",
                ConnectionName
            );
            return null;
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT email, locale, first_name, last_name
            FROM users
            WHERE id = @id AND deleted_at IS NULL
            LIMIT 1
            """;
        command.Parameters.AddWithValue("id", userId.Value);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;

        var email = reader.GetString(0);
        var locale = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
        var firstName = reader.IsDBNull(2) ? null : reader.GetString(2);
        var lastName = reader.IsDBNull(3) ? null : reader.GetString(3);

        var displayName = $"{firstName} {lastName}".Trim();
        if (string.IsNullOrWhiteSpace(displayName)) displayName = email;
        if (string.IsNullOrWhiteSpace(locale)) locale = "en-US";

        return new UserContactInfo(email, locale, displayName);
    }
}
