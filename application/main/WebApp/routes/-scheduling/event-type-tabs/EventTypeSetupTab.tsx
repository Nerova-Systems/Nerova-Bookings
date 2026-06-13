/* eslint-disable max-lines, max-lines-per-function */
import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { FormValidationContext } from "@repo/ui/components/Form";
import { NumberField } from "@repo/ui/components/NumberField";
import { SelectContent, SelectItem, SelectTrigger, SelectValue } from "@repo/ui/components/Select";
import { SelectField } from "@repo/ui/components/SelectField";
import { TextAreaField } from "@repo/ui/components/TextAreaField";
import { TextField } from "@repo/ui/components/TextField";
import { useMutation } from "@tanstack/react-query";
import { ImageIcon, Trash2Icon, UploadIcon } from "lucide-react";
import { useEffect, useRef, useState } from "react";
import { toast } from "sonner";

import { apiClient, queryClient, type Schemas } from "@/shared/lib/api/client";

import type { EventTypeTabProps } from "./EventTypeTabTypes";

import { LocationTypeSelect } from "../LocationTypeSelect";
import { getEventTypeSettings, slugify, updateEventTypeSettings } from "../schedulingTypes";
import { EventTypeTabSection } from "./EventTypeTabSection";

const ALLOWED_IMAGE_TYPES = ["image/jpeg", "image/png", "image/webp"];
const MAX_UPLOAD_BYTES = 2 * 1024 * 1024;
const MAX_IMAGE_SIDE = 640;
const JPEG_QUALITY = 0.85;

