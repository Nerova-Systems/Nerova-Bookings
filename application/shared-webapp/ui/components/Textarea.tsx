import type * as React from "react";

import { cn } from "../utils";

interface TextareaProps extends React.ComponentProps<"textarea"> {
  lines?: number;
  resizable?: boolean;
}

function Textarea({ className, lines, resizable, rows, onKeyDown, onPaste, ...props }: TextareaProps) {
  const hasFixedLines = lines !== undefined && lines > 0;
  const isResizable = resizable ?? !hasFixedLines;

  const handleKeyDown = (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if (hasFixedLines && e.key === "Enter") {
      const currentLines = e.currentTarget.value.split("\n").length;
      if (currentLines >= lines) {
        e.preventDefault();
      }
    }
    onKeyDown?.(e);
  };

  const handlePaste = (e: React.ClipboardEvent<HTMLTextAreaElement>) => {
    if (hasFixedLines) {
      const textarea = e.currentTarget;
      const paste = e.clipboardData.getData("text");
      const before = textarea.value.slice(0, textarea.selectionStart);
      const after = textarea.value.slice(textarea.selectionEnd);
      const resultLines = (before + paste + after).split("\n");
      if (resultLines.length > lines) {
        e.preventDefault();
        const trimmed = resultLines.slice(0, lines).join("\n");
        // Use the native setter so React's synthetic onChange still fires.
        const setter = Object.getOwnPropertyDescriptor(window.HTMLTextAreaElement.prototype, "value")?.set;
        setter?.call(textarea, trimmed);
        textarea.dispatchEvent(new Event("input", { bubbles: true }));
      }
    }
    onPaste?.(e);
  };

  return (
    <textarea
      data-slot="textarea"
      rows={lines ?? rows}
      onKeyDown={handleKeyDown}
      onPaste={handlePaste}
      className={cn(
        hasFixedLines ? "" : "[field-sizing:content] min-h-16",
        isResizable ? "resize-y" : "resize-none",
        "w-full min-w-0 rounded-md border border-input bg-white px-2.5 pt-3 pb-2.5 text-sm shadow-xs outline-ring transition-[color,box-shadow] placeholder:text-muted-foreground focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 disabled:pointer-events-none disabled:cursor-not-allowed disabled:opacity-50 aria-invalid:outline aria-invalid:outline-2 aria-invalid:outline-offset-2 aria-invalid:outline-destructive aria-invalid:focus-visible:shadow-error-halo dark:bg-input/30",
        className
      )}
      {...props}
    />
  );
}

export { Textarea };
