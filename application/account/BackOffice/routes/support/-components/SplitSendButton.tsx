import { t } from "@lingui/core/macro";
import { Button } from "@repo/ui/components/Button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger
} from "@repo/ui/components/DropdownMenu";
import { CheckCheckIcon, CheckIcon, ChevronDownIcon, type LucideIcon, SendIcon } from "lucide-react";

export type SendAction = "send" | "sendAndResolve" | "resolve";

interface SendActionConfig {
  icon: LucideIcon;
  pending: () => string;
  idle: () => string;
  needsBody: boolean;
}

const SEND_ACTION_LABELS: Record<SendAction, SendActionConfig> = {
  send: { icon: SendIcon, pending: () => t`Sending…`, idle: () => t`Send`, needsBody: true },
  sendAndResolve: {
    icon: CheckCheckIcon,
    pending: () => t`Sending…`,
    idle: () => t`Send & resolve`,
    needsBody: true
  },
  resolve: { icon: CheckIcon, pending: () => t`Resolving…`, idle: () => t`Resolve`, needsBody: false }
};

interface SplitSendButtonProps {
  primaryAction: SendAction;
  hasBody: boolean;
  isPending: boolean;
  onAction: (action: SendAction) => void;
}

export function SplitSendButton({ primaryAction, hasBody, isPending, onAction }: Readonly<SplitSendButtonProps>) {
  const config = SEND_ACTION_LABELS[primaryAction];
  const Icon = config.icon;
  const disablePrimary = isPending || (config.needsBody && !hasBody);

  return (
    <div className="flex items-stretch">
      <Button
        type="button"
        size="sm"
        className="rounded-r-none"
        disabled={disablePrimary}
        isPending={isPending}
        onClick={() => onAction(primaryAction)}
      >
        <Icon className="size-3.5" />
        {isPending ? config.pending() : config.idle()}
      </Button>
      <DropdownMenu trackingTitle="Send actions">
        <DropdownMenuTrigger
          render={
            <Button
              type="button"
              size="sm"
              className="-ml-px rounded-l-none px-2"
              disabled={isPending}
              aria-label={t`More send options`}
            >
              <ChevronDownIcon className="size-3.5" />
            </Button>
          }
        />
        <DropdownMenuContent align="end">
          {(Object.entries(SEND_ACTION_LABELS) as [SendAction, SendActionConfig][]).map(([key, value]) => {
            const RowIcon = value.icon;
            return (
              <DropdownMenuItem
                key={key}
                onClick={() => onAction(key)}
                trackingLabel={key}
                disabled={value.needsBody && !hasBody}
              >
                <RowIcon className="size-4" />
                <span className="flex-1">{value.idle()}</span>
                {primaryAction === key && <CheckIcon className="size-3.5" />}
              </DropdownMenuItem>
            );
          })}
        </DropdownMenuContent>
      </DropdownMenu>
    </div>
  );
}
