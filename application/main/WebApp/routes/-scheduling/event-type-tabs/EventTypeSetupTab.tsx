import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { FormValidationContext } from "@repo/ui/components/Form";
import { NumberField } from "@repo/ui/components/NumberField";
import { TextAreaField } from "@repo/ui/components/TextAreaField";
import { TextField } from "@repo/ui/components/TextField";

import type { EventTypeTabProps } from "./EventTypeTabTypes";

import { slugify } from "../schedulingTypes";
import { EventTypeTabSection } from "./EventTypeTabSection";

export function EventTypeSetupTab({ value, onChange, error }: EventTypeTabProps) {
  return (
    <FormValidationContext.Provider value={error?.errors ?? {}}>
      <div className="grid gap-5">
        <EventTypeTabSection
          title={<Trans>Setup</Trans>}
          description={<Trans>Define the public booking page details people see before they choose a time.</Trans>}
        >
          <div className="grid gap-4 md:grid-cols-2">
            <TextField
              name="title"
              label={t`Title`}
              required={true}
              value={value.title}
              onChange={(title) => onChange({ ...value, title, slug: value.slug || slugify(title) })}
            />
            <TextField
              name="slug"
              label={t`Slug`}
              required={true}
              value={value.slug}
              onChange={(slug) => onChange({ ...value, slug: slugify(slug) })}
            />
          </div>
          <TextAreaField
            name="description"
            label={t`Description`}
            lines={4}
            value={value.description ?? ""}
            onChange={(description) => onChange({ ...value, description: description || null })}
          />
        </EventTypeTabSection>
        <EventTypeTabSection
          title={<Trans>Duration</Trans>}
          description={<Trans>Set how long this booking reserves on the calendar.</Trans>}
        >
          <NumberField
            name="durationMinutes"
            label={t`Duration`}
            minValue={5}
            maxValue={1440}
            value={value.durationMinutes}
            onChange={(durationMinutes) => onChange({ ...value, durationMinutes: durationMinutes ?? 30 })}
          />
        </EventTypeTabSection>
      </div>
    </FormValidationContext.Provider>
  );
}
