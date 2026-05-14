import { Trans } from "@lingui/react/macro";
import { preferredLocaleKey } from "@repo/infrastructure/translations/constants";
import { Button } from "@repo/ui/components/Button";

import facebookIconUrl from "@/shared/images/facebook-icon.svg";
import googleIconUrl from "@/shared/images/google-icon.svg";

interface ExternalSignupButtonsProps {
  isGoogleOAuthEnabled: boolean;
  isFacebookOAuthEnabled: boolean;
  isPending: boolean;
  isGoogleSignupPending: boolean;
  isFacebookSignupPending: boolean;
  setIsGoogleSignupPending: (pending: boolean) => void;
  setIsFacebookSignupPending: (pending: boolean) => void;
}

export function ExternalSignupButtons({
  isGoogleOAuthEnabled,
  isFacebookOAuthEnabled,
  isPending,
  isGoogleSignupPending,
  isFacebookSignupPending,
  setIsGoogleSignupPending,
  setIsFacebookSignupPending
}: Readonly<ExternalSignupButtonsProps>) {
  if (!isGoogleOAuthEnabled && !isFacebookOAuthEnabled) return null;

  const handleExternalSignup = (provider: "Google" | "Facebook") => {
    if (provider === "Google") {
      setIsGoogleSignupPending(true);
    } else {
      setIsFacebookSignupPending(true);
    }

    const locale = localStorage.getItem(preferredLocaleKey);
    const params = new URLSearchParams();
    if (locale) {
      params.set("Locale", locale);
    }
    const queryString = params.toString();
    window.location.href = `/api/account/authentication/${provider}/signup/start${queryString ? `?${queryString}` : ""}`;
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
          onClick={() => handleExternalSignup("Google")}
          isPending={isGoogleSignupPending}
          disabled={isPending}
        >
          {!isGoogleSignupPending && <img src={googleIconUrl} alt="" aria-hidden="true" className="size-5" />}
          {isGoogleSignupPending ? <Trans>Redirecting...</Trans> : <Trans>Sign up with Google</Trans>}
        </Button>
      )}
      {isFacebookOAuthEnabled && (
        <Button
          type="button"
          variant="outline"
          className="w-full"
          onClick={() => handleExternalSignup("Facebook")}
          isPending={isFacebookSignupPending}
          disabled={isPending}
        >
          {!isFacebookSignupPending && <img src={facebookIconUrl} alt="" aria-hidden="true" className="size-5" />}
          {isFacebookSignupPending ? <Trans>Redirecting...</Trans> : <Trans>Sign up with Facebook</Trans>}
        </Button>
      )}
    </>
  );
}
