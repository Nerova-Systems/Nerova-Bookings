import { Trans } from "@lingui/react/macro";
import { Badge } from "@repo/ui/components/Badge";
import { Button } from "@repo/ui/components/Button";
import { Grid3X3Icon, PlusIcon } from "lucide-react";

export function EventTypeAppsTab() {
  return (
    <div className="grid gap-5">
      <div className="flex min-h-[20rem] flex-col items-center justify-center gap-4 rounded-lg border border-dashed bg-background p-8 text-center">
        <div className="flex size-16 items-center justify-center rounded-full bg-muted text-muted-foreground">
          <Grid3X3Icon className="size-8" />
        </div>
        <div className="grid gap-2">
          <div className="text-xl font-semibold">
            <Trans>No apps installed</Trans>
          </div>
          <p className="max-w-md text-sm text-muted-foreground">
            <Trans>
              Connect other tools you use to Cal.com. Apps help you automate scheduling and reduce manual work.
            </Trans>
          </p>
        </div>
        <Button type="button" variant="outline">
          <Trans>Browse app store</Trans>
        </Button>
      </div>
      <div className="rounded-lg border bg-muted/30 p-4">
        <div className="mb-4">
          <div className="font-semibold">
            <Trans>Available apps</Trans>
          </div>
          <p className="text-sm text-muted-foreground">
            <Trans>View popular apps below and explore more in our App Store</Trans>
          </p>
        </div>
        <div className="rounded-lg border bg-background p-4">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div className="min-w-0">
              <div className="flex items-center gap-2 font-medium">
                <Trans>Cal Video</Trans>
                <Badge variant="secondary">
                  <Trans>Conferencing</Trans>
                </Badge>
              </div>
              <p className="truncate text-sm text-muted-foreground">
                <Trans>Built-in video meetings for your Cal.com bookings.</Trans>
              </p>
            </div>
            <Button type="button" variant="outline" size="sm" disabled>
              <PlusIcon />
              <Trans>Add</Trans>
            </Button>
          </div>
        </div>
      </div>
    </div>
  );
}
