import type { ReactNode } from "react";

import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Input } from "@repo/ui/components/Input";
import { Label } from "@repo/ui/components/Label";
import { Switch } from "@repo/ui/components/Switch";

export interface SmtpFormState {
  host: string;
  port: string;
  username: string;
  password: string;
  fromEmail: string;
  fromName: string;
  replyToEmail: string;
  useSsl: boolean;
}

export interface SmtpFormSetters {
  setHost: (value: string) => void;
  setPort: (value: string) => void;
  setUsername: (value: string) => void;
  setPassword: (value: string) => void;
  setFromEmail: (value: string) => void;
  setFromName: (value: string) => void;
  setReplyToEmail: (value: string) => void;
  setUseSsl: (value: boolean) => void;
}

export function SmtpFormFields({
  state,
  setters,
  disabled
}: Readonly<{ state: SmtpFormState; setters: SmtpFormSetters; disabled: boolean }>) {
  return (
    <div className="flex flex-col gap-4">
      <Row>
        <Field id="smtp-host" label={<Trans>Host</Trans>}>
          <Input
            id="smtp-host"
            name="host"
            value={state.host}
            onChange={(e) => setters.setHost(e.target.value)}
            disabled={disabled}
            required={true}
          />
        </Field>
        <Field id="smtp-port" label={<Trans>Port</Trans>}>
          <Input
            id="smtp-port"
            name="port"
            type="number"
            inputMode="numeric"
            value={state.port}
            onChange={(e) => setters.setPort(e.target.value)}
            disabled={disabled}
            required={true}
          />
        </Field>
      </Row>
      <Row>
        <Field id="smtp-username" label={<Trans>Username</Trans>}>
          <Input
            id="smtp-username"
            name="username"
            value={state.username}
            onChange={(e) => setters.setUsername(e.target.value)}
            disabled={disabled}
            required={true}
          />
        </Field>
        <Field id="smtp-password" label={<Trans>Password</Trans>}>
          <Input
            id="smtp-password"
            name="password"
            type="password"
            value={state.password}
            onChange={(e) => setters.setPassword(e.target.value)}
            disabled={disabled}
            placeholder={t`Enter SMTP password`}
          />
        </Field>
      </Row>
      <Row>
        <Field id="smtp-from-email" label={<Trans>From email</Trans>}>
          <Input
            id="smtp-from-email"
            name="fromEmail"
            type="email"
            value={state.fromEmail}
            onChange={(e) => setters.setFromEmail(e.target.value)}
            disabled={disabled}
            required={true}
          />
        </Field>
        <Field id="smtp-from-name" label={<Trans>From name</Trans>}>
          <Input
            id="smtp-from-name"
            name="fromName"
            value={state.fromName}
            onChange={(e) => setters.setFromName(e.target.value)}
            disabled={disabled}
          />
        </Field>
      </Row>
      <Field id="smtp-reply-to" label={<Trans>Reply-to email</Trans>}>
        <Input
          id="smtp-reply-to"
          name="replyToEmail"
          type="email"
          value={state.replyToEmail}
          onChange={(e) => setters.setReplyToEmail(e.target.value)}
          disabled={disabled}
        />
      </Field>
      <div className="flex items-center justify-between gap-4 rounded-md border border-border p-4">
        <div>
          <p className="font-medium">
            <Trans>Use SSL/TLS</Trans>
          </p>
          <p className="text-sm text-muted-foreground">
            <Trans>Encrypt SMTP connection with SSL/TLS.</Trans>
          </p>
        </div>
        <Switch
          checked={state.useSsl}
          onCheckedChange={setters.setUseSsl}
          disabled={disabled}
          aria-label={t`Use SSL`}
        />
      </div>
    </div>
  );
}

function Row({ children }: Readonly<{ children: ReactNode }>) {
  return <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">{children}</div>;
}

function Field({ id, label, children }: Readonly<{ id: string; label: ReactNode; children: ReactNode }>) {
  return (
    <div className="flex flex-col gap-2">
      <Label htmlFor={id}>{label}</Label>
      {children}
    </div>
  );
}
