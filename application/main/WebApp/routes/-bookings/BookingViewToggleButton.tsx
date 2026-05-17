import { t } from "@lingui/core/macro";
import { ToggleGroup, ToggleGroupItem } from "@repo/ui/components/ToggleGroup";
import { Tooltip, TooltipContent, TooltipTrigger } from "@repo/ui/components/Tooltip";
import { CalendarIcon, ListIcon } from "lucide-react";

import type { BookingDashboardView } from "./bookingTypes";

export function BookingViewToggleButton({
  view,
  onViewChange
}: Readonly<{
  view: BookingDashboardView;
  onViewChange: (view: BookingDashboardView) => void;
}>) {
  return (
    <div className="hidden sm:block">
      <ToggleGroup
        value={[view]}
        onValueChange={(value) => {
          const nextView = value[0];
          if (nextView === "list" || nextView === "calendar") {
            onViewChange(nextView);
          }
        }}
        variant="outline"
        size="sm"
      >
        <Tooltip>
          <TooltipTrigger
            render={
              <ToggleGroupItem value="list" aria-label={t`List view`}>
                <ListIcon />
              </ToggleGroupItem>
            }
          />
          <TooltipContent>{t`List view`}</TooltipContent>
        </Tooltip>
        <Tooltip>
          <TooltipTrigger
            render={
              <ToggleGroupItem value="calendar" aria-label={t`Calendar view`}>
                <CalendarIcon />
              </ToggleGroupItem>
            }
          />
          <TooltipContent>{t`Calendar view`}</TooltipContent>
        </Tooltip>
      </ToggleGroup>
    </div>
  );
}
