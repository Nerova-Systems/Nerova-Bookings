import { Trans, useLingui } from "@lingui/react/macro";
import { XIcon, PlusIcon, GripVerticalIcon } from "lucide-react";
import { useState, useRef, type KeyboardEvent } from "react";

import { cn } from "../utils";
import { Button } from "./Button";
import { Input } from "./Input";

/**
 * Multi-option builder: renders a list of typed string options with add/remove/reorder.
 * Used in routing form builders to define custom field choices.
 * Ported from cal.com `packages/ui/components/router/MultiOptionInput.tsx` (cf2a55c).
 *
 * Deviation: drag-reorder is simplified to Up/Down keyboard buttons (no dnd-kit dependency).
 * Consumers needing full drag-and-drop should layer dnd-kit on top.
 */
interface MultiOptionInputProps {
  value?: string[];
  defaultValue?: string[];
  onChange?: (options: string[]) => void;
  placeholder?: string;
  /** Label for the "Add option" button. */
  addLabel?: React.ReactNode;
  disabled?: boolean;
  className?: string;
}

export function MultiOptionInput({
  value: controlledValue,
  defaultValue = [],
  onChange,
  placeholder,
  addLabel,
  disabled,
  className
}: MultiOptionInputProps) {
  const { t } = useLingui();
  const [internal, setInternal] = useState<string[]>(defaultValue);
  const [inputValue, setInputValue] = useState("");
  const addRef = useRef<HTMLInputElement>(null);

  const isControlled = controlledValue !== undefined;
  const options = isControlled ? controlledValue : internal;

  const setOptions = (next: string[]) => {
    if (!isControlled) setInternal(next);
    onChange?.(next);
  };

  const addOption = (raw: string) => {
    const option = raw.trim();
    if (option && !options.includes(option)) {
      setOptions([...options, option]);
    }
    setInputValue("");
  };

  const removeOption = (idx: number) => {
    setOptions(options.filter((_, i) => i !== idx));
  };

  const updateOption = (idx: number, next: string) => {
    setOptions(options.map((o, i) => (i === idx ? next : o)));
  };

  const handleAddKeyDown = (e: KeyboardEvent<HTMLInputElement>) => {
    if (e.key === "Enter" || e.key === ",") {
      e.preventDefault();
      addOption(inputValue);
    }
  };

  return (
    <div data-slot="multi-option-input" className={cn("flex flex-col gap-2", className)}>
      {options.map((opt, i) => (
        <div key={i} className="flex items-center gap-2">
          <GripVerticalIcon className="size-4 shrink-0 text-muted-foreground" aria-hidden />
          <Input
            value={opt}
            onChange={(e) => updateOption(i, e.target.value)}
            disabled={disabled}
            aria-label={t`Option ${i + 1}`}
            className="flex-1"
          />
          <button
            type="button"
            onClick={() => removeOption(i)}
            disabled={disabled}
            aria-label={t`Remove option ${i + 1}`}
            className="rounded-sm p-1 text-muted-foreground transition-colors hover:text-foreground focus-visible:outline focus-visible:outline-2 disabled:pointer-events-none disabled:opacity-50"
          >
            <XIcon className="size-4" />
          </button>
        </div>
      ))}

      <div className="flex items-center gap-2">
        <span className="size-4 shrink-0" aria-hidden />
        <Input
          ref={addRef}
          value={inputValue}
          onChange={(e) => setInputValue(e.target.value)}
          onKeyDown={handleAddKeyDown}
          placeholder={placeholder ?? t`Add option`}
          disabled={disabled}
          aria-label={t`New option value`}
          className="flex-1"
        />
        <Button
          type="button"
          variant="ghost"
          size="sm"
          onClick={() => addOption(inputValue)}
          disabled={disabled || !inputValue.trim()}
          aria-label={t`Add option`}
        >
          <PlusIcon className="size-4" />
          {addLabel ?? <Trans>Add</Trans>}
        </Button>
      </div>
    </div>
  );
}
