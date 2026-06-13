import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { DialogBody, DialogClose, DialogFooter, DialogForm } from "@repo/ui/components/Dialog";
import { useDialogSetDirty } from "@repo/ui/components/DirtyDialogContext";
import { NumberField } from "@repo/ui/components/NumberField";
import { TextAreaField } from "@repo/ui/components/TextAreaField";
import { TextField } from "@repo/ui/components/TextField";
import { useNavigate } from "@tanstack/react-router";
import { useState } from "react";
import { flushSync } from "react-dom";
import { toast } from "sonner";

import { api, queryClient } from "@/shared/lib/api/client";

import { GeneralApiErrors } from "./ApiErrors";
import { CreateAdvancedSettings } from "./CreateAdvancedSettings";
import { LocationTypeSelect } from "./LocationTypeSelect";
import {
  getEventTypeSettings,
  isEventTypePayloadSubmittable,
  newEventTypePayload,
  type EventTypeSettings,
  type Schedule,
  slugify,
  updateEventTypeSettings,
  updateEventTypeSettingsSection
} from "./schedulingTypes";

export function CreateEventTypeDialogBody({
  defaultSchedule,
  onClose
}: Readonly<{
  defaultSchedule: Schedule | undefined;
  onClose: () => void;
}>) {
  const navigate = useNavigate();
  const setDirty = useDialogSetDirty();
  const [slugWasEdited, setSlugWasEdited] = useState(false);
  const [draft, setDraft] = useState(() => newEventTypePayload(defaultSchedule?.id ?? ""));
  const { error, isPending, mutate } = api.useMutation("post", "/api/event-types", {
    onSuccess: (eventType) => {
      toast.success(t`Service created`);
      void queryClient.invalidateQueries();
      flushSync(() => setDirty(false));
      onClose();
      navigate({
        to: "/event-types/$eventTypeId",
        params: { eventTypeId: eventType.id },
        search: { tabName: "setup" }
      });
    }
  });
  const canSubmit = defaultSchedule !== undefined && isEventTypePayloadSubmittable(draft);

  const updateDraft = (nextDraft: typeof draft) => {
    setDirty(true);
    setDraft(nextDraft);
  };
  const updatePayment = (payment: Partial<EventTypeSettings["payment"]>) => {
    updateDraft(
      updateEventTypeSettingsSection(draft, "payment", (currentPayment) => ({
        ...currentPayment,
        ...payment
      }))
    );
  };
  const updatePrimaryLocation = (locationType: string, locationValue: string | null) => {
    const nextDraft = { ...draft, locationType, locationValue };
    updateDraft(
      updateEventTypeSettings(nextDraft, (nextSettings) => ({
        ...nextSettings,
        locations: replacePrimaryLocation(nextSettings.locations, locationType, locationValue)
      }))
    );
  };
  const settings = getEventTypeSettings(draft);

  return (
    <DialogForm
      validationErrors={error?.errors}
      onSubmit={(event) => {
        event.preventDefault();
        if (!defaultSchedule || !canSubmit) return;
        mutate({ body: { ...draft, scheduleId: defaultSchedule.id } });
      }}
    >
      <DialogBody>
        <GeneralApiErrors error={error} />
        <div className="grid gap-4">
          <TextField
            name="title"
            label={t`Service name`}
            description={t`Use the name clients already know, like "Gel manicure".`}
            required={true}
            autoFocus={true}
            value={draft.title}
            onChange={(title) => updateDraft({ ...draft, title, slug: slugWasEdited ? draft.slug : slugify(title) })}
          />
          <TextAreaField
            name="description"
            label={t`Short description`}
            description={t`A simple note clients see before they book.`}
            lines={3}
            value={draft.description ?? ""}
            onChange={(description) => updateDraft({ ...draft, description: description || null })}
          />
          <NumberField
            name="durationMinutes"
            label={t`How long it takes`}
            description={t`Minutes blocked in your calendar.`}
            minValue={5}
            maxValue={1440}
            value={draft.durationMinutes}
            onChange={(durationMinutes) => updateDraft({ ...draft, durationMinutes: durationMinutes ?? 30 })}
          />
        </div>
        <div className="grid gap-4 sm:grid-cols-2">
          <NumberField
            name="paymentPrice"
            label={t`Price`}
            description={t`Leave empty if the price changes per client.`}
            minValue={0}
            step={0.01}
            decimalPlaces={2}
            allowEmpty={true}
            value={settings.payment.price ?? undefined}
            onChange={(price) => updatePayment({ price: price ?? null })}
          />
          <NumberField
            name="paymentDepositAmount"
            label={t`Deposit`}
            description={t`Optional amount clients pay to secure the booking.`}
            minValue={0}
            step={0.01}
            decimalPlaces={2}
            allowEmpty={true}
            value={settings.payment.depositAmount ?? undefined}
            onChange={(depositAmount) =>
              updatePayment({ depositAmount: depositAmount ?? null, requiresDeposit: depositAmount !== null })
            }
          />
        </div>
        <div className="grid gap-4 sm:grid-cols-2">
          <LocationTypeSelect
            value={draft.locationType ?? ""}
            onChange={(locationType) => updatePrimaryLocation(locationType, draft.locationValue ?? null)}
          />
          <TextField
            name="locationValue"
            label={t`Address, phone number, or video link`}
            description={t`Leave blank if you confirm the exact place with the client later.`}
            value={draft.locationValue ?? ""}
            onChange={(locationValue) => updatePrimaryLocation(draft.locationType ?? "link", locationValue || null)}
          />
        </div>
        <CreateAdvancedSettings
          draft={draft}
          slugWasEdited={slugWasEdited}
          onSlugEdited={setSlugWasEdited}
          onChange={updateDraft}
        />
      </DialogBody>
      <DialogFooter>
        <DialogClose render={<Button type="reset" variant="outline" disabled={isPending} />}>
          <Trans>Cancel</Trans>
        </DialogClose>
        <Button type="submit" disabled={!canSubmit} isPending={isPending}>
          <Trans>Continue</Trans>
        </Button>
      </DialogFooter>
    </DialogForm>
  );
}

function replacePrimaryLocation(
  locations: Array<{ type: string; value: string | null; displayLocationPubliclyToTeam: boolean }>,
  type: string,
  value: string | null
) {
  const primaryLocation = { type, value: value?.trim() || null, displayLocationPubliclyToTeam: false };
  return locations.length === 0 ? [primaryLocation] : [primaryLocation, ...locations.slice(1)];
}
