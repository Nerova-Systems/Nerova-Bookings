import { enhancedFetch } from "@repo/infrastructure/http/httpClient";

/**
 * The kind of scope a {@link TenantInfo} represents. Mirrors the backend `TenantKind` enum:
 * `Solo` is a flat tenant (the user's personal account); `Organization` is a top-level org;
 * `Team` is a team nested under an organization (`parentOrgId` points to the parent org).
 */
export type ScopeKind = "Solo" | "Organization" | "Team";

export interface TenantInfo {
  tenantId: string;
  tenantName: string | null;
  logoUrl: string | null;
  isNew: boolean;
  /**
   * Optional — populated when the tenant was retrieved via the switchable-scopes endpoint.
   * Older callers (multi-account-only flows) leave it unset and the UI treats those as Solo.
   */
  kind?: ScopeKind;
  /** Set only when `kind === "Team"`. References the parent Organization's tenant id. */
  parentOrgId?: string | null;
}

export interface TenantsResponse {
  tenants: TenantInfo[];
}

interface SwitchableScopeDto {
  tenantId: string;
  tenantName: string | null;
  logoUrl: string | null;
  kind: ScopeKind;
  parentOrgId: string | null;
  isCurrent: boolean;
  isPending: boolean;
}

interface SwitchableScopesDto {
  scopes: SwitchableScopeDto[];
}

export function sortTenants(tenants: TenantInfo[]): TenantInfo[] {
  return [...tenants].sort((a, b) => {
    if (!a.tenantName && b.tenantName) {
      return 1;
    }
    if (a.tenantName && !b.tenantName) {
      return -1;
    }
    const nameA = a.tenantName || "";
    const nameB = b.tenantName || "";
    return nameA.localeCompare(nameB);
  });
}

export async function fetchTenants(): Promise<TenantsResponse> {
  const response = await enhancedFetch("/api/account/tenants");
  return response.json();
}

/**
 * Fetches every Solo tenant, Organization, and Team the current user can switch to.
 * Used by the federated tenant switcher (user menu + mobile drawer) to render the
 * cal.com-style 3-tier scope list. Falls back gracefully to the legacy email-matched
 * tenants endpoint if the new endpoint is unavailable (e.g., during partial deploys).
 */
export async function fetchSwitchableScopes(): Promise<TenantsResponse> {
  try {
    const response = await enhancedFetch("/api/account/tenants/switchable-scopes");
    const data: SwitchableScopesDto = await response.json();
    return {
      tenants: data.scopes.map<TenantInfo>((s) => ({
        tenantId: s.tenantId,
        tenantName: s.tenantName,
        logoUrl: s.logoUrl,
        isNew: s.isPending,
        kind: s.kind,
        parentOrgId: s.parentOrgId
      }))
    };
  } catch {
    // Fallback for older backends: legacy endpoint returns only Solo tenants.
    return fetchTenants();
  }
}

export async function switchTenantApi(tenantId: string): Promise<void> {
  await enhancedFetch("/api/account/authentication/switch-tenant", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ tenantId })
  });
}

export async function logoutApi(): Promise<void> {
  await enhancedFetch("/api/account/authentication/logout", {
    method: "POST"
  });
}

export interface OrgGroup {
  org: TenantInfo;
  teams: TenantInfo[];
}

/**
 * Groups a flat scope list into Solo + Organizations (with nested Teams) + orphan Teams.
 * Teams whose parent org is not in the list (e.g., access to the team but not the parent
 * org) land in `orphanTeams` so the user can still switch to them. Legacy `TenantInfo`
 * records without a `kind` are treated as Solo for backwards compatibility.
 */
export function groupScopes(scopes: TenantInfo[]): {
  solo: TenantInfo[];
  orgs: OrgGroup[];
  orphanTeams: TenantInfo[];
} {
  const solo: TenantInfo[] = [];
  const orgsById = new Map<string, OrgGroup>();
  const teams: TenantInfo[] = [];

  for (const scope of scopes) {
    const kind = scope.kind ?? "Solo";
    if (kind === "Solo") {
      solo.push(scope);
    } else if (kind === "Organization") {
      orgsById.set(scope.tenantId, orgsById.get(scope.tenantId) ?? { org: scope, teams: [] });
    } else {
      teams.push(scope);
    }
  }

  const orphanTeams: TenantInfo[] = [];
  for (const team of teams) {
    const parent = team.parentOrgId ? orgsById.get(team.parentOrgId) : undefined;
    if (parent) {
      parent.teams.push(team);
    } else {
      orphanTeams.push(team);
    }
  }

  return { solo, orgs: [...orgsById.values()], orphanTeams };
}
