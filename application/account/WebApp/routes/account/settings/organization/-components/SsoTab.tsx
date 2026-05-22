import { GoogleSsoCardLoader } from "./GoogleSsoCard";
import { MicrosoftSsoCardLoader } from "./MicrosoftSsoCard";

interface OrgSsoTabProps {
  canManage: boolean;
}

export function OrgSsoTab({ canManage }: Readonly<OrgSsoTabProps>) {
  // TODO(u4-org-settings): SAML SSO is not yet supported by the backend.
  // Only Microsoft and Google OAuth providers are wired up.
  return (
    <div className="flex flex-col gap-6">
      <MicrosoftSsoCardLoader canManage={canManage} />
      <GoogleSsoCardLoader canManage={canManage} />
    </div>
  );
}
