import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { ComboboxField } from "@repo/ui/components/ComboboxField";
import { DateRangePicker } from "@repo/ui/components/DateRangePicker";
import { useNavigate } from "@tanstack/react-router";
import { format, parseISO } from "date-fns";
import { FilterXIcon } from "lucide-react";
import { useMemo } from "react";

import { api } from "@/shared/lib/api/client";

import type { AuditLogSearch } from "../index";

// Static enum mirrors of `Account.Features.AuditLog.Domain.AuditAction` / `AuditResource`.
// The backend stores these as PascalCase strings via EF Core's enum-to-string converter,
// so the filter values must match the C# member names exactly.
const AUDIT_ACTIONS = [
  "Created",
  "Updated",
  "Deleted",
  "Invited",
  "Accepted",
  "Declined",
  "Assigned",
  "Revoked",
  "Enabled",
  "Disabled",
  "Started",
  "Ended",
  "Exported",
  "Imported",
  "Approved",
  "Rejected",
  "Configured",
  "Tested",
  "KeyRotated"
] as const;

const AUDIT_RESOURCES = [
  "Membership",
  "Role",
  "Tenant",
  "Booking",
  "EventType",
  "User",
  "ApiKey",
  "Workflow",
  "Insights",
  "Attribute",
  "Schedule",
  "Sso",
  "Smtp",
  "Billing",
  "OrgProfile",
  "DelegationCredential"
] as const;

interface AuditLogFiltersProps {
  search: AuditLogSearch;
}

export function AuditLogFilters({ search }: Readonly<AuditLogFiltersProps>) {
  const navigate = useNavigate({ from: "/account/settings/audit-log/" });

  // Pull the first page of users for the Actor combobox. Bulk paging is out of scope for a
  // filter dropdown; cal.com's audit log uses the same "first 100 members" shortcut.
  const { data: usersData } = api.useQuery("get", "/api/account/users", {
    params: { query: { PageSize: 100 } }
  });

  const actorItems = useMemo(
    () =>
      (usersData?.users ?? []).map((user) => ({
        id: user.id,
        label: [user.firstName, user.lastName].filter(Boolean).join(" ") || user.email
      })),
    [usersData?.users]
  );

  const actionItems = useMemo(() => AUDIT_ACTIONS.map((action) => ({ id: action, label: action })), []);

  const resourceItems = useMemo(() => AUDIT_RESOURCES.map((resource) => ({ id: resource, label: resource })), []);

  const dateRange =
    search.fromDate && search.toDate ? { start: parseISO(search.fromDate), end: parseISO(search.toDate) } : null;

  const hasFilters = Boolean(
    search.actorUserId || search.action || search.resource || search.fromDate || search.toDate
  );

  const updateFilter = (next: Partial<AuditLogSearch>) => {
    navigate({ search: (prev) => ({ ...prev, ...next, pageOffset: undefined }) });
  };

  const clearAll = () => {
    navigate({ search: () => ({}) });
  };

  return (
    <div className="flex flex-wrap items-end gap-2">
      <div className="min-w-48 flex-1">
        <ComboboxField
          label={t`Actor`}
          placeholder={t`Any actor`}
          items={actorItems}
          value={search.actorUserId ?? null}
          onValueChange={(value) => updateFilter({ actorUserId: value ?? undefined })}
        />
      </div>
      <div className="min-w-40 flex-1">
        <ComboboxField
          label={t`Action`}
          placeholder={t`Any action`}
          items={actionItems}
          value={search.action ?? null}
          onValueChange={(value) => updateFilter({ action: value ?? undefined })}
        />
      </div>
      <div className="min-w-40 flex-1">
        <ComboboxField
          label={t`Resource type`}
          placeholder={t`Any resource`}
          items={resourceItems}
          value={search.resource ?? null}
          onValueChange={(value) => updateFilter({ resource: value ?? undefined })}
        />
      </div>
      <DateRangePicker
        value={dateRange}
        onChange={(range) =>
          updateFilter({
            fromDate: range ? format(range.start, "yyyy-MM-dd") : undefined,
            toDate: range ? format(range.end, "yyyy-MM-dd") : undefined
          })
        }
        label={t`Date range`}
        placeholder={t`Select dates`}
      />
      {hasFilters && (
        <Button variant="secondary" onClick={clearAll} aria-label={t`Clear filters`}>
          <FilterXIcon className="size-4" />
          <Trans>Clear filters</Trans>
        </Button>
      )}
    </div>
  );
}
