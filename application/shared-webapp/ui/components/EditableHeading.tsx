import { useLingui } from "@lingui/react/macro";
import { PencilIcon } from "lucide-react";
import { useEffect, useRef, useState } from "react";

import { cn } from "../utils";
import { Button } from "./Button";

/**
 * Inline-editable heading. Click the pencil icon (or the text itself when in edit mode)
 * to toggle an edit field; press Enter or blur to confirm, Escape to cancel.
 * Ported from cal.com `packages/ui/components/editable-heading/EditableHeading.tsx` (cf2a55c).
 *
 * No prop deviations.
 */
interface EditableHeadingProps {
  value: string;
  onChange?: (value: string) => void;
  disabled?: boolean;
  /** HTML heading tag. @default "h1" */
  as?: "h1" | "h2" | "h3" | "h4";
  placeholder?: string;
  className?: string;
  inputClassName?: string;
}

export function EditableHeading({
  value,
  onChange,
  disabled,
  as: Tag = "h1",
  placeholder,
  className,
  inputClassName
}: EditableHeadingProps) {
  const { t } = useLingui();
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState(value);
  const inputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    setDraft(value);
  }, [value]);

  useEffect(() => {
    if (editing) {
      inputRef.current?.focus();
      inputRef.current?.select();
    }
  }, [editing]);

  const commit = () => {
    const trimmed = draft.trim();
    if (trimmed && trimmed !== value) {
      onChange?.(trimmed);
    } else {
      setDraft(value); // reset if empty or unchanged
    }
    setEditing(false);
  };

  const cancel = () => {
    setDraft(value);
    setEditing(false);
  };

  if (disabled) {
    return (
      <Tag data-slot="editable-heading" className={cn("font-bold", className)}>
        {value}
      </Tag>
    );
  }

  return (
    <div data-slot="editable-heading" className={cn("group/editable-heading flex items-center gap-2", className)}>
      {editing ? (
        <input
          ref={inputRef}
          type="text"
          value={draft}
          placeholder={placeholder}
          onChange={(e) => setDraft(e.target.value)}
          onBlur={commit}
          onKeyDown={(e) => {
            if (e.key === "Enter") {
              e.preventDefault();
              commit();
            } else if (e.key === "Escape") {
              e.preventDefault();
              cancel();
            }
          }}
          className={cn(
            "border-b border-input bg-transparent text-foreground outline-none",
            "font-[inherit] leading-[inherit] text-[inherit]",
            inputClassName
          )}
          aria-label={t`Edit name`}
        />
      ) : (
        <>
          <Tag className="cursor-pointer font-bold" onClick={() => setEditing(true)}>
            {value}
          </Tag>
          <Button
            variant="ghost"
            size="icon-xs"
            className="opacity-0 transition-opacity group-hover/editable-heading:opacity-100"
            onClick={() => setEditing(true)}
            aria-label={t`Edit name`}
          >
            <PencilIcon />
          </Button>
        </>
      )}
    </div>
  );
}
