import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { toast } from "sonner";

import { api, type Schemas } from "@/shared/lib/api/client";

import { SsoCard } from "./SsoCard";
import { buildSaveSuccess, parseDomains, runSsoTest, runSsoToggle } from "./ssoHelpers";

type MicrosoftConfig = Schemas["OrgMicrosoftSsoConfigResponse"];

export function MicrosoftSsoCardLoader({ canManage }: Readonly<{ canManage: boolean }>) {
  const { data: config, isLoading } = api.useQuery("get", "/api/account/org/sso/microsoft");
  if (isLoading || !config) {
    return null;
  }
  return <MicrosoftSsoCardInner config={config} canManage={canManage} />;
}

function MicrosoftSsoCardInner({ config, canManage }: Readonly<{ config: MicrosoftConfig; canManage: boolean }>) {
  const queryClient = useQueryClient();
  const [azureTenantId, setAzureTenantId] = useState(config.azureTenantId);
  const [clientId, setClientId] = useState(config.clientId);
  const [clientSecret, setClientSecret] = useState("");
  const [allowedDomains, setAllowedDomains] = useState(config.allowedDomains.join("\n"));

  const configureMutation = api.useMutation("put", "/api/account/org/sso/microsoft", {
    meta: { skipQueryInvalidation: true }
  });
  const enableMutation = api.useMutation("post", "/api/account/org/sso/microsoft/enable");
  const disableMutation = api.useMutation("post", "/api/account/org/sso/microsoft/disable");
  const testMutation = api.useMutation("post", "/api/account/org/sso/microsoft/test");

  const handleSave = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    await configureMutation.mutateAsync(
      {
        body: {
          azureTenantId: azureTenantId.trim(),
          clientId: clientId.trim(),
          clientSecret,
          allowedDomains: parseDomains(allowedDomains)
        }
      },
      buildSaveSuccess(queryClient, "/api/account/org/sso/microsoft", () => {
        setClientSecret("");
        toast.success(t`Microsoft SSO configuration saved`);
      })
    );
  };

  return (
    <SsoCard
      title={<Trans>Microsoft Entra ID</Trans>}
      description={t`Sign in with Microsoft accounts from your Azure tenant.`}
      isEnabled={config.isEnabled}
      canManage={canManage}
      validationErrors={configureMutation.error?.errors}
      isSaving={configureMutation.isPending}
      isToggling={enableMutation.isPending || disableMutation.isPending}
      isTesting={testMutation.isPending}
      onSave={handleSave}
      onToggle={() =>
        runSsoToggle(config.isEnabled, enableMutation, disableMutation, queryClient, "/api/account/org/sso/microsoft", {
          enable: t`Microsoft SSO enabled`,
          disable: t`Microsoft SSO disabled`
        })
      }
      onTest={() => runSsoTest(testMutation, t`Microsoft SSO test succeeded`, t`Microsoft SSO test failed`)}
      providerFieldId="ms-azure-tenant-id"
      providerFieldLabel={<Trans>Azure tenant ID</Trans>}
      providerFieldName="azureTenantId"
      providerFieldValue={azureTenantId}
      onProviderFieldChange={setAzureTenantId}
      clientId={clientId}
      onClientIdChange={setClientId}
      clientSecret={clientSecret}
      onClientSecretChange={setClientSecret}
      allowedDomains={allowedDomains}
      onAllowedDomainsChange={setAllowedDomains}
      idPrefix="ms"
    />
  );
}
