import { Trans } from "@lingui/react/macro";
import { Checkbox } from "@repo/ui/components/Checkbox";

import { PermissionAction, type Schemas } from "@/shared/lib/api/client";

type PermissionGroup = Schemas["PermissionGroupResponse"];
type Permission = Schemas["PermissionResponse"];

// CRUD actions that are implicitly granted when "Manage" is selected for a resource. We still
// render them (checked + disabled) so the matrix communicates the implied grant, but only the
// "Manage" key is persisted to PermissionIds.
const IMPLIED_BY_MANAGE: ReadonlySet<PermissionAction> = new Set([
  PermissionAction.Create,
  PermissionAction.Read,
  PermissionAction.Update,
  PermissionAction.Delete
]);

interface PermissionMatrixProps {
  groups: PermissionGroup[];
  selectedKeys: ReadonlySet<string>;
  onChange: (key: string, checked: boolean) => void;
  readOnly?: boolean;
}

export function PermissionMatrix({
  groups,
  selectedKeys,
  onChange,
  readOnly = false
}: Readonly<PermissionMatrixProps>) {
  // Collect every action that appears in any group so columns line up across rows.
  const orderedActions = collectOrderedActions(groups);

  return (
    <div className="overflow-x-auto rounded-md border">
      <table className="w-full border-collapse text-sm">
        <thead className="bg-muted/50">
          <tr>
            <th className="px-3 py-2 text-left font-medium">
              <Trans>Resource</Trans>
            </th>
            {orderedActions.map((action) => (
              <th key={action} className="px-3 py-2 text-center font-medium whitespace-nowrap">
                {action}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {groups.map((group) => {
            const manageKey = findPermissionKey(group.permissions, PermissionAction.Manage);
            const manageSelected = manageKey !== null && selectedKeys.has(manageKey);
            return (
              <tr key={group.resource} className="border-t">
                <td className="px-3 py-2 font-medium">{group.resource}</td>
                {orderedActions.map((action) => {
                  const permission = group.permissions.find((p) => p.action === action);
                  if (!permission) {
                    return (
                      <td key={action} className="px-3 py-2 text-center text-muted-foreground">
                        —
                      </td>
                    );
                  }

                  const isImpliedByManage = manageSelected && IMPLIED_BY_MANAGE.has(action);
                  const checked = isImpliedByManage || selectedKeys.has(permission.key);
                  const disabled = readOnly || isImpliedByManage;

                  return (
                    <td key={action} className="px-3 py-2 text-center">
                      <div className="flex items-center justify-center">
                        <Checkbox
                          checked={checked}
                          disabled={disabled}
                          onCheckedChange={(value) => onChange(permission.key, value === true)}
                          aria-label={`${group.resource} ${action}`}
                        />
                      </div>
                    </td>
                  );
                })}
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}

function findPermissionKey(permissions: Permission[], action: PermissionAction): string | null {
  return permissions.find((p) => p.action === action)?.key ?? null;
}

function collectOrderedActions(groups: PermissionGroup[]): PermissionAction[] {
  // Preserve the backend's natural enum ordering by sourcing from PermissionAction values that
  // actually appear in the payload.
  const seen = new Set<PermissionAction>();
  for (const group of groups) {
    for (const permission of group.permissions) {
      seen.add(permission.action);
    }
  }
  return Object.values(PermissionAction).filter((action) => seen.has(action));
}
