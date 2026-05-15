import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";

import googleIconUrl from "@/shared/images/google-icon.svg";

interface ExternalLoginButtonsProps {
  returnPath?: string;
  isGoogleOAuthEnabled: boolean;
  isPending: boolean;
  isGoogleLoginPending: boolean;
  setIsGoogleLoginPending: (pending: boolean) => void;
}

export function ExternalLoginButtons({
  returnPath,
  isGoogleOAuthEnabled,
  isPending,
  isGoogleLoginPending,
  setIsGoogleLoginPending
}: Readonly<ExternalLoginButtonsProps>) {
  if (!isGoogleOAuthEnabled) return null;

  const handleExternalLogin = () => {
    setIsGoogleLoginPending(true);

    const params = new URLSearchParams();
    if (returnPath) {
      params.set("ReturnPath", returnPath);
    }
    try {
      const preferredTenantId = localStorage.getItem("preferred-tenant");
      if (preferredTenantId) {
        params.set("PreferredTenantId", preferredTenantId);
      }
    } catch {
      // Ignore localStorage errors
    }
    const queryString = params.toString();
    window.location.href = `/api/account/authentication/Google/login/start${queryString ? `?${queryString}` : ""}`;
  };

  return (
    <>
      <div className="flex w-full items-center gap-4">
        <div className="h-px flex-1 bg-border" />
        <span className="text-sm text-muted-foreground">
          <Trans>or</Trans>
        </span>
        <div className="h-px flex-1 bg-border" />
      </div>
      {isGoogleOAuthEnabled && (
        <Button
          type="button"
          variant="outline"
          className="w-full"
          onClick={handleExternalLogin}
          isPending={isGoogleLoginPending}
          disabled={isPending}
        >
          {!isGoogleLoginPending && <img src={googleIconUrl} alt="" aria-hidden="true" className="size-5" />}
          {isGoogleLoginPending ? <Trans>Redirecting...</Trans> : <Trans>Log in with Google</Trans>}
        </Button>
      )}
    </>
  );
}
