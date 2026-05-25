using Main.Features.TeamMembers.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using SharedKernel.Domain;

namespace Main.Features.TeamMembers.Infrastructure;

/// <summary>
///     Cross-SCS implementation of <see cref="ITeamMemberDirectory" /> against the account-database.
///     Read-only: a single SELECT per call, no transactions or writes. Returns an empty array when
///     the connection string is missing (allows the main-api host to resolve the service even when
///     it has not declared <c>WithReference(accountDatabase)</c>). Mirrors
///     <c>AccountDbUserContactLookup</c>.
///     <para>
///         The current schema does not expose team membership in a queryable form to main, so this
///         implementation filters by tenant id only. Once cross-SCS membership is exposed, the SQL
///         should be updated to filter by team membership as well.
///     </para>
/// </summary>
public sealed class AccountDbTeamMemberDirectory(
    IConfiguration configuration,
    ILogger<AccountDbTeamMemberDirectory> logger
) : ITeamMemberDirectory
{
    private const string ConnectionName = "account-database";

    public async Task<TeamMember[]> SearchAsync(TenantId tenantId, string? query, int limit, CancellationToken cancellationToken)
    {
        var connectionString = configuration.GetConnectionString(ConnectionName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            logger.LogWarning(
                "Cross-SCS team member directory skipped: connection string '{ConnectionName}' is not configured.",
                ConnectionName
            );
            return [];
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        var hasQuery = !string.IsNullOrWhiteSpace(query);
        command.CommandText = $$"""
            SELECT id, email, first_name, last_name
            FROM users
            WHERE tenant_id = @tenant_id AND deleted_at IS NULL
            {{(hasQuery ? "AND (email ILIKE @q OR first_name ILIKE @q OR last_name ILIKE @q)" : string.Empty)}}
            ORDER BY first_name NULLS LAST, last_name NULLS LAST, email
            LIMIT @limit
            """;
        command.Parameters.AddWithValue("tenant_id", tenantId.Value);
        command.Parameters.AddWithValue("limit", Math.Clamp(limit, 1, 100));
        if (hasQuery) command.Parameters.AddWithValue("q", $"%{query}%");

        var results = new List<TeamMember>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var id = reader.GetString(0);
            var email = reader.GetString(1);
            var firstName = reader.IsDBNull(2) ? null : reader.GetString(2);
            var lastName = reader.IsDBNull(3) ? null : reader.GetString(3);
            var displayName = $"{firstName} {lastName}".Trim();
            if (string.IsNullOrWhiteSpace(displayName)) displayName = email;
            results.Add(new TeamMember(new UserId(id), displayName, email));
        }

        return results.ToArray();
    }
}
