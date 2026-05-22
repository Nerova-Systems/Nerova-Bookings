import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Badge } from "@repo/ui/components/Badge";
import { Button } from "@repo/ui/components/Button";
import { Form } from "@repo/ui/components/Form";
import { Input } from "@repo/ui/components/Input";
import { Label } from "@repo/ui/components/Label";
import { Section } from "@repo/ui/components/Section";
import { useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { toast } from "sonner";

import { api, type Schemas } from "@/shared/lib/api/client";

import { SmtpFormFields, type SmtpFormState } from "./SmtpFormFields";

type SmtpConfig = Schemas["OrgSmtpConfigResponse"];

interface OrgSmtpTabProps {
  canManage: boolean;
}

export function OrgSmtpTab({ canManage }: Readonly<OrgSmtpTabProps>) {
  const { data: config, isLoading } = api.useQuery("get", "/api/account/org/smtp");
  if (isLoading || !config) {
    return null;
  }
  return <OrgSmtpCard config={config} canManage={canManage} />;
}

function OrgSmtpCard({ config, canManage }: Readonly<{ config: SmtpConfig; canManage: boolean }>) {
  const queryClient = useQueryClient();
  const [host, setHost] = useState(config.host);
  const [port, setPort] = useState(config.port.toString());
  const [username, setUsername] = useState(config.username);
  const [password, setPassword] = useState("");
  const [fromEmail, setFromEmail] = useState(config.fromEmail);
  const [fromName, setFromName] = useState(config.fromName ?? "");
  const [replyToEmail, setReplyToEmail] = useState(config.replyToEmail ?? "");
  const [useSsl, setUseSsl] = useState(config.useSsl);
  const [recipientEmail, setRecipientEmail] = useState("");

  const configureMutation = api.useMutation("put", "/api/account/org/smtp", { meta: { skipQueryInvalidation: true } });
  const deleteMutation = api.useMutation("delete", "/api/account/org/smtp");
  const testMutation = api.useMutation("post", "/api/account/org/smtp/test");

  const state: SmtpFormState = { host, port, username, password, fromEmail, fromName, replyToEmail, useSsl };
  const setters = {
    setHost,
    setPort,
    setUsername,
    setPassword,
    setFromEmail,
    setFromName,
    setReplyToEmail,
    setUseSsl
  };

  const invalidate = () =>
    queryClient.invalidateQueries({
      predicate: (query) =>
        Array.isArray(query.queryKey) &&
        typeof query.queryKey[1] === "string" &&
        query.queryKey[1].startsWith("/api/account/org/smtp")
    });

  const buildPayload = () => ({
    host: host.trim(),
    port: Number.parseInt(port, 10),
    username: username.trim(),
    password,
    fromEmail: fromEmail.trim(),
    fromName: fromName.trim() === "" ? null : fromName.trim(),
    replyToEmail: replyToEmail.trim() === "" ? null : replyToEmail.trim(),
    useSsl
  });

  const handleSave = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (password.trim() === "") {
      toast.error(t`Please enter the SMTP password to save the configuration.`);
      return;
    }
    await configureMutation.mutateAsync(
      { body: buildPayload() },
      {
        onSuccess: async () => {
          await invalidate();
          setPassword("");
          toast.success(t`SMTP configuration saved`);
        }
      }
    );
  };

  const handleTest = async () => {
    if (password.trim() === "" || recipientEmail.trim() === "") {
      toast.error(t`Please enter both the SMTP password and a recipient email address.`);
      return;
    }
    await testMutation.mutateAsync(
      { body: { ...buildPayload(), recipientEmail: recipientEmail.trim() } },
      {
        onSuccess: (result) => {
          if (result.success) {
            toast.success(t`SMTP test email sent successfully`);
          } else {
            toast.error(result.errorMessage ?? t`SMTP test failed`);
          }
        }
      }
    );
  };

  const handleDelete = async () => {
    await deleteMutation.mutateAsync(
      {},
      {
        onSuccess: async () => {
          await invalidate();
          toast.success(t`SMTP configuration removed`);
        }
      }
    );
  };

  const disabled = !canManage || configureMutation.isPending;

  return (
    <Section
      title={
        <div className="flex items-center gap-2">
          <Trans>Custom SMTP</Trans>
          {config.isEnabled ? (
            <Badge variant="outline">
              <Trans>Enabled</Trans>
            </Badge>
          ) : (
            <Badge variant="secondary">
              <Trans>Disabled</Trans>
            </Badge>
          )}
        </div>
      }
      description={t`Send transactional emails from your own SMTP server.`}
    >
      <Form onSubmit={handleSave} validationErrors={configureMutation.error?.errors} className="flex flex-col gap-4">
        <SmtpFormFields state={state} setters={setters} disabled={disabled} />
        {canManage && (
          <SmtpActions
            recipientEmail={recipientEmail}
            onRecipientChange={setRecipientEmail}
            onTest={handleTest}
            onDelete={handleDelete}
            isEnabled={config.isEnabled}
            isTesting={testMutation.isPending}
            isDeleting={deleteMutation.isPending}
            isSaving={configureMutation.isPending}
          />
        )}
      </Form>
    </Section>
  );
}

function SmtpActions(
  props: Readonly<{
    recipientEmail: string;
    onRecipientChange: (value: string) => void;
    onTest: () => void;
    onDelete: () => void;
    isEnabled: boolean;
    isTesting: boolean;
    isDeleting: boolean;
    isSaving: boolean;
  }>
) {
  return (
    <div className="flex flex-col gap-3 border-t border-border pt-4 sm:flex-row sm:items-end sm:justify-between">
      <div className="flex flex-1 flex-col gap-2">
        <Label htmlFor="smtp-recipient">
          <Trans>Test recipient</Trans>
        </Label>
        <Input
          id="smtp-recipient"
          name="recipientEmail"
          type="email"
          value={props.recipientEmail}
          onChange={(e) => props.onRecipientChange(e.target.value)}
          placeholder={t`Send a test email to this address`}
          aria-label={t`Test recipient email`}
        />
      </div>
      <div className="flex justify-end gap-2">
        <Button type="button" variant="secondary" onClick={props.onTest} isPending={props.isTesting}>
          <Trans>Send test</Trans>
        </Button>
        {props.isEnabled && (
          <Button type="button" variant="destructive" onClick={props.onDelete} isPending={props.isDeleting}>
            <Trans>Remove</Trans>
          </Button>
        )}
        <Button type="submit" isPending={props.isSaving}>
          <Trans>Save</Trans>
        </Button>
      </div>
    </div>
  );
}
