import { Trans } from "@lingui/react/macro";
import { Empty, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from "@repo/ui/components/Empty";
import { SettingsIcon } from "lucide-react";

export function EventTypePlaceholderTab({ name }: Readonly<{ name: string }>) {
  return (
    <Empty className="min-h-64 border">
      <EmptyHeader>
        <EmptyMedia variant="icon">
          <SettingsIcon />
        </EmptyMedia>
        <EmptyTitle>{name}</EmptyTitle>
        <EmptyDescription>
          <Trans>No settings available yet.</Trans>
        </EmptyDescription>
      </EmptyHeader>
    </Empty>
  );
}
