namespace SharedKernel.DelegationCredentials;

/// <summary>
///     A decrypted delegation credential resolved for a specific org member.
///     <para>
///         <strong>Security:</strong> The <see cref="AccessTokenOrServiceAccountKey" /> field contains
///         sensitive key material. Never log, persist, or transmit this value outside the caller's
///         in-process scope.
///     </para>
/// </summary>
/// <param name="Platform">The workspace platform this credential targets.</param>
/// <param name="AccessTokenOrServiceAccountKey">
///     The decrypted key material — a service-account JSON blob (Google) or an OAuth refresh token
///     (Microsoft). Treat as a secret.
/// </param>
/// <param name="MemberEmail">The org member email for which the credential was resolved.</param>
/// <param name="Domain">The email domain the credential is scoped to (e.g. <c>acme.com</c>).</param>
[PublicAPI]
public sealed record ResolvedCredential(
    WorkspacePlatform Platform,
    string AccessTokenOrServiceAccountKey,
    string MemberEmail,
    string Domain
);