export function EventTypeSetupTab({ eventTypeId, imageUrl, value, onChange, error }: EventTypeTabProps) {
  const settings = getEventTypeSettings(value);
  const [durationOptionsText, setDurationOptionsText] = useState(formatDurationOptions(settings.durationOptions));

  useEffect(() => {
    setDurationOptionsText(formatDurationOptions(getEventTypeSettings(value).durationOptions));
  }, [value]);

  const bookerLayouts = [
    { value: "month", label: t`Month` },
    { value: "week", label: t`Week` },
    { value: "column", label: t`Column` }
  ];

  const updateSettings = (updater: Parameters<typeof updateEventTypeSettings>[1]) =>
    onChange(updateEventTypeSettings(value, updater));

  const updatePrimaryLocation = (locationType: string, locationValue: string | null) => {
    const nextValue = {
      ...value,
      locationType,
      locationValue
    };

    onChange(
      updateEventTypeSettings(nextValue, (nextSettings) => ({
        ...nextSettings,
        locations: replacePrimaryLocation(nextSettings.locations, locationType, locationValue)
      }))
    );
  };

  return (
    <FormValidationContext.Provider value={error?.errors ?? {}}>
      <div className="grid gap-5">
        <EventTypeTabSection
          title={<Trans>Setup</Trans>}
          description={<Trans>Define the public booking page details people see before they choose a time.</Trans>}
        >
          <EventTypeImageUpload eventTypeId={eventTypeId} imageUrl={imageUrl} />
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
              label={t`Link name`}
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
            onChange={(durationMinutes) => {
              const nextDuration = durationMinutes ?? 30;
              onChange(
                updateEventTypeSettings({ ...value, durationMinutes: nextDuration }, (nextSettings) => ({
                  ...nextSettings,
                  durationOptions: ensureDurationOption(nextSettings.durationOptions, nextDuration)
                }))
              );
            }}
          />
          <TextField
            name="durationOptions"
            label={t`Duration options`}
            value={durationOptionsText}
            onChange={setDurationOptionsText}
            onBlur={() => {
              const durationOptions = parseDurationOptions(durationOptionsText, value.durationMinutes);
              updateSettings((nextSettings) => ({ ...nextSettings, durationOptions }));
              setDurationOptionsText(formatDurationOptions(durationOptions));
            }}
          />
        </EventTypeTabSection>
        <EventTypeTabSection
          title={<Trans>Booking page</Trans>}
          description={<Trans>Choose the client layout and accent color for this service.</Trans>}
        >
          <div className="grid gap-4 md:grid-cols-2">
            <SelectField
              name="bookerLayout"
              label={t`Client page layout`}
              items={bookerLayouts}
              value={settings.bookerLayout}
              onValueChange={(bookerLayout) =>
                updateSettings((nextSettings) => ({ ...nextSettings, bookerLayout: bookerLayout ?? "month" }))
              }
            >
              <SelectTrigger>
                <SelectValue>
                  {(bookerLayout: string) => bookerLayouts.find((item) => item.value === bookerLayout)?.label}
                </SelectValue>
              </SelectTrigger>
              <SelectContent>
                {bookerLayouts.map((bookerLayout) => (
                  <SelectItem key={bookerLayout.value} value={bookerLayout.value}>
                    {bookerLayout.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </SelectField>
            <TextField
              name="eventColor"
              label={t`Event color`}
              type="color"
              value={settings.eventColor ?? "#2563eb"}
              onChange={(eventColor) =>
                updateSettings((nextSettings) => ({
                  ...nextSettings,
                  eventColor
                }))
              }
            />
          </div>
        </EventTypeTabSection>
        <EventTypeTabSection
          title={<Trans>Locations</Trans>}
          description={<Trans>Set the primary location and optional alternatives shown to clients.</Trans>}
        >
          <div className="grid gap-4 md:grid-cols-2">
            <LocationTypeSelect
              value={value.locationType ?? ""}
              onChange={(locationType) => updatePrimaryLocation(locationType, value.locationValue ?? null)}
            />
            <TextField
              name="locationValue"
              label={t`Primary location`}
              value={value.locationValue ?? ""}
              onChange={(locationValue) => updatePrimaryLocation(value.locationType ?? "link", locationValue || null)}
            />
          </div>
          <TextAreaField
            name="locations"
            label={t`Location list`}
            lines={3}
            value={formatLocations(settings.locations)}
            onChange={(locationsText) => {
              const locations = parseLocations(locationsText);
              const primaryLocation = locations[0];
              onChange({
                ...updateEventTypeSettings(value, (nextSettings) => ({ ...nextSettings, locations })),
                locationType: primaryLocation?.type ?? value.locationType,
                locationValue: primaryLocation?.value ?? value.locationValue
              });
            }}
          />
        </EventTypeTabSection>
      </div>
    </FormValidationContext.Provider>
  );
}

export function EventTypeImageUpload({
  eventTypeId,
  imageUrl
}: Readonly<{ eventTypeId: string; imageUrl: string | null }>) {
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [previewUrl, setPreviewUrl] = useState<string | null>(null);
  const displayedImageUrl = previewUrl ?? imageUrl;

  useEffect(() => {
    return () => {
      if (previewUrl) URL.revokeObjectURL(previewUrl);
    };
  }, [previewUrl]);

  const uploadImageMutation = useMutation<void, Schemas["HttpValidationProblemDetails"], File>({
    mutationFn: async (file) => {
      const formData = new FormData();
      formData.append("file", file, "service.jpg");
      await apiClient.POST("/api/event-types/{id}/image", {
        params: { path: { id: eventTypeId } },
        body: formData,
        bodySerializer: (value: unknown) => value as FormData
      } as never);
    },
    onSuccess: async () => {
      toast.success(t`Image updated`);
      await queryClient.invalidateQueries();
    },
    onError: () => {
      setPreviewUrl(null);
      toast.error(t`Failed to update image`);
    }
  });

  const removeImageMutation = useMutation<void, Schemas["HttpValidationProblemDetails"]>({
    mutationFn: async () => {
      await apiClient.DELETE("/api/event-types/{id}/image", {
        params: { path: { id: eventTypeId } }
      } as never);
    },
    onSuccess: async () => {
      setPreviewUrl(null);
      toast.success(t`Image removed`);
      await queryClient.invalidateQueries();
    },
    onError: () => toast.error(t`Failed to remove image`)
  });

  const isPending = uploadImageMutation.isPending || removeImageMutation.isPending;

  const handleFileSelect = async (files: FileList | null) => {
    const file = files?.[0];
    if (!file) return;

    if (!ALLOWED_IMAGE_TYPES.includes(file.type)) {
      toast.error(t`Please select a JPEG, PNG, or WebP image.`);
      return;
    }

    try {
      const resizedFile = await resizeServiceImage(file);
      if (resizedFile.size > MAX_UPLOAD_BYTES) {
        toast.error(t`Image must be smaller than 2 MB.`);
        return;
      }

      setPreviewUrl((currentPreviewUrl) => {
        if (currentPreviewUrl) URL.revokeObjectURL(currentPreviewUrl);
        return URL.createObjectURL(resizedFile);
      });
      uploadImageMutation.mutate(resizedFile);
    } catch {
      toast.error(t`Failed to process image`);
    }
  };

  return (
    <div className="grid gap-3 rounded-lg border bg-card p-4 sm:grid-cols-[9rem_1fr] sm:items-center">
      <div className="flex aspect-video items-center justify-center overflow-hidden rounded-md border bg-muted">
        {displayedImageUrl ? (
          <img src={displayedImageUrl} alt={t`Service image`} className="size-full object-cover" />
        ) : (
          <ImageIcon className="size-8 text-muted-foreground" aria-hidden={true} />
        )}
      </div>
      <div className="flex flex-col gap-3">
        <div>
          <h3 className="text-sm font-medium">
            <Trans>Photo</Trans>
          </h3>
          <p className="text-sm text-muted-foreground">
            <Trans>Shown on the public booking page. Images are resized before upload.</Trans>
          </p>
        </div>
        <input
          ref={fileInputRef}
          type="file"
          accept={ALLOWED_IMAGE_TYPES.join(",")}
          className="hidden"
          onChange={(event) => {
            void handleFileSelect(event.currentTarget.files);
            event.currentTarget.value = "";
          }}
        />
        <div className="flex flex-wrap gap-2">
          <Button
            type="button"
            variant="outline"
            isPending={uploadImageMutation.isPending}
            onClick={() => fileInputRef.current?.click()}
          >
            <UploadIcon />
            {displayedImageUrl ? <Trans>Change photo</Trans> : <Trans>Upload photo</Trans>}
          </Button>
          {displayedImageUrl && (
            <Button
              type="button"
              variant="outline"
              isPending={removeImageMutation.isPending}
              disabled={isPending}
              onClick={() => removeImageMutation.mutate()}
            >
              <Trash2Icon />
              <Trans>Remove</Trans>
            </Button>
          )}
        </div>
      </div>
    </div>
  );
}

async function resizeServiceImage(file: File) {
  const imageUrl = URL.createObjectURL(file);
  try {
    const image = await loadImage(imageUrl);
    const longestSide = Math.max(image.naturalWidth, image.naturalHeight);
    const scale = Math.min(1, MAX_IMAGE_SIDE / longestSide);
    const width = Math.max(1, Math.round(image.naturalWidth * scale));
    const height = Math.max(1, Math.round(image.naturalHeight * scale));
    const canvas = document.createElement("canvas");
    canvas.width = width;
    canvas.height = height;
    const context = canvas.getContext("2d");
    if (!context) throw new Error("Canvas is not supported.");

    context.fillStyle = "#fff";
    context.fillRect(0, 0, width, height);
    context.drawImage(image, 0, 0, width, height);

    const blob = await new Promise<Blob>((resolve, reject) => {
      canvas.toBlob(
        (nextBlob) => {
          if (!nextBlob) {
            reject(new Error("Image export failed."));
            return;
          }
          resolve(nextBlob);
        },
        "image/jpeg",
        JPEG_QUALITY
      );
    });
    return new File([blob], "service.jpg", { type: "image/jpeg" });
  } finally {
    URL.revokeObjectURL(imageUrl);
  }
}

async function loadImage(imageUrl: string) {
  const image = new Image();
  image.src = imageUrl;
  await image.decode();
  return image;
}

function ensureDurationOption(options: number[], durationMinutes: number) {
  return [...new Set([durationMinutes, ...options])].sort((left, right) => left - right);
}

function parseDurationOptions(value: string, durationMinutes: number) {
  const options = value
    .split(",")
    .map((option) => Number(option.trim()))
    .filter((option) => Number.isInteger(option) && option >= 5 && option <= 1440);

  return ensureDurationOption(options.length > 0 ? options : [durationMinutes], durationMinutes);
}

function formatDurationOptions(options: number[]) {
  return options.join(", ");
}

function replacePrimaryLocation(
  locations: Array<{ type: string; value: string | null; displayLocationPubliclyToTeam: boolean }>,
  type: string,
  value: string | null
) {
  const primaryLocation = { type, value: value?.trim() || null, displayLocationPubliclyToTeam: false };
  return locations.length === 0 ? [primaryLocation] : [primaryLocation, ...locations.slice(1)];
}

function formatLocations(locations: Array<{ type: string; value: string | null }>) {
  return locations.map((location) => `${location.type}${location.value ? `: ${location.value}` : ""}`).join("\n");
}

function parseLocations(value: string) {
  return value
    .split("\n")
    .map((line) => {
      const [type, ...rest] = line.split(":");
      return { type: type.trim(), value: rest.join(":").trim() || null, displayLocationPubliclyToTeam: false };
    })
    .filter((location) => location.type.length > 0);
}
