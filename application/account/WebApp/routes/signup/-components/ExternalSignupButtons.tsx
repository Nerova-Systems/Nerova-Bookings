import { Trans } from "@lingui/react/macro";
import { preferredLocaleKey } from "@repo/infrastructure/translations/constants";
import { Button } from "@repo/ui/components/Button";

import googleIconUrl from "@/shared/images/google-icon.svg";

interface ExternalSignupButtonsProps {
  isGoogleOAuthEnabled: boolean;
  isPending: boolean;
  isGoogleSignupPending: boolean;
  setIsGoogleSignupPending: (pending: boolean) => void;
}

export function ExternalSignupButtons({
  isGoogleOAuthEnabled,
  isPending,
  isGoogleSignupPending,
  setIsGoogleSignupPending
}: Readonly<ExternalSignupButtonsProps>) {
  if (!isGoogleOAuthEnabled) return null;

  const handleExternalSignup = () => {
    setIsGoogleSignupPending(true);

    const locale = localStorage.getItem(preferredLocaleKey);
    const params = new URLSearchParams();
    if (locale) {
      params.set("Locale", locale);
    }
    const queryString = params.toString();
    window.location.href = `/api/account/authentication/Google/signup/start${queryString ? `?${queryString}` : ""}`;
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
          onClick={handleExternalSignup}
          isPending={isGoogleSignupPending}
          disabled={isPending}
        >
          {!isGoogleSignupPending && <img src={googleIconUrl} alt="" aria-hidden="true" className="size-5" />}
          {isGoogleSignupPending ? <Trans>Redirecting...</Trans> : <Trans>Sign up with Google</Trans>}
        </Button>
      )}
    </>
  );
}
