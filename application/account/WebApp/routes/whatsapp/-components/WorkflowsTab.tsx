import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { SwitchField } from "@repo/ui/components/SwitchField";
import {
  BellRingIcon,
  CalendarCheckIcon,
  CreditCardIcon,
  MessageSquareIcon,
  ShieldCheckIcon,
  UsersIcon
} from "lucide-react";

type WorkflowCardProps = {
  icon: React.ReactNode;
  title: React.ReactNode;
  description: React.ReactNode;
  category: "always-on" | "toggle";
  enabled?: boolean;
  onToggle?: (enabled: boolean) => void;
};

function WorkflowCard({ icon, title, description, category, enabled, onToggle }: Readonly<WorkflowCardProps>) {
  const isAlwaysOn = category === "always-on";

  return (
    <div className="flex items-start gap-4 rounded-xl border border-border bg-card p-5 transition-all hover:border-primary/20 hover:shadow-sm">
      <div
        className={`flex size-10 shrink-0 items-center justify-center rounded-lg ${
          isAlwaysOn
            ? "bg-green-100 text-green-700 dark:bg-green-950 dark:text-green-400"
            : "bg-primary/10 text-primary"
        }`}
      >
        {icon}
      </div>

      <div className="min-w-0 flex-1">
        <div className="mb-0.5 flex items-center gap-2">
          <h4 className="text-sm font-semibold">{title}</h4>
          {isAlwaysOn && (
            <span className="rounded-full bg-green-100 px-2 py-0.5 text-[10px] font-bold tracking-wider text-green-700 uppercase dark:bg-green-900/50 dark:text-green-400">
              Always on
            </span>
          )}
        </div>
        <p className="text-xs leading-relaxed text-muted-foreground">{description}</p>
      </div>

      {!isAlwaysOn && (
        <div className="shrink-0 pt-0.5">
          <SwitchField label="" defaultChecked={enabled} onCheckedChange={(checked) => onToggle?.(checked)} />
        </div>
      )}
    </div>
  );
}

export function WorkflowsTab() {
  return (
    <div className="flex flex-col gap-6">
      {/* Always-on flows */}
      <div>
        <div className="mb-3">
          <h3 className="text-sm font-bold tracking-wider text-muted-foreground uppercase">
            <Trans>Core flows</Trans>
          </h3>
          <p className="mt-1 text-xs text-muted-foreground">
            <Trans>These flows are always active and handle core booking functionality.</Trans>
          </p>
        </div>

        <div className="flex flex-col gap-2">
          <WorkflowCard
            icon={<UsersIcon className="size-5" />}
            title={<Trans>Client identification</Trans>}
            description={
              <Trans>
                Automatically identifies returning clients by phone number. New clients are onboarded with email
                verification and contact details. Returning clients with a new phone number verify via email OTP.
              </Trans>
            }
            category="always-on"
          />

          <WorkflowCard
            icon={<CalendarCheckIcon className="size-5" />}
            title={<Trans>Booking flow</Trans>}
            description={
              <Trans>
                Structured booking experience: service selection → date → time → summary → confirm. Multi-location
                tenants automatically see department/team selection. Service details (duration, cost, description) shown
                via info drawer.
              </Trans>
            }
            category="always-on"
          />
        </div>
      </div>

      {/* Toggleable add-ons */}
      <div>
        <div className="mb-3">
          <h3 className="text-sm font-bold tracking-wider text-muted-foreground uppercase">
            <Trans>Add-on workflows</Trans>
          </h3>
          <p className="mt-1 text-xs text-muted-foreground">
            <Trans>
              Toggle these workflows to extend your booking experience. Each adds a utility message to the flow.
            </Trans>
          </p>
        </div>

        <div className="flex flex-col gap-2">
          <WorkflowCard
            icon={<BellRingIcon className="size-5" />}
            title={<Trans>Booking reminders</Trans>}
            description={
              <Trans>
                Send an automated WhatsApp reminder before the appointment. Reduces no-shows by up to 40%. Sent as a
                utility message.
              </Trans>
            }
            category="toggle"
            enabled={false}
          />

          <WorkflowCard
            icon={<CreditCardIcon className="size-5" />}
            title={<Trans>Payment before booking</Trans>}
            description={
              <Trans>
                Require payment or a deposit before confirming the booking. Adds a payment link page to the booking
                flow. Requires Paystack to be connected.
              </Trans>
            }
            category="toggle"
            enabled={false}
          />

          <WorkflowCard
            icon={<MessageSquareIcon className="size-5" />}
            title={<Trans>Booking confirmation</Trans>}
            description={
              <Trans>
                Send a WhatsApp confirmation message after a booking is completed. Includes booking details, staff name,
                and cancellation contact. Sent as a utility message.
              </Trans>
            }
            category="toggle"
            enabled={true}
          />
        </div>
      </div>

      {/* Info note */}
      <div className="flex items-start gap-2.5 rounded-lg border border-border bg-muted/30 p-4 text-xs text-muted-foreground">
        <ShieldCheckIcon className="mt-0.5 size-4 shrink-0" />
        <div>
          <Trans>
            Utility messages (reminders, confirmations) are billed at WhatsApp's utility rate. Marketing messages
            (follow-ups, promotions) are coming soon and will be billed at a separate rate.
          </Trans>
        </div>
      </div>
    </div>
  );
}
