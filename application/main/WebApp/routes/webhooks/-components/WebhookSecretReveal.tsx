import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { CheckIcon, CopyIcon } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";

/**
 * Displays a webhook signing secret right after creation. The secret is shown in plain text with
 * a copy-to-clipboard button. The accompanying warning tells the user the value cannot be
 * retrieved later.
 */
export function WebhookSecretReveal({ secret }: Readonly<{ secret: string }>) {
  const [copied, setCopied] = useState(false);

  const handleCopy = async () => {
    try {
      await navigator.clipboard.writeText(secret);
      setCopied(true);
      toast.success(t`Secret copied to clipboard`);
      window.setTimeout(() => setCopied(false), 2000);
    } catch {
      toast.error(t`Failed to copy secret`);
    }
  };

  return (
    <div className="flex flex-col gap-2">
      <p className="text-sm font-medium text-foreground">
        <Trans>Signing secret</Trans>
      </p>
      <div className="flex items-center gap-2 rounded-md border bg-muted/40 px-3 py-2">
        <code className="min-w-0 flex-1 font-mono text-sm break-all">{secret}</code>
        <Button
          type="button"
          size="sm"
          variant="outline"
          onClick={() => {
            void handleCopy();
          }}
          aria-label={t`Copy signing secret`}
        >
          {copied ? <CheckIcon /> : <CopyIcon />}
          {copied ? <Trans>Copied</Trans> : <Trans>Copy</Trans>}
        </Button>
      </div>
      <p className="rounded-md border border-amber-500/40 bg-amber-500/5 px-3 py-2 text-sm text-amber-700 dark:text-amber-300">
        <Trans>Copy and store this secret now — you won't be able to see it again.</Trans>
      </p>
    </div>
  );
}
