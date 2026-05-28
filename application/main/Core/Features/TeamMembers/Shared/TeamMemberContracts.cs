using JetBrains.Annotations;

namespace Main.Features.TeamMembers.Shared;

[PublicAPI]
public sealed record TeamMemberResponse(string UserId, string DisplayName, string Email);

[PublicAPI]
public sealed record SearchTeamMembersResponse(TeamMemberResponse[] Members);
