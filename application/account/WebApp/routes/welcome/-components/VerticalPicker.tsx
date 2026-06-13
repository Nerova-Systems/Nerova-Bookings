import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Badge } from "@repo/ui/components/Badge";
import { Button } from "@repo/ui/components/Button";

import type { Schemas } from "@/shared/lib/api/client";

export type NerovaVerticalValue = "Salon" | "Barber" | "Nails" | "Trainer" | "Tutor" | "Vet" | "Clinic" | "Other";

const verticalOptions: NerovaVerticalValue[] = [
  "Salon",
  "Barber",
  "Nails",
  "Trainer",
  "Tutor",
  "Vet",
  "Clinic",
  "Other"
];
export function VerticalPicker({
  selectedVertical,
  isPending,
  onSelect
}: Readonly<{
  selectedVertical: NerovaVerticalValue | null;
  isPending: boolean;
  onSelect: (vertical: NerovaVerticalValue) => void;
}>) {
  return (
    <section className="flex w-full flex-col gap-3 rounded-xl border bg-card p-4">
      <div className="flex flex-col gap-1">
        <h3 className="text-sm font-medium">
          <Trans>What kind of business are you?</Trans>
        </h3>
        <p className="text-sm text-muted-foreground">
          <Trans>Optional for now. This helps Nerova choose the right words and defaults.</Trans>
        </p>
      </div>
      <div className="grid grid-cols-2 gap-2">
        {verticalOptions.map((vertical) => {
          const isSelected = selectedVertical === vertical;
          const isComingSoon = vertical === "Vet";

          return (
            <Button
              key={vertical}
              type="button"
              variant={isSelected ? "default" : "outline"}
              size="sm"
              disabled={isPending || isComingSoon}
              className="h-auto justify-between px-3 py-2"
              onClick={() => onSelect(vertical)}
            >
              <span>{verticalLabel(vertical)}</span>
              {isComingSoon ? (
                <Badge variant="secondary">
                  <Trans>Soon</Trans>
                </Badge>
              ) : null}
            </Button>
          );
        })}
      </div>
    </section>
  );
}

function verticalLabel(vertical: NerovaVerticalValue) {
  switch (vertical) {
    case "Salon":
      return t`Salon`;
    case "Barber":
      return t`Barber`;
    case "Nails":
      return t`Nails`;
    case "Trainer":
      return t`Personal trainer`;
    case "Tutor":
      return t`Tutor`;
    case "Vet":
      return t`Vet`;
    case "Clinic":
      return t`Clinic`;
    case "Other":
      return t`Other`;
  }
}

export function toVerticalValue(vertical: Schemas["NerovaVertical"] | null | undefined): NerovaVerticalValue | null {
  return verticalOptions.includes(vertical as NerovaVerticalValue) ? (vertical as NerovaVerticalValue) : null;
}
