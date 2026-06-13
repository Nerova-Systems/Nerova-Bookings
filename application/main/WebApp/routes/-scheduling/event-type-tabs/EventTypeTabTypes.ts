import type { ApiValidationError, EventTypePayload, Schedule } from "../schedulingTypes";

export type EventTypeTabProps = Readonly<{
  eventTypeId: string;
  imageUrl: string | null;
  value: EventTypePayload;
  schedules: Schedule[];
  onChange: (value: EventTypePayload) => void;
  error?: ApiValidationError;
}>;
