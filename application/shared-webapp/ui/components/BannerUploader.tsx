import { Trans, useLingui } from "@lingui/react/macro";
import { UploadIcon } from "lucide-react";
import * as React from "react";
import { useRef, useState } from "react";

import { cn } from "../utils";
import { Button } from "./Button";
import { Dialog, DialogContent, DialogHeader, DialogTitle } from "./Dialog";
import { ImageUploader } from "./ImageUploader";

/**
 * Banner image uploader: file picker + crop dialog optimised for wide banner images (aspect ~3:1).
 * Ported from cal.com `packages/ui/components/banner-uploader/BannerUploader.tsx` (cf2a55c).
 *
 * No prop deviations.
 */
interface BannerUploaderProps {
  /** Current banner URL (used as preview). */
  value?: string;
  onChange?: (dataUrl: string) => void;
  onRemove?: () => void;
  /** Aspect ratio for the crop area. @default 3 */
  aspect?: number;
  disabled?: boolean;
  className?: string;
}

export function BannerUploader({ value, onChange, onRemove, aspect = 3, disabled, className }: BannerUploaderProps) {
  const { t } = useLingui();
  const fileRef = useRef<HTMLInputElement>(null);
  const [pendingSrc, setPendingSrc] = useState<string | null>(null);

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    const reader = new FileReader();
    reader.onload = (ev) => setPendingSrc(ev.target?.result as string);
    reader.readAsDataURL(file);
    // Reset so same file can be re-selected
    e.target.value = "";
  };

  return (
    <div data-slot="banner-uploader" className={cn("flex flex-col gap-3", className)}>
      {/* Banner preview */}
      {value ? (
        <div className="relative w-full overflow-hidden rounded-lg border border-border">
          {/* eslint-disable-next-line @next/next/no-img-element */}
          <img src={value} alt={t`Banner preview`} className="h-24 w-full object-cover" />
        </div>
      ) : (
        <div className="flex h-24 w-full items-center justify-center rounded-lg border border-dashed border-border bg-muted text-muted-foreground">
          <UploadIcon className="size-8" aria-hidden />
        </div>
      )}

      {/* Actions */}
      <div className="flex items-center gap-2">
        <input
          ref={fileRef}
          type="file"
          accept="image/*"
          className="sr-only"
          disabled={disabled}
          aria-label={t`Upload banner image`}
          onChange={handleFileChange}
        />
        <Button type="button" variant="outline" size="sm" disabled={disabled} onClick={() => fileRef.current?.click()}>
          <UploadIcon className="size-4" />
          <Trans>Upload</Trans>
        </Button>
        {value && onRemove && (
          <Button type="button" variant="ghost" size="sm" disabled={disabled} onClick={onRemove}>
            <Trans>Remove</Trans>
          </Button>
        )}
      </div>

      {/* Crop dialog */}
      <Dialog
        trackingTitle="Crop Banner"
        open={!!pendingSrc}
        onOpenChange={(open) => {
          if (!open) setPendingSrc(null);
        }}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>
              <Trans>Crop banner</Trans>
            </DialogTitle>
          </DialogHeader>
          {pendingSrc && (
            <ImageUploader
              src={pendingSrc}
              aspect={aspect}
              cropShape="rect"
              onCropComplete={(dataUrl) => {
                onChange?.(dataUrl);
                setPendingSrc(null);
              }}
              onClose={() => setPendingSrc(null)}
            />
          )}
        </DialogContent>
      </Dialog>
    </div>
  );
}
