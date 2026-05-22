import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { DateRangePicker } from "@repo/ui/components/DateRangePicker";
import { format } from "date-fns";
import { FilterXIcon } from "lucide-react";

import { getDefaultRange, pickerValue } from "./insightsDateRange";

interface InsightsFiltersProps {
  from: string;
  to: string;
  hasCustomRange: boolean;
  onChange: (next: { from: string; to: string }) => void;
  onReset: () => void;
}

export function InsightsFilters({ from, to, hasCustomRange, onChange, onReset }: Readonly<InsightsFiltersProps>) {
  return (
    <div className="flex flex-wrap items-end gap-2">
      <DateRangePicker
        value={pickerValue(from, to)}
        onChange={(range) => {
          if (range) {
            onChange({ from: format(range.start, "yyyy-MM-dd"), to: format(range.end, "yyyy-MM-dd") });
          } else {
            const defaults = getDefaultRange();
            onChange(defaults);
          }
        }}
        label={t`Date range`}
        placeholder={t`Select dates`}
      />
      {hasCustomRange && (
        <Button variant="secondary" onClick={onReset} aria-label={t`Reset filters`}>
          <FilterXIcon className="size-4" />
          <Trans>Reset</Trans>
        </Button>
      )}
    </div>
  );
}
