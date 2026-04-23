import { Trans } from "@lingui/react/macro";
import { UploadIcon } from "lucide-react";
import { type Accept, type DropzoneOptions, type FileRejection, useDropzone } from "react-dropzone";

import { cn } from "../utils";

export { useDropzone };
export type { Accept, FileRejection };

interface DropzoneProps extends Omit<React.HTMLAttributes<HTMLDivElement>, "onDrop"> {
  onDrop: (acceptedFiles: File[], fileRejections: FileRejection[]) => void;
  accept?: Accept;
  maxSize?: number;
  minSize?: number;
  maxFiles?: number;
  multiple?: boolean;
  disabled?: boolean;
  noClick?: DropzoneOptions["noClick"];
  noKeyboard?: DropzoneOptions["noKeyboard"];
  noDragEventsBubbling?: DropzoneOptions["noDragEventsBubbling"];
}

export function Dropzone({
  onDrop,
  accept,
  maxSize,
  minSize,
  maxFiles,
  multiple,
  disabled,
  noClick,
  noKeyboard,
  noDragEventsBubbling,
  className,
  children,
  ...props
}: DropzoneProps) {
  const { getRootProps, getInputProps, isDragActive, isDragReject } = useDropzone({
    onDrop,
    accept,
    maxSize,
    minSize,
    maxFiles,
    multiple,
    disabled,
    noClick,
    noKeyboard,
    noDragEventsBubbling
  });

  return (
    <div
      {...getRootProps({
        ...props,
        "data-slot": "dropzone",
        "data-drag-active": isDragActive || undefined,
        "data-drag-reject": isDragReject || undefined,
        "data-disabled": disabled || undefined,
        className: cn(
          "flex cursor-pointer flex-col items-center justify-center gap-2 rounded-md bg-card p-6 text-center outline outline-2 -outline-offset-2 outline-transparent transition-colors outline-dashed hover:bg-accent/50 focus-visible:outline-offset-2 focus-visible:outline-ring active:bg-accent",
          "data-[drag-active]:outline-ring",
          "data-[drag-reject]:outline-destructive",
          "data-[disabled]:pointer-events-none data-[disabled]:opacity-50",
          className
        )
      })}
    >
      <input {...getInputProps()} />
      {children ?? <DropzoneDefaultContent />}
    </div>
  );
}

function DropzoneDefaultContent() {
  return (
    <>
      <UploadIcon className="size-8 text-muted-foreground" />
      <div className="text-sm font-medium">
        <Trans>Drop files here, or click to browse</Trans>
      </div>
    </>
  );
}
