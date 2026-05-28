import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Label } from "@repo/ui/components/Label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@repo/ui/components/Select";
import { Switch } from "@repo/ui/components/Switch";

import type { BookingFilterSearch } from "./BookingsFilters";

function ToggleFilter({
  id,
  label,
  description,
  checked,
  onCheckedChange
}: Readonly<{
  id: string;
  label: string;
  description: string;
  checked: boolean;
  onCheckedChange: (checked: boolean) => void;
}>) {
  return (
    <div className="flex items-start justify-between gap-4">
      <div className="flex flex-col gap-1">
        <Label htmlFor={id}>{label}</Label>
        <span className="text-sm text-muted-foreground">{description}</span>
      </div>
      <Switch id={id} checked={checked} onCheckedChange={onCheckedChange} aria-label={label} />
    </div>
  );
}

export function FacetedFilters({
  draftSearch,
  updateSearch
}: Readonly<{
  draftSearch: BookingFilterSearch;
  updateSearch: (next: Partial<BookingFilterSearch>) => void;
}>) {
  return (
    <>
      <ToggleFilter
        id="booking-no-show-only"
        label={t`No-show only`}
        description={t`Only show bookings where the attendee did not join.`}
        checked={draftSearch.noShowOnly ?? false}
        onCheckedChange={(checked) => updateSearch({ noShowOnly: checked || undefined })}
      />
      <ToggleFilter
        id="booking-has-internal-note"
        label={t`Has internal note`}
        description={t`Only show bookings with at least one internal note.`}
        checked={draftSearch.hasInternalNote ?? false}
        onCheckedChange={(checked) => updateSearch({ hasInternalNote: checked || undefined })}
      />
      <div className="flex flex-col gap-2">
        <Label htmlFor="booking-min-rating">
          <Trans>Minimum rating</Trans>
        </Label>
        <Select
          value={draftSearch.minRating ? String(draftSearch.minRating) : "any"}
          onValueChange={(value) => {
            const next = value ?? "any";
            updateSearch({ minRating: next === "any" ? undefined : Number(next) });
          }}
        >
          <SelectTrigger id="booking-min-rating" className="w-full" aria-label={t`Minimum rating`}>
            <SelectValue>{(value: string) => (value === "any" ? t`Any rating` : t`${value} stars and up`)}</SelectValue>
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="any">
              <Trans>Any rating</Trans>
            </SelectItem>
            {[1, 2, 3, 4, 5].map((rating) => (
              <SelectItem key={rating} value={String(rating)}>
                <Trans>{rating} stars and up</Trans>
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>
    </>
  );
}
