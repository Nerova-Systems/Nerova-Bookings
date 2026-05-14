import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";

import facebookIconUrl from "@/shared/images/facebook-icon.svg";
import googleIconUrl from "@/shared/images/google-icon.svg";

interface ExternalLoginButtonsProps {
  returnPath?: string;
  isGoogleOAuthEnabled: boolean;
  isFacebookOAuthEnabled: boolean;
  isPending: boolean;
  isGoogleLoginPending: boolean;
  isFacebookLoginPending: boolean;
  setIsGoogleLoginPending: (pending: boolean) => void;
  setIsFacebookLoginPending: (pending: boolean) => void;
}

export function ExternalLoginButtons({
  returnPath,
  isGoogleOAuthEnabled,
  isFacebookOAuthEnabled,
  isPending,
  isGoogleLoginPending,
  isFacebookLoginPending,
  setIsGoogleLoginPending,
  setIsFacebookLoginPending
}: Readonly<ExternalLoginButtonsProps>) {
  if (!isGoogleOAuthEnabled && !isFacebookOAuthEnabled) return null;

  const handleExternalLogin = (provider: "Google" | "Facebook") => {
    if (provider === "Google") {
      setIsGoogleLoginPending(true);
    } else {
      setIsFacebookLoginPending(true);
    }

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
    window.location.href = `/api/account/authentication/${provider}/login/start${queryString ? `?${queryString}` : ""}`;
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
          onClick={() => handleExternalLogin("Google")}
          isPending={isGoogleLoginPending}
          disabled={isPending}
        >
          {!isGoogleLoginPending && <img src={googleIconUrl} alt="" aria-hidden="true" className="size-5" />}
          {isGoogleLoginPending ? <Trans>Redirecting...</Trans> : <Trans>Log in with Google</Trans>}
        </Button>
      )}
      {isFacebookOAuthEnabled && (
        <Button
          type="button"
          variant="outline"
          className="w-full"
          onClick={() => handleExternalLogin("Facebook")}
          isPending={isFacebookLoginPending}
          disabled={isPending}
        >
          {!isFacebookLoginPending && <img src={facebookIconUrl} alt="" aria-hidden="true" className="size-5" />}
          {isFacebookLoginPending ? <Trans>Redirecting...</Trans> : <Trans>Log in with Facebook</Trans>}
        </Button>
      )}
    </>
  );
}
