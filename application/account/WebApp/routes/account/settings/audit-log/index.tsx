import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { useUserInfo } from "@repo/infrastructure/auth/hooks";
import { isFeatureFlagEnabled } from "@repo/infrastructure/featureFlags/useFeatureFlag";
import { AppLayout } from "@repo/ui/components/AppLayout";
import { keepPreviousData } from "@tanstack/react-query";
import { createFileRoute, redirect, useNavigate } from "@tanstack/react-router";
import { useCallback } from "react";
import { z } from "zod";

import { api, type Schemas } from "@/shared/lib/api/client";

import { AuditLogFilters } from "./-components/AuditLogFilters";
import { AuditLogTable } from "./-components/AuditLogTable";

type AuditLogEntry = Schemas["AuditLogEntryResponse"];

const auditLogSearchSchema = z.object({
  actorUserId: z.string().optional(),
  action: z.string().optional(),
  resource: z.string().optional(),
  fromDate: z.string().optional(),
  toDate: z.string().optional(),
  pageOffset: z.number().optional()
});

export type AuditLogSearch = z.infer<typeof auditLogSearchSchema>;

export const Route = createFileRoute("/account/settings/audit-log/")({
  beforeLoad: () => {
    // The cap-audit-log flag gates the entire viewer. The sidebar hides the entry when
    // disabled, but direct navigation must redirect rather than render an unsupported page.
    if (!isFeatureFlagEnabled("cap-audit-log")) {
      throw redirect({ to: "/account/settings" });
    }
  },
  staticData: { trackingTitle: "Audit log" },
  component: AuditLogPage,
  validateSearch: auditLogSearchSchema
});

function AuditLogPage() {
  const userInfo = useUserInfo();
  const navigate = useNavigate({ from: Route.fullPath });
  const search = Route.useSearch();

  // TODO(pbac): replace this role-based gate with a usePermission(AuditLog.Read) hook once
  // the frontend self-gating story lands. Until then we fall back to the legacy Owner/Admin
  // gate that mirrors the backend's RequirePermission attribute.
  const canRead = userInfo?.role === "Owner" || userInfo?.role === "Admin";

  const { data, isLoading } = api.useQuery(
    "get",
    "/api/account/audit-log",
    {
      params: {
        query: {
          ActorUserId: search.actorUserId,
          Action: search.action,
          Resource: search.resource,
          FromDate: search.fromDate,
          ToDate: search.toDate,
          PageOffset: search.pageOffset
        }
      }
    },
    { enabled: canRead, placeholderData: keepPreviousData }
  );

  const handlePageChange = useCallback(
    (page: number) => {
      navigate({ search: (prev) => ({ ...prev, pageOffset: page === 1 ? undefined : page - 1 }) });
    },
    [navigate]
  );

  if (!canRead) {
    return (
      <AppLayout variant="center" maxWidth="80rem" title={<Trans>Audit log</Trans>}>
        <p className="text-sm text-muted-foreground">
          <Trans>You don't have permission to view the audit log.</Trans>
        </p>
      </AppLayout>
    );
  }

  const entries: AuditLogEntry[] = data?.entries ?? [];
  const totalPages = data?.totalPages ?? 1;
  const currentPageOffset = data?.currentPageOffset ?? 0;
  const hasFilters = Boolean(
    search.actorUserId || search.action || search.resource || search.fromDate || search.toDate
  );

  return (
    <AppLayout
      variant="center"
      maxWidth="80rem"
      title={t`Audit log`}
      subtitle={t`Review every significant action performed in your organization.`}
    >
      <AuditLogFilters search={search} />
      <AuditLogTable
        entries={entries}
        isLoading={isLoading}
        hasFilters={hasFilters}
        totalPages={totalPages}
        currentPageOffset={currentPageOffset}
        onPageChange={handlePageChange}
      />
    </AppLayout>
  );
}
