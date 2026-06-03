import { t } from "@lingui/core/macro";
import { trackInteraction } from "@repo/infrastructure/applicationInsights/ApplicationInsightsProvider";
import { DateRangePicker } from "@repo/ui/components/DateRangePicker";
import { Field, FieldLabel } from "@repo/ui/components/Field";
import { InputGroup, InputGroupAddon, InputGroupButton, InputGroupInput } from "@repo/ui/components/InputGroup";
import { format } from "date-fns";
import { SearchIcon, XIcon } from "lucide-react";

import { useClientFilters } from "./useClientFilters";

interface ClientQueryingProps {
  onFiltersUpdated?: () => void;
}

export function ClientQuerying({ onFiltersUpdated }: ClientQueryingProps = {}) {
  const filters = useClientFilters({ onFiltersUpdated });

  return (
    <div className="flex items-center gap-2">
      <Field className="max-w-60 min-w-32 flex-1">
        <FieldLabel>{t`Search`}</FieldLabel>
        <InputGroup>
          <InputGroupAddon>
            <SearchIcon />
          </InputGroupAddon>
          <InputGroupInput
            type="text"
            role="searchbox"
            placeholder={t`Search`}
            value={filters.search}
            onChange={(e) => filters.setSearch(e.target.value)}
            onKeyDown={(e) => e.key === "Escape" && filters.search && filters.setSearch("")}
          />
          {filters.search && (
            <InputGroupAddon align="inline-end">
              <InputGroupButton onClick={() => filters.setSearch("")} size="icon-xs" aria-label={t`Clear search`}>
                <XIcon />
              </InputGroupButton>
            </InputGroupAddon>
          )}
        </InputGroup>
      </Field>

      <DateRangePicker
        value={filters.dateRange}
        onChange={(range) => {
          trackInteraction("Client filters", "interaction", "Date filter");
          filters.updateFilter({
            startDate: range ? format(range.start, "yyyy-MM-dd") : undefined,
            endDate: range ? format(range.end, "yyyy-MM-dd") : undefined
          });
        }}
        label={t`First visit`}
        placeholder={t`Select dates`}
      />
    </div>
  );
}
