import { Trans, useLingui } from "@lingui/react/macro";
import { useCallback, useState } from "react";
import Cropper, { type Area } from "react-easy-crop";

import { cn } from "../utils";
import { Button } from "./Button";
import { Slider } from "./Slider";

/**
 * Image uploader with crop, rotate, and zoom using `react-easy-crop`.
 * Ported from cal.com `packages/ui/components/image-uploader/ImageUploader.tsx` (cf2a55c).
 *
 * Exports both the cropper UI and a utility to create a cropped image blob.
 * The `onChange` callback receives a `string` (base64 data URL) of the cropped result.
 */
interface ImageUploaderProps {
  /** The image source URL or data URL to crop. */
  src: string;
  /** Called with a base64 data URL of the cropped image. */
  onCropComplete?: (croppedImage: string) => void;
  onClose?: () => void;
  aspect?: number;
  cropShape?: "rect" | "round";
  className?: string;
}

/** Utility: given an image URL and crop area pixels, return a base64 data URL. */
export async function getCroppedImg(imageSrc: string, pixelCrop: Area): Promise<string> {
  const img = await createImage(imageSrc);
  const canvas = document.createElement("canvas");
  const ctx = canvas.getContext("2d");

  if (!ctx) throw new Error("Cannot get canvas context");

  canvas.width = pixelCrop.width;
  canvas.height = pixelCrop.height;

  ctx.drawImage(
    img,
    pixelCrop.x,
    pixelCrop.y,
    pixelCrop.width,
    pixelCrop.height,
    0,
    0,
    pixelCrop.width,
    pixelCrop.height
  );

  return canvas.toDataURL("image/jpeg");
}

function createImage(url: string): Promise<HTMLImageElement> {
  return new Promise((resolve, reject) => {
    const img = new Image();
    img.addEventListener("load", () => resolve(img));
    img.addEventListener("error", reject);
    img.setAttribute("crossOrigin", "anonymous");
    img.src = url;
  });
}

export function ImageUploader({
  src,
  onCropComplete,
  onClose,
  aspect = 1,
  cropShape = "rect",
  className
}: ImageUploaderProps) {
  const { t } = useLingui();
  const [crop, setCrop] = useState({ x: 0, y: 0 });
  const [zoom, setZoom] = useState(1);
  const [croppedAreaPixels, setCroppedAreaPixels] = useState<Area | null>(null);

  const handleCropComplete = useCallback((_: Area, areaPixels: Area) => {
    setCroppedAreaPixels(areaPixels);
  }, []);

  const handleSave = useCallback(async () => {
    if (!croppedAreaPixels) return;
    try {
      const result = await getCroppedImg(src, croppedAreaPixels);
      onCropComplete?.(result);
    } catch (err) {
      console.error("Failed to crop image", err);
    }
  }, [src, croppedAreaPixels, onCropComplete]);

  return (
    <div data-slot="image-uploader" className={cn("flex flex-col gap-4", className)}>
      <div className="relative h-64 w-full overflow-hidden rounded-lg bg-muted">
        <Cropper
          image={src}
          crop={crop}
          zoom={zoom}
          aspect={aspect}
          cropShape={cropShape}
          onCropChange={setCrop}
          onZoomChange={setZoom}
          onCropComplete={handleCropComplete}
        />
      </div>

      <div className="flex flex-col gap-2">
        <label htmlFor="image-uploader-zoom" className="text-sm font-medium text-foreground">
          <Trans>Zoom</Trans>
        </label>
        <Slider
          id="image-uploader-zoom"
          min={1}
          max={3}
          step={0.05}
          value={[zoom]}
          onValueChange={(vals) => setZoom((Array.isArray(vals) ? vals[0] : vals) ?? 1)}
          aria-label={t`Zoom image`}
        />
      </div>

      <div className="flex items-center justify-end gap-2">
        {onClose && (
          <Button variant="outline" onClick={onClose}>
            <Trans>Cancel</Trans>
          </Button>
        )}
        <Button onClick={handleSave}>
          <Trans>Save</Trans>
        </Button>
      </div>
    </div>
  );
}
