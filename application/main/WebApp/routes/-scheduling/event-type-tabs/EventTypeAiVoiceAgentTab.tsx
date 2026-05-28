import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { FormValidationContext } from "@repo/ui/components/Form";
import { SwitchField } from "@repo/ui/components/SwitchField";
import { TextAreaField } from "@repo/ui/components/TextAreaField";

import type { EventTypeTabProps } from "./EventTypeTabTypes";

import { getEventTypeSettings, updateEventTypeSettingsSection } from "../schedulingTypes";
import { EventTypeTabSection } from "./EventTypeTabSection";

export function EventTypeAiVoiceAgentTab({ value, onChange, error }: EventTypeTabProps) {
  const settings = getEventTypeSettings(value);
  const aiVoiceAgent = settings.aiVoiceAgent;
  const updateAiVoiceAgent = (partial: Partial<typeof aiVoiceAgent>) => {
    onChange(
      updateEventTypeSettingsSection(value, "aiVoiceAgent", (current) => ({
        ...current,
        ...partial
      }))
    );
  };

  return (
    <FormValidationContext.Provider value={error?.errors ?? {}}>
      <EventTypeTabSection
        title={<Trans>AI voice agent</Trans>}
        description={
          <Trans>
            Let an AI voice agent answer booker calls, qualify them, and place them on this event type's calendar.
          </Trans>
        }
      >
        <SwitchField
          name="aiVoiceAgentEnabled"
          label={t`Enable AI voice agent`}
          checked={aiVoiceAgent.enabled}
          onCheckedChange={(enabled) => updateAiVoiceAgent({ enabled })}
        />
        <TextAreaField
          name="aiVoiceAgentConfig"
          label={t`Agent configuration`}
          lines={6}
          disabled={!aiVoiceAgent.enabled}
          value={aiVoiceAgent.agentConfig ?? ""}
          onChange={(agentConfig) => updateAiVoiceAgent({ agentConfig: agentConfig || null })}
        />
      </EventTypeTabSection>
    </FormValidationContext.Provider>
  );
}
