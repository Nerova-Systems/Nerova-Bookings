using JetBrains.Annotations;

namespace SharedKernel.DelegationCredentials;

/// <summary>
///     Identifies the third-party workspace platform that a delegation credential targets.
/// </summary>
[PublicAPI]
public enum WorkspacePlatform
{
    /// <summary>Google Workspace (Calendar, Meet).</summary>
    Google,

    /// <summary>Microsoft 365 (Exchange Online, Teams).</summary>
    Microsoft
}
