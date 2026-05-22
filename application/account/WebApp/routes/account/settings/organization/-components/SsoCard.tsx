import type { ReactNode } from "react";

import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Badge } from "@repo/ui/components/Badge";
import { Button } from "@repo/ui/components/Button";
import { Form } from "@repo/ui/components/Form";
import { Input } from "@repo/ui/components/Input";
import { Label } from "@repo/ui/components/Label";
import { Section } from "@repo/ui/components/Section";
import { Textarea } from "@repo/ui/components/Textarea";

export interface SsoCardProps {
  title: ReactNode;
  description: string;
  isEnabled: boolean;
  canManage: boolean;
  validationErrors: Record<string, string | string[]> | undefined;
  isSaving: boolean;
  isToggling: boolean;
  isTesting: boolean;
  onSave: (event: React.FormEvent<HTMLFormElement>) => void;
  onToggle: () => void;
  onTest: () => void;
  providerFieldId: string;
  providerFieldLabel: ReactNode;
  providerFieldName: string;
  providerFieldValue: string;
  onProviderFieldChange: (value: string) => void;
  providerFieldPlaceholder?: string;
  clientId: string;
  onClientIdChange: (value: string) => void;
  clientSecret: string;
  onClientSecretChange: (value: string) => void;
  allowedDomains: string;
  onAllowedDomainsChange: (value: string) => void;
  idPrefix: string;
}

export function SsoCard(props: Readonly<SsoCardProps>) {
  const disabled = !props.canManage || props.isSaving;
  return (
    <Section title={renderTitle(props.title, props.isEnabled)} description={props.description}>
      <Form onSubmit={props.onSave} validationErrors={props.validationErrors} className="flex flex-col gap-4">
        <Field id={props.providerFieldId} label={props.providerFieldLabel}>
          <Input
            id={props.providerFieldId}
            name={props.providerFieldName}
            value={props.providerFieldValue}
            onChange={(e) => props.onProviderFieldChange(e.target.value)}
            disabled={disabled}
            placeholder={props.providerFieldPlaceholder}
          />
        </Field>
        <Field id={`${props.idPrefix}-client-id`} label={<Trans>Client ID</Trans>}>
          <Input
            id={`${props.idPrefix}-client-id`}
            name="clientId"
            value={props.clientId}
            onChange={(e) => props.onClientIdChange(e.target.value)}
            disabled={disabled}
          />
        </Field>
        <Field id={`${props.idPrefix}-client-secret`} label={<Trans>Client secret</Trans>}>
          <Input
            id={`${props.idPrefix}-client-secret`}
            name="clientSecret"
            type="password"
            value={props.clientSecret}
            onChange={(e) => props.onClientSecretChange(e.target.value)}
            disabled={disabled}
            placeholder={t`Leave blank to keep current secret`}
          />
        </Field>
        <Field id={`${props.idPrefix}-allowed-domains`} label={<Trans>Allowed email domains</Trans>}>
          <Textarea
            id={`${props.idPrefix}-allowed-domains`}
            name="allowedDomains"
            value={props.allowedDomains}
            onChange={(e) => props.onAllowedDomainsChange(e.target.value)}
            disabled={disabled}
            rows={3}
            placeholder={t`acme.com, partner.com`}
          />
          <p className="text-sm text-muted-foreground">
            <Trans>One domain per line, or comma-separated. Only users with these email domains can sign in.</Trans>
          </p>
        </Field>
        {props.canManage && <SsoActions {...props} />}
      </Form>
    </Section>
  );
}

function renderTitle(title: ReactNode, isEnabled: boolean) {
  return (
    <div className="flex items-center gap-2">
      {title}
      {isEnabled ? (
        <Badge variant="outline">
          <Trans>Enabled</Trans>
        </Badge>
      ) : (
        <Badge variant="secondary">
          <Trans>Disabled</Trans>
        </Badge>
      )}
    </div>
  );
}

function SsoActions(props: Readonly<SsoCardProps>) {
  return (
    <div className="flex justify-end gap-2">
      <Button
        type="button"
        variant="secondary"
        onClick={props.onTest}
        isPending={props.isTesting}
        disabled={!props.isEnabled}
      >
        <Trans>Send test</Trans>
      </Button>
      <Button
        type="button"
        variant={props.isEnabled ? "destructive" : "secondary"}
        onClick={props.onToggle}
        isPending={props.isToggling}
      >
        {props.isEnabled ? <Trans>Disable</Trans> : <Trans>Enable</Trans>}
      </Button>
      <Button type="submit" isPending={props.isSaving}>
        <Trans>Save</Trans>
      </Button>
    </div>
  );
}

function Field({ id, label, children }: Readonly<{ id: string; label: ReactNode; children: ReactNode }>) {
  return (
    <div className="flex flex-col gap-2">
      <Label htmlFor={id}>{label}</Label>
      {children}
    </div>
  );
}
