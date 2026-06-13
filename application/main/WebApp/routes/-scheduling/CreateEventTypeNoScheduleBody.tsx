import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { DialogBody, DialogClose, DialogFooter } from "@repo/ui/components/Dialog";
import { Empty, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from "@repo/ui/components/Empty";
import { useNavigate } from "@tanstack/react-router";
import { CalendarDaysIcon } from "lucide-react";

export function CreateEventTypeNoScheduleBody({ onClose }: Readonly<{ onClose: () => void }>) {
  const navigate = useNavigate();

  return (
    <>
      <DialogBody>
        <Empty className="min-h-48 border">
          <EmptyHeader>
            <EmptyMedia variant="icon">
              <CalendarDaysIcon />
            </EmptyMedia>
            <EmptyTitle>
              <Trans>Create hours first</Trans>
            </EmptyTitle>
            <EmptyDescription>
              <Trans>Set your hours before adding services.</Trans>
            </EmptyDescription>
          </EmptyHeader>
        </Empty>
      </DialogBody>
      <DialogFooter>
        <DialogClose render={<Button type="reset" variant="outline" />}>
          <Trans>Cancel</Trans>
        </DialogClose>
        <Button
          type="button"
          onClick={() => {
            onClose();
            navigate({ to: "/availability" });
          }}
        >
          <CalendarDaysIcon />
          <Trans>Go to hours</Trans>
        </Button>
      </DialogFooter>
    </>
  );
}
