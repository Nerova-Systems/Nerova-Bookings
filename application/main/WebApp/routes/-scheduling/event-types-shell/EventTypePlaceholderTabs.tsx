import { Trans } from "@lingui/react/macro";

export function EventTypePlaceholderTab({ name }: Readonly<{ name: string }>) {
  return (
    <div className="rounded-md border border-dashed p-6 text-sm text-muted-foreground">
      <p className="font-medium text-foreground">{name}</p>
      <p className="mt-1">
        <Trans>This tab shell is ready for the dedicated editor component.</Trans>
      </p>
    </div>
  );
}
