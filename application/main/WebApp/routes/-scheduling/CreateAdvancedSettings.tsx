import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from "@repo/ui/components/Collapsible";
import { TextField } from "@repo/ui/components/TextField";
import { ChevronDownIcon } from "lucide-react";
import { useState } from "react";

import { slugify, type EventTypePayload } from "./schedulingTypes";

export function CreateAdvancedSettings({
  draft,
  onSlugEdited,
  onChange
}: Readonly<{
  draft: EventTypePayload;
  slugWasEdited: boolean;
  onSlugEdited: (value: boolean) => void;
  onChange: (value: EventTypePayload) => void;
}>) {
  const [expanded, setExpanded] = useState(false);

  return (
    <Collapsible open={expanded} onOpenChange={setExpanded}>
      <div className="rounded-lg border">
        <CollapsibleTrigger
          render={
            <Button
              type="button"
              variant="ghost"
              className="flex h-auto w-full items-start justify-between gap-4 p-3 text-left hover:bg-muted/60"
            >
              <span className="grid gap-1">
                <span className="font-semibold">
                  <Trans>Advanced settings</Trans>
                </span>
                <span className="text-sm font-normal text-muted-foreground">
                  <Trans>Change the public link name if you need to.</Trans>
                </span>
              </span>
              <ChevronDownIcon
                className={`mt-1 size-4 shrink-0 transition-transform ${expanded ? "rotate-180" : ""}`}
              />
            </Button>
          }
        />
        <CollapsibleContent>
          <div className="border-t p-3">
            <TextField
              name="slug"
              label={t`Link name`}
              description={t`This becomes the last part of the public booking link.`}
              required={true}
              value={draft.slug}
              onChange={(slug) => {
                onSlugEdited(true);
                onChange({ ...draft, slug: slugify(slug) });
              }}
            />
          </div>
        </CollapsibleContent>
      </div>
    </Collapsible>
  );
}
