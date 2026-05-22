import { useLingui } from "@lingui/react/macro";
import { CheckIcon, CopyIcon } from "lucide-react";

import { useCopy } from "../hooks/useCopy";
import { cn } from "../utils";
import { Button } from "./Button";
import { Input } from "./Input";

/**
 * OAuth / API token display with masked input and copy-to-clipboard button.
 * Ported from cal.com `packages/ui/components/TokenHandler/TokenHandler.tsx` (cf2a55c).
 *
 * No prop deviations.
 */
interface TokenHandlerProps {
  token: string;
  /** If true the token is shown as a password input. @default true */
  masked?: boolean;
  className?: string;
}

export function TokenHandler({ token, masked = true, className }: TokenHandlerProps) {
  const { t } = useLingui();
  const { isCopied, copyToClipboard } = useCopy();

  return (
    <div data-slot="token-handler" className={cn("flex items-center gap-2", className)}>
      <Input
        readOnly
        type={masked ? "password" : "text"}
        value={token}
        className="font-mono"
        aria-label={t`API token`}
      />
      <Button
        variant="outline"
        size="icon"
        onClick={() => copyToClipboard(token)}
        aria-label={isCopied ? t`Copied` : t`Copy token`}
      >
        {isCopied ? <CheckIcon className="text-success" /> : <CopyIcon />}
      </Button>
    </div>
  );
}
