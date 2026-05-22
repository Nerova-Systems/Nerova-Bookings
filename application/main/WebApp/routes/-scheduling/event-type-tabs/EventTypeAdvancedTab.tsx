/* eslint-disable max-lines, max-lines-per-function */
import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { FormValidationContext } from "@repo/ui/components/Form";
import { NumberField } from "@repo/ui/components/NumberField";
import { SelectContent, SelectItem, SelectTrigger, SelectValue } from "@repo/ui/components/Select";
import { SelectField } from "@repo/ui/components/SelectField";
import { SwitchField } from "@repo/ui/components/SwitchField";
import { TextField } from "@repo/ui/components/TextField";

import type { EventTypeTabProps } from "./EventTypeTabTypes";

import { getEventTypeSettings, updateEventTypeSettings, updateEventTypeSettingsSection } from "../schedulingTypes";
import { EventTypePrivateLinksSection } from "./EventTypePrivateLinksSection";
import { EventTypeTabSection } from "./EventTypeTabSection";

export function EventTypeAdvancedTab({ eventTypeId, value, onChange, error }: EventTypeTabProps) {
  const settings = getEventTypeSettings(value);
  const interfaceLanguages = [
    { value: "auto", label: t`Browser default` },
    { value: "en-US", label: t`English` },
    { value: "da-DK", label: t`Danish` }
  ];
  const hasRecurrence = settings.recurrence !== null;

  const updateSettings = (updater: Parameters<typeof updateEventTypeSettings>[1]) =>
    onChange(updateEventTypeSettings(value, updater));
  const updateConfirmationPolicy = (
    confirmationPolicy: Partial<ReturnType<typeof getEventTypeSettings>["confirmationPolicy"]>
  ) => {
    onChange(
      updateEventTypeSettingsSection(value, "confirmationPolicy", (currentConfirmationPolicy) => ({
        ...currentConfirmationPolicy,
        ...confirmationPolicy
      }))
    );
  };
  const updateCancellationPolicy = (
    cancellationPolicy: Partial<ReturnType<typeof getEventTypeSettings>["cancellationPolicy"]>
  ) => {
    onChange(
      updateEventTypeSettingsSection(value, "cancellationPolicy", (currentCancellationPolicy) => ({
        ...currentCancellationPolicy,
        ...cancellationPolicy
      }))
    );
  };
  const updateReschedulePolicy = (
    reschedulePolicy: Partial<ReturnType<typeof getEventTypeSettings>["reschedulePolicy"]>
  ) => {
    onChange(
      updateEventTypeSettingsSection(value, "reschedulePolicy", (currentReschedulePolicy) => ({
        ...currentReschedulePolicy,
        ...reschedulePolicy
      }))
    );
  };
  const updateRedirects = (redirects: Partial<ReturnType<typeof getEventTypeSettings>["redirects"]>) => {
    onChange(
      updateEventTypeSettingsSection(value, "redirects", (currentRedirects) => ({
        ...currentRedirects,
        ...redirects
      }))
    );
  };
  const updateSeats = (seats: Partial<ReturnType<typeof getEventTypeSettings>["seats"]>) => {
    onChange(
      updateEventTypeSettingsSection(value, "seats", (currentSeats) => ({
        ...currentSeats,
        ...seats
      }))
    );
  };
  const updatePrivacy = (privacy: Partial<ReturnType<typeof getEventTypeSettings>["privacy"]>) => {
    onChange(updateEventTypeSettingsSection(value, "privacy", (current) => ({ ...current, ...privacy })));
  };
  const updateEmail = (email: Partial<ReturnType<typeof getEventTypeSettings>["email"]>) => {
    onChange(updateEventTypeSettingsSection(value, "email", (current) => ({ ...current, ...email })));
  };
  const updateTimezone = (timezone: Partial<ReturnType<typeof getEventTypeSettings>["timezone"]>) => {
    onChange(updateEventTypeSettingsSection(value, "timezone", (current) => ({ ...current, ...timezone })));
  };

  return (
    <FormValidationContext.Provider value={error?.errors ?? {}}>
      <div className="grid gap-5">
        <EventTypeTabSection
          title={<Trans>Visibility</Trans>}
          description={<Trans>Control whether this booking page is listed publicly.</Trans>}
        >
          <SwitchField
            name="hidden"
            label={t`Hidden`}
            checked={value.hidden}
            onCheckedChange={(hidden) => onChange({ ...value, hidden })}
          />
        </EventTypeTabSection>
        <EventTypeTabSection
          title={<Trans>Event name</Trans>}
          description={<Trans>Customize the calendar event title and the reply-to address bookers see.</Trans>}
        >
          <div className="grid gap-4 md:grid-cols-2">
            <TextField
              name="eventName"
              label={t`Custom event name`}
              value={settings.email.eventName ?? ""}
              onChange={(eventName) => updateEmail({ eventName: eventName || null })}
            />
            <TextField
              name="customReplyToEmail"
              label={t`Custom reply-to email`}
              value={settings.email.customReplyToEmail ?? ""}
              onChange={(customReplyToEmail) => updateEmail({ customReplyToEmail: customReplyToEmail || null })}
            />
          </div>
        </EventTypeTabSection>
        <EventTypeTabSection
          title={<Trans>Privacy</Trans>}
          description={<Trans>Hide booker information from external calendars and disable guests.</Trans>}
        >
          <div className="grid gap-4 md:grid-cols-2">
            <SwitchField
              name="disableGuests"
              label={t`Disable guests`}
              checked={settings.privacy.disableGuests}
              onCheckedChange={(disableGuests) => updatePrivacy({ disableGuests })}
            />
            <SwitchField
              name="hideCalendarNotes"
              label={t`Hide calendar notes`}
              checked={settings.privacy.hideCalendarNotes}
              onCheckedChange={(hideCalendarNotes) => updatePrivacy({ hideCalendarNotes })}
            />
            <SwitchField
              name="hideCalendarEventDetails"
              label={t`Hide calendar event details`}
              checked={settings.privacy.hideCalendarEventDetails}
              onCheckedChange={(hideCalendarEventDetails) => updatePrivacy({ hideCalendarEventDetails })}
            />
          </div>
        </EventTypeTabSection>
        <EventTypeTabSection
          title={<Trans>Timezone</Trans>}
          description={<Trans>Lock the booking page timezone or follow the booker.</Trans>}
        >
          <div className="grid gap-4 md:grid-cols-2">
            <SwitchField
              name="lockTimeZoneToggleOnBookingPage"
              label={t`Lock timezone on booking page`}
              checked={settings.timezone.lockTimeZoneToggleOnBookingPage}
              onCheckedChange={(lockTimeZoneToggleOnBookingPage) => updateTimezone({ lockTimeZoneToggleOnBookingPage })}
            />
            <TextField
              name="lockedTimeZone"
              label={t`Locked timezone`}
              disabled={!settings.timezone.lockTimeZoneToggleOnBookingPage}
              value={settings.timezone.lockedTimeZone ?? ""}
              onChange={(lockedTimeZone) => updateTimezone({ lockedTimeZone: lockedTimeZone || null })}
            />
            <SwitchField
              name="useBookerTimezone"
              label={t`Use booker timezone for organizer`}
              checked={settings.timezone.useBookerTimezone}
              onCheckedChange={(useBookerTimezone) => updateTimezone({ useBookerTimezone })}
            />
          </div>
        </EventTypeTabSection>
        <EventTypeTabSection
          title={<Trans>Confirmation</Trans>}
          description={<Trans>Control manual approval and booker email checks.</Trans>}
        >
          <div className="grid gap-4 md:grid-cols-2">
            <SwitchField
              name="requiresConfirmation"
              label={t`Requires confirmation`}
              checked={settings.confirmationPolicy.requiresConfirmation}
              onCheckedChange={(requiresConfirmation) => updateConfirmationPolicy({ requiresConfirmation })}
            />
            <SwitchField
              name="requiresBookerEmailVerification"
              label={t`Email verification`}
              checked={settings.confirmationPolicy.requiresBookerEmailVerification}
              onCheckedChange={(requiresBookerEmailVerification) =>
                updateConfirmationPolicy({ requiresBookerEmailVerification })
              }
            />
            <SwitchField
              name="blockSlotWhilePending"
              label={t`Block slot while pending`}
              checked={settings.confirmationPolicy.blockSlotWhilePending}
              onCheckedChange={(blockSlotWhilePending) => updateConfirmationPolicy({ blockSlotWhilePending })}
            />
            <SwitchField
              name="requiresConfirmationForFreeEmail"
              label={t`Confirm free-email bookers`}
              checked={settings.confirmationPolicy.requiresConfirmationForFreeEmail}
              onCheckedChange={(requiresConfirmationForFreeEmail) =>
                updateConfirmationPolicy({ requiresConfirmationForFreeEmail })
              }
            />
            <SwitchField
              name="requiresCancellationReason"
              label={t`Require cancellation reason`}
              checked={settings.confirmationPolicy.requiresCancellationReason}
              onCheckedChange={(requiresCancellationReason) => updateConfirmationPolicy({ requiresCancellationReason })}
            />
          </div>
        </EventTypeTabSection>
        <EventTypeTabSection
          title={<Trans>Cancellation and reschedule</Trans>}
          description={<Trans>Set whether bookers can change bookings and how much notice is required.</Trans>}
        >
          <div className="grid gap-4 md:grid-cols-2">
            <SwitchField
              name="allowCancellation"
              label={t`Allow cancellation`}
              checked={settings.cancellationPolicy.allowCancellation}
              onCheckedChange={(allowCancellation) => updateCancellationPolicy({ allowCancellation })}
            />
            <NumberField
              name="cancellationMinimumNoticeMinutes"
              label={t`Cancellation notice`}
              minValue={0}
              maxValue={525600}
              allowEmpty={true}
              disabled={!settings.cancellationPolicy.allowCancellation}
              value={settings.cancellationPolicy.minimumNoticeMinutes ?? undefined}
              onChange={(minimumNoticeMinutes) => updateCancellationPolicy({ minimumNoticeMinutes })}
            />
            <SwitchField
              name="allowReschedule"
              label={t`Allow reschedule`}
              checked={settings.reschedulePolicy.allowReschedule}
              onCheckedChange={(allowReschedule) => updateReschedulePolicy({ allowReschedule })}
            />
            <NumberField
              name="rescheduleMinimumNoticeMinutes"
              label={t`Reschedule notice`}
              minValue={0}
              maxValue={525600}
              allowEmpty={true}
              disabled={!settings.reschedulePolicy.allowReschedule}
              value={settings.reschedulePolicy.minimumNoticeMinutes ?? undefined}
              onChange={(minimumNoticeMinutes) => updateReschedulePolicy({ minimumNoticeMinutes })}
            />
            <SwitchField
              name="allowReschedulingPastBookings"
              label={t`Allow rescheduling past bookings`}
              checked={settings.reschedulePolicy.allowReschedulingPastBookings}
              onCheckedChange={(allowReschedulingPastBookings) =>
                updateReschedulePolicy({ allowReschedulingPastBookings })
              }
            />
            <SwitchField
              name="allowReschedulingCancelledBookings"
              label={t`Allow rescheduling cancelled bookings`}
              checked={settings.reschedulePolicy.allowReschedulingCancelledBookings}
              onCheckedChange={(allowReschedulingCancelledBookings) =>
                updateReschedulePolicy({ allowReschedulingCancelledBookings })
              }
            />
          </div>
        </EventTypeTabSection>
        <EventTypeTabSection
          title={<Trans>Redirects</Trans>}
          description={<Trans>Send bookers to a custom page after success or cancellation.</Trans>}
        >
          <div className="grid gap-4 md:grid-cols-2">
            <TextField
              name="successUrl"
              label={t`Success URL`}
              value={settings.redirects.successUrl ?? ""}
              onChange={(successUrl) => updateRedirects({ successUrl: successUrl || null })}
            />
            <TextField
              name="cancellationUrl"
              label={t`Cancellation URL`}
              value={settings.redirects.cancellationUrl ?? ""}
              onChange={(cancellationUrl) => updateRedirects({ cancellationUrl: cancellationUrl || null })}
            />
          </div>
        </EventTypeTabSection>
        <EventTypeTabSection
          title={<Trans>Language</Trans>}
          description={<Trans>Choose which language the public booking page renders in.</Trans>}
        >
          <SelectField
            name="interfaceLanguage"
            label={t`Interface language`}
            items={interfaceLanguages}
            value={settings.interfaceLanguage ?? "auto"}
            onValueChange={(interfaceLanguage) =>
              updateSettings((nextSettings) => ({
                ...nextSettings,
                interfaceLanguage: interfaceLanguage === "auto" ? null : (interfaceLanguage ?? null)
              }))
            }
          >
            <SelectTrigger>
              <SelectValue>
                {(interfaceLanguage: string) =>
                  interfaceLanguages.find((item) => item.value === interfaceLanguage)?.label
                }
              </SelectValue>
            </SelectTrigger>
            <SelectContent>
              {interfaceLanguages.map((interfaceLanguage) => (
                <SelectItem key={interfaceLanguage.value} value={interfaceLanguage.value}>
                  {interfaceLanguage.label}
                </SelectItem>
              ))}
            </SelectContent>
          </SelectField>
        </EventTypeTabSection>
        <EventTypeTabSection
          title={<Trans>Private links</Trans>}
          description={<Trans>Share one-off booking links that bypass the public schedule.</Trans>}
        >
          <EventTypePrivateLinksSection eventTypeId={eventTypeId} />
        </EventTypeTabSection>
        <EventTypeTabSection
          title={<Trans>Seats</Trans>}
          description={
            hasRecurrence ? (
              <Trans>Seats cannot be enabled while recurring events are enabled.</Trans>
            ) : (
              <Trans>Offer multiple seats for each available time.</Trans>
            )
          }
        >
          <div className="grid gap-4 md:grid-cols-2">
            <SwitchField
              name="seatsEnabled"
              label={t`Offer seats`}
              checked={settings.seats.enabled}
              disabled={hasRecurrence}
              onCheckedChange={(enabled) =>
                updateSeats({ enabled, capacity: enabled ? (settings.seats.capacity ?? 2) : null })
              }
            />
            <NumberField
              name="seatsCapacity"
              label={t`Seat capacity`}
              minValue={1}
              maxValue={10000}
              disabled={!settings.seats.enabled || hasRecurrence}
              value={settings.seats.capacity ?? 2}
              onChange={(capacity) => updateSeats({ capacity: capacity ?? 2 })}
            />
            <SwitchField
              name="showAttendeeInfo"
              label={t`Show attendee info`}
              checked={settings.seats.showAttendeeInfo}
              disabled={!settings.seats.enabled || hasRecurrence}
              onCheckedChange={(showAttendeeInfo) => updateSeats({ showAttendeeInfo })}
            />
          </div>
        </EventTypeTabSection>
      </div>
    </FormValidationContext.Provider>
  );
}
