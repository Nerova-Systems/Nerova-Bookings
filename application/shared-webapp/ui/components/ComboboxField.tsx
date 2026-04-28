import type { Combobox as ComboboxPrimitive } from "@base-ui/react/combobox";
import type * as React from "react";

import { Trans } from "@lingui/react/macro";
import { PlusIcon } from "lucide-react";
import { useRef, useState } from "react";

import { useFieldError } from "../hooks/useFieldError";
import { cn } from "../utils";
import { Combobox, ComboboxContent, ComboboxInput, ComboboxItem, ComboboxList } from "./Combobox";
import { Field, FieldDescription, FieldError, FieldLabel } from "./Field";
import { LabelWithTooltip } from "./LabelWithTooltip";

export interface ComboboxFieldItem {
  id: string;
  label: string;
  icon?: React.ReactNode;
}

export interface ComboboxFieldProps {
  name?: string;
  label?: string;
  description?: string;
  errorMessage?: string;
  tooltip?: React.ReactNode;
  placeholder?: string;
  emptyMessage?: React.ReactNode;
  items: ComboboxFieldItem[];
  value?: string | null;
  onValueChange?: (value: string | null) => void;
  className?: string;
  disabled?: boolean;
  readOnly?: boolean;
  /** Allow typing custom values not in the items list */
  allowCustomValue?: boolean;
  /** Show a "Create" option when typed text doesn't match any item */
  allowCreate?: boolean;
  /** Called when user creates a new item via the Create option */
  onCreateItem?: (label: string) => void;
  /** Icon shown when no item is selected or selected item has no icon */
  startIcon?: React.ReactNode;
}

function filterItems(items: ComboboxFieldItem[], search: string) {
  if (!search) return items;
  const lower = search.toLowerCase();
  return items.filter((item) => item.label.toLowerCase().includes(lower));
}

export function ComboboxField({
  name,
  label,
  description,
  errorMessage,
  tooltip,
  placeholder,
  emptyMessage,
  items,
  value,
  onValueChange,
  className,
  disabled,
  readOnly,
  allowCustomValue,
  allowCreate,
  onCreateItem,
  startIcon: startIconProp
}: Readonly<ComboboxFieldProps>) {
  const { errors, isInvalid, clearNow } = useFieldError({ name, errorMessage });

  const fieldRef = useRef<HTMLDivElement>(null);
  const [open, setOpen] = useState(false);
  const suppressOpenRef = useRef(false);
  const [search, setSearch] = useState("");
  const filtered = filterItems(items, search);
  const selectedIcon = items.find((item) => item.id === value)?.icon;
  const isKnownItem = items.some((item) => item.id === value);
  const hasExactMatch = items.some((item) => item.label.toLowerCase() === search.toLowerCase());

  const focusTrigger = () => {
    const input = name
      ? (document.getElementById(name) as HTMLElement | null)
      : (fieldRef.current?.querySelector("input") as HTMLElement | null);
    if (!input) return;
    suppressOpenRef.current = true;
    input.focus({ preventScroll: true });
    requestAnimationFrame(() => {
      suppressOpenRef.current = false;
    });
  };

  const handleOpenChange = (nextOpen: boolean) => {
    if (suppressOpenRef.current && nextOpen) return;
    setOpen(nextOpen);
  };

  const handleInputValueChange = (text: string) => {
    setSearch(text);
    const exactMatch = items.find((item) => item.label.toLowerCase() === text.toLowerCase());
    if (exactMatch) {
      clearNow();
      onValueChange?.(exactMatch.id);
    } else if (text && (allowCustomValue || allowCreate)) {
      onValueChange?.(text);
    }
  };

  const handleCreateItem = () => {
    onCreateItem?.(search);
    clearNow();
    onValueChange?.(search);
  };

  return (
    <Field ref={fieldRef} className={cn("flex flex-col", className)}>
      {label && (
        <FieldLabel
          onClick={focusTrigger}
          onKeyDown={(event) => {
            if (event.key === "Enter" || event.key === " ") {
              event.preventDefault();
              focusTrigger();
            }
          }}
        >
          {tooltip ? <LabelWithTooltip tooltip={tooltip}>{label}</LabelWithTooltip> : label}
        </FieldLabel>
      )}
      <Combobox
        disabled={disabled}
        open={readOnly ? false : open}
        onOpenChange={handleOpenChange}
        value={value ?? null}
        onValueChange={(v) => {
          clearNow();
          onValueChange?.(v);
        }}
        onInputValueChange={handleInputValueChange}
        itemToStringLabel={(itemValue: ComboboxPrimitive.Item.Props["value"]) =>
          items.find((item) => item.id === itemValue)?.label ?? String(itemValue ?? "")
        }
      >
        <ComboboxInput
          id={name}
          placeholder={placeholder}
          disabled={disabled}
          readOnly={readOnly}
          startIcon={selectedIcon ?? startIconProp}
          startIconClassName={value ? "text-foreground" : undefined}
          aria-invalid={isInvalid || undefined}
        />
        <ComboboxContent>
          <ComboboxList>
            {filtered.length === 0 && !allowCreate && (
              <div className="flex w-full justify-center py-2 text-center text-sm text-muted-foreground">
                {emptyMessage ?? <Trans>No results found</Trans>}
              </div>
            )}
            {filtered.map((item) => (
              <ComboboxItem key={item.id} value={item.id}>
                {item.icon}
                {item.label}
              </ComboboxItem>
            ))}
            {allowCustomValue && value && !isKnownItem && (
              <ComboboxItem key="__custom" value={value} className="hidden">
                {value}
              </ComboboxItem>
            )}
            {allowCreate && search && !hasExactMatch && (
              <ComboboxItem
                value={search}
                onClick={handleCreateItem}
                className="font-medium whitespace-nowrap text-primary"
              >
                <PlusIcon />
                <Trans>
                  Create <span className="font-bold">{search}</span>
                </Trans>
              </ComboboxItem>
            )}
          </ComboboxList>
        </ComboboxContent>
      </Combobox>
      {description && <FieldDescription>{description}</FieldDescription>}
      <FieldError errors={errors} />
    </Field>
  );
}
