import { Trans } from "@lingui/react/macro";
import { Badge } from "@repo/ui/components/Badge";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@repo/ui/components/Card";

export function EventTypeTabSection({
  title,
  description,
  children
}: Readonly<{
  title: React.ReactNode;
  description?: React.ReactNode;
  children: React.ReactNode;
}>) {
  return (
    <Card className="rounded-lg">
      <CardHeader>
        <CardTitle>{title}</CardTitle>
        {description && <CardDescription>{description}</CardDescription>}
      </CardHeader>
      <CardContent className="grid gap-4">{children}</CardContent>
    </Card>
  );
}

export function DisabledFeatureRow({
  title,
  description
}: Readonly<{
  title: React.ReactNode;
  description: React.ReactNode;
}>) {
  return (
    <div
      aria-disabled="true"
      className="flex min-h-[var(--control-height)] items-start justify-between gap-4 rounded-md border bg-muted/40 p-4 opacity-70"
    >
      <div className="grid gap-1">
        <div className="font-medium">{title}</div>
        <div className="text-sm text-muted-foreground">{description}</div>
      </div>
      <Badge variant="outline">
        <Trans>Not available</Trans>
      </Badge>
    </div>
  );
}
