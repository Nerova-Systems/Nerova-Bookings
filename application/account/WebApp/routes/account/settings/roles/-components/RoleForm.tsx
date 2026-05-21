import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { Form } from "@repo/ui/components/Form";
import { Input } from "@repo/ui/components/Input";
import { Label } from "@repo/ui/components/Label";
import { Textarea } from "@repo/ui/components/Textarea";
import { useState } from "react";

import { api, type Schemas } from "@/shared/lib/api/client";

import { PermissionMatrix } from "./PermissionMatrix";

type RoleResponse = Schemas["RoleResponse"];
type ValidationProblem = Schemas["HttpValidationProblemDetails"];

interface RoleFormProps {
  initialRole?: RoleResponse;
  submitLabel: string;
  isPending: boolean;
  errors?: ValidationProblem["errors"];
  onSubmit: (values: { name: string; description: string | null; permissions: string[] }) => void;
  onCancel: () => void;
}

export function RoleForm({ initialRole, submitLabel, isPending, errors, onSubmit, onCancel }: Readonly<RoleFormProps>) {
  const [name, setName] = useState(initialRole?.name ?? "");
  const [description, setDescription] = useState(initialRole?.description ?? "");
  const [selectedKeys, setSelectedKeys] = useState<Set<string>>(
    () => new Set(initialRole?.permissions.map((p) => p.key) ?? [])
  );

  const { data: permissionGroups, isLoading } = api.useQuery("get", "/api/account/permissions");

  const readOnly = initialRole?.isSystem === true;

  const handleTogglePermission = (key: string, checked: boolean) => {
    setSelectedKeys((current) => {
      const next = new Set(current);
      if (checked) {
        next.add(key);
      } else {
        next.delete(key);
      }
      return next;
    });
  };

  const handleSubmit = (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    onSubmit({
      name: name.trim(),
      description: description.trim() === "" ? null : description.trim(),
      permissions: Array.from(selectedKeys)
    });
  };

  return (
    <Form onSubmit={handleSubmit} validationErrors={errors} className="flex flex-col gap-6">
      <div className="flex flex-col gap-2">
        <Label htmlFor="role-name">
          <Trans>Name</Trans>
        </Label>
        <Input
          id="role-name"
          name="name"
          value={name}
          onChange={(e) => setName(e.target.value)}
          disabled={readOnly || isPending}
          required={true}
          aria-label={t`Role name`}
        />
      </div>

      <div className="flex flex-col gap-2">
        <Label htmlFor="role-description">
          <Trans>Description</Trans>
        </Label>
        <Textarea
          id="role-description"
          name="description"
          value={description}
          onChange={(e) => setDescription(e.target.value)}
          disabled={readOnly || isPending}
          rows={3}
          aria-label={t`Role description`}
        />
      </div>

      <div className="flex flex-col gap-2">
        <Label>
          <Trans>Permissions</Trans>
        </Label>
        <p className="text-sm text-muted-foreground">
          <Trans>
            Selecting <b>Manage</b> for a resource implicitly grants Create, Read, Update, and Delete.
          </Trans>
        </p>
        {isLoading || !permissionGroups ? (
          <p className="text-sm text-muted-foreground">
            <Trans>Loading permissions...</Trans>
          </p>
        ) : (
          <PermissionMatrix
            groups={permissionGroups}
            selectedKeys={selectedKeys}
            onChange={handleTogglePermission}
            readOnly={readOnly || isPending}
          />
        )}
      </div>

      <div className="flex justify-end gap-2">
        <Button type="button" variant="secondary" onClick={onCancel} disabled={isPending}>
          <Trans>Cancel</Trans>
        </Button>
        {!readOnly && (
          <Button type="submit" isPending={isPending}>
            {submitLabel}
          </Button>
        )}
      </div>
    </Form>
  );
}
