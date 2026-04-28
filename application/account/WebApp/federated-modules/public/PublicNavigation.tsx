import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { loginPath, signUpPath } from "@repo/infrastructure/auth/constants";
import { useIsAuthenticated } from "@repo/infrastructure/auth/hooks";
import { Link } from "@repo/ui/components/Link";
import {
  CalendarDaysIcon,
  ChevronDownIcon,
  CreditCardIcon,
  DatabaseZapIcon,
  HospitalIcon,
  MessageCircleIcon,
  PlugZapIcon,
  ScissorsIcon,
  SparklesIcon,
  StoreIcon,
  UserRoundIcon,
  UsersRoundIcon
} from "lucide-react";
import { Suspense, useState } from "react";
import type { LucideIcon } from "lucide-react";

import UserMenu from "@/federated-modules/userMenu/UserMenu";

const businessSolutions: readonly SolutionItem[] = [
  {
    title: "Solo operators",
    description: "Fixed WhatsApp booking flows for independent service businesses.",
    href: "/#why-nerova",
    Icon: UserRoundIcon
  },
  {
    title: "Studios and salons",
    description: "Services, staff schedules, reminders, and payment prompts for small teams.",
    href: "/#product",
    Icon: ScissorsIcon
  },
  {
    title: "Clinics and practices",
    description: "Structured appointment operations for consultation-led teams.",
    href: "/#product",
    Icon: HospitalIcon
  },
  {
    title: "Multi-team businesses",
    description: "Stronger controls across calendars, staff, services, and account billing.",
    href: "/#pricing",
    Icon: StoreIcon
  }
];

const workflowSolutions: readonly SolutionItem[] = [
  {
    title: "WhatsApp flows",
    description: "Fixed booking, confirmation, reminder, reschedule, and payment sequences.",
    href: "/#why-nerova",
    Icon: MessageCircleIcon
  },
  {
    title: "Bookings",
    description: "Availability, client intake, and confirmed appointment flows.",
    href: "/#product",
    Icon: CalendarDaysIcon
  },
  {
    title: "Staff schedules",
    description: "Daily team views for appointments, service notes, and handoffs.",
    href: "/#product",
    Icon: UsersRoundIcon
  },
  {
    title: "Payments",
    description: "PayFast subscriptions now, planned Stitch client payment flows next.",
    href: "/#payments",
    Icon: CreditCardIcon
  },
  {
    title: "Integrations",
    description: "Google tools first, Microsoft later, and Nango-powered connectors over time.",
    href: "/#integrations",
    Icon: PlugZapIcon
  }
];

type SolutionItem = {
  readonly title: string;
  readonly description: string;
  readonly href: string;
  readonly Icon: LucideIcon;
};

