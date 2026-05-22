import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { toast } from "sonner";

import { api, type Schemas } from "@/shared/lib/api/client";

import { SsoCard } from "./SsoCard";
import { buildSaveSuccess, parseDomains, runSsoTest, runSsoToggle } from "./ssoHelpers";

type GoogleConfig = Schemas["OrgGoogleSsoConfigResponse"];

export function GoogleSsoCardLoader({ canManage }: Readonly<{ canManage: boolean }>) {
  const { data: config, isLoading } = api.useQuery("get", "/api/account/org/sso/google");
  if (isLoading || !config) {
    return null;
  }
  return <GoogleSsoCardInner config={config} canManage={canManage} />;
}

function GoogleSsoCardInner({ config, canManage }: Readonly<{ config: GoogleConfig; canManage: boolean }>) {
  const queryClient = useQueryClient();
  const [hostedDomain, setHostedDomain] = useState(config.hostedDomain);
  const [clientId, setClientId] = useState(config.clientId);
  const [clientSecret, setClientSecret] = useState("");
  const [allowedDomains, setAllowedDomains] = useState(config.allowedDomains.join("\n"));

  const configureMutation = api.useMutation("put", "/api/account/org/sso/google", {
    meta: { skipQueryInvalidation: true }
  });
  const enableMutation = api.useMutation("post", "/api/account/org/sso/google/enable");
  const disableMutation = api.useMutation("post", "/api/account/org/sso/google/disable");
  const testMutation = api.useMutation("post", "/api/account/org/sso/google/test");

  const handleSave = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    await configureMutation.mutateAsync(
      {
        body: {
          hostedDomain: hostedDomain.trim(),
          clientId: clientId.trim(),
          clientSecret,
          allowedDomains: parseDomains(allowedDomains)
        }
      },
      buildSaveSuccess(queryClient, "/api/account/org/sso/google", () => {
        setClientSecret("");
        toast.success(t`Google SSO configuration saved`);
      })
    );
  };

  return (
    <SsoCard
      title={<Trans>Google Workspace</Trans>}
      description={t`Sign in with Google accounts from your workspace domain.`}
      isEnabled={config.isEnabled}
      canManage={canManage}
      validationErrors={configureMutation.error?.errors}
      isSaving={configureMutation.isPending}
      isToggling={enableMutation.isPending || disableMutation.isPending}
      isTesting={testMutation.isPending}
      onSave={handleSave}
      onToggle={() =>
        runSsoToggle(config.isEnabled, enableMutation, disableMutation, queryClient, "/api/account/org/sso/google", {
          enable: t`Google SSO enabled`,
          disable: t`Google SSO disabled`
        })
      }
      onTest={() => runSsoTest(testMutation, t`Google SSO test succeeded`, t`Google SSO test failed`)}
      providerFieldId="google-hosted-domain"
      providerFieldLabel={<Trans>Workspace domain</Trans>}
      providerFieldName="hostedDomain"
      providerFieldValue={hostedDomain}
      onProviderFieldChange={setHostedDomain}
      providerFieldPlaceholder={t`acme.com`}
      clientId={clientId}
      onClientIdChange={setClientId}
      clientSecret={clientSecret}
      onClientSecretChange={setClientSecret}
      allowedDomains={allowedDomains}
      onAllowedDomainsChange={setAllowedDomains}
      idPrefix="google"
    />
  );
}
