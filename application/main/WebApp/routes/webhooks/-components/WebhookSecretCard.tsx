import { Trans } from "@lingui/react/macro";
import { Badge } from "@repo/ui/components/Badge";
import { Card, CardContent, CardHeader, CardTitle } from "@repo/ui/components/Card";

// The backend only returns the secret in plain text immediately after creation.
// Subsequent GETs do not expose it, so the detail page always shows the hidden state.
export function WebhookSecretCard() {
  return (
    <Card>
      <CardHeader>
        <CardTitle>
          <Trans>Signing secret</Trans>
        </CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        <p className="text-sm text-muted-foreground">
          <Trans>
            For security, the signing secret is only shown once — right after the webhook is created. If you've lost it,
            delete this webhook and create a new one to receive a fresh secret.
          </Trans>
        </p>
        <div className="flex items-center gap-2 rounded-md border bg-muted/40 px-3 py-2">
          <code className="font-mono text-sm tracking-widest text-muted-foreground">••••••••••••••••</code>
          <Badge variant="secondary">
            <Trans>Secret hidden</Trans>
          </Badge>
        </div>
      </CardContent>
    </Card>
  );
}

// The backend update endpoint does not accept eventTypeId — scope is fixed at creation.
export function WebhookScopeCard({ eventTypeId }: Readonly<{ eventTypeId: string }>) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>
          <Trans>Scope</Trans>
        </CardTitle>
      </CardHeader>
      <CardContent className="flex flex-wrap items-center gap-3">
        <Badge variant="outline">{eventTypeId}</Badge>
        <p className="text-sm text-muted-foreground">
          <Trans>The service scope is locked once a webhook is created.</Trans>
        </p>
      </CardContent>
    </Card>
  );
}