export default function PublicNavigation() {
  const isAuthenticated = useIsAuthenticated();
  const [isSolutionsOpen, setIsSolutionsOpen] = useState(false);

  return (
    <header className="sticky top-0 z-50 px-4 pt-4">
      <nav
        className="mx-auto flex h-16 w-full max-w-7xl items-center justify-between gap-4 rounded-3xl border border-border/80 bg-background/95 px-5 shadow-[0_10px_40px_rgba(17,17,17,0.08)] backdrop-blur"
        aria-label={t`Public navigation`}
        onMouseLeave={() => setIsSolutionsOpen(false)}
      >
        <Link href="/" variant="logo" underline={false} className="text-3xl font-semibold text-foreground">
          Nerova
        </Link>

        <div className="hidden items-center gap-1 lg:flex">
          <div className="relative">
            <button
              type="button"
              className="inline-flex h-10 items-center gap-1 rounded-full px-4 text-sm font-medium text-foreground transition-colors hover:bg-muted"
              aria-expanded={isSolutionsOpen}
              onClick={() => setIsSolutionsOpen(true)}
              onFocus={() => setIsSolutionsOpen(true)}
              onMouseEnter={() => setIsSolutionsOpen(true)}
            >
              <Trans>Solutions</Trans>
              <ChevronDownIcon className="size-4" />
            </button>
            {isSolutionsOpen && <SolutionsMegaMenu />}
          </div>

          <a href="/#product" className="inline-flex h-10 items-center rounded-full px-4 text-sm font-medium text-foreground hover:bg-muted">
            <Trans>Product</Trans>
          </a>
          <a href="/#why-nerova" className="inline-flex h-10 items-center rounded-full px-4 text-sm font-medium text-foreground hover:bg-muted">
            <Trans>Why us</Trans>
          </a>
          <a href="/#integrations" className="inline-flex h-10 items-center rounded-full px-4 text-sm font-medium text-foreground hover:bg-muted">
            <Trans>Integrations</Trans>
          </a>
          <a href="/#pricing" className="inline-flex h-10 items-center rounded-full px-4 text-sm font-medium text-foreground hover:bg-muted">
            <Trans>Pricing</Trans>
          </a>
        </div>

        {isAuthenticated ? (
          <Suspense fallback={<div className="h-10" />}>
            <UserMenu />
          </Suspense>
        ) : (
          <div className="flex items-center gap-2">
            <Link href={loginPath} variant="button-secondary" underline={false} className="hidden h-10 px-4 sm:inline-flex">
              <Trans>Log in</Trans>
            </Link>
            <Link href={signUpPath} variant="button-primary" underline={false} className="h-10 rounded-2xl px-4">
              <Trans>Start free trial</Trans>
            </Link>
          </div>
        )}
      </nav>
    </header>
  );
}

function SolutionsMegaMenu() {
  return (
    <div className="absolute top-13 left-1/2 w-[min(78rem,calc(100vw-3rem))] -translate-x-1/2 rounded-3xl border border-border bg-background p-6 shadow-[0_28px_90px_rgba(17,17,17,0.14)]">
      <div className="grid gap-8 lg:grid-cols-[1.05fr_1.05fr_0.9fr]">
        <SolutionColumn title="By business type" items={businessSolutions} />
        <SolutionColumn title="By workflow" items={workflowSolutions} />
        <div className="flex min-h-80 flex-col justify-between rounded-3xl bg-[#101010] p-6 text-white">
          <div className="flex justify-end">
            <span className="inline-flex items-center gap-2 rounded-full bg-white px-3 py-1 text-xs font-semibold text-[#111111]">
              <SparklesIcon className="size-3.5" />
              <Trans>Coming soon</Trans>
            </span>
          </div>
          <div>
            <DatabaseZapIcon className="mb-5 size-10 text-white" />
            <h3 className="text-3xl font-semibold">Custom datasets</h3>
            <p className="mt-3 max-w-sm text-sm leading-6 text-white/70">
              <Trans>Enterprise workflows for specialized appointment teams. Coming soon, with no self-serve checkout path yet.</Trans>
            </p>
          </div>
        </div>
      </div>
    </div>
  );
}

function SolutionColumn({ title, items }: { readonly title: string; readonly items: readonly SolutionItem[] }) {
  return (
    <div>
      <h2 className="mb-4 text-sm font-semibold text-muted-foreground">{title}</h2>
      <div className="grid gap-3">
        {items.map(({ title: itemTitle, description, href, Icon }) => (
          <a key={itemTitle} href={href} className="grid grid-cols-[4.5rem_1fr] gap-4 rounded-2xl p-3 transition-colors hover:bg-muted">
            <span className="flex size-16 items-center justify-center rounded-2xl border border-border bg-muted/40">
              <Icon className="size-7 text-foreground" />
            </span>
            <span>
              <span className="block text-base font-semibold text-foreground">{itemTitle}</span>
              <span className="mt-1 block text-sm leading-6 text-muted-foreground">{description}</span>
            </span>
          </a>
        ))}
      </div>
    </div>
  );
}
