import { Trans } from "@lingui/react/macro";
import { Link } from "@repo/ui/components/Link";
import { Tooltip, TooltipContent, TooltipTrigger } from "@repo/ui/components/Tooltip";
import { MailIcon } from "lucide-react";

export default function PublicFooter() {
  const currentYear = new Date().getFullYear();

  return (
    <footer className="w-full bg-[#101010] text-[#a1a1aa]">
      <div className="mx-auto max-w-7xl px-6 py-14">
        <div className="grid gap-10 border-b border-white/10 pb-12 lg:grid-cols-[1.2fr_0.8fr_0.8fr_0.8fr]">
          <div className="max-w-md">
            <Link href="/" variant="logo" underline={false} className="text-2xl font-semibold text-white">
              Nerova
            </Link>
            <p className="mt-4 leading-7">
              <Trans>Business operations for appointment teams, starting with bookings, staff schedules, services, payments, and integrations.</Trans>
            </p>
          </div>

          <FooterColumn
            title="Product"
            links={[
              ["Pricing", "/#pricing"],
              ["Integrations", "/#integrations"],
              ["Start free trial", "/signup"]
            ]}
          />
          <FooterColumn
            title="Company"
            links={[
              ["Nerova Systems", "https://nerovasystems.com"],
              ["Contact", "mailto:support@nerovasystems.com"]
            ]}
          />
          <FooterColumn
            title="Legal"
            links={[
              ["Compliance", "/legal/"],
              ["Terms", "/legal/terms"],
              ["Privacy", "/legal/privacy"],
              ["DPA", "/legal/dpa"]
            ]}
          />
        </div>

        <div className="flex flex-col items-center justify-between gap-6 pt-8 sm:flex-row">
          <p className="text-sm">
            <Trans>© {currentYear} Nerova Systems. All rights reserved.</Trans>
          </p>

          <Tooltip>
            <TooltipTrigger
              render={
                <Link
                  href="mailto:support@nerovasystems.com"
                  aria-label="Email Nerova"
                  variant="icon"
                  underline={false}
                  className="bg-white/10 text-[#a1a1aa] hover:bg-white/15 hover:text-white"
                >
                  <MailIcon className="size-5" />
                </Link>
              }
            />
            <TooltipContent>
              <Trans>Email Nerova</Trans>
            </TooltipContent>
          </Tooltip>
        </div>
      </div>
    </footer>
  );
}

function FooterColumn({ title, links }: { readonly title: string; readonly links: readonly (readonly [string, string])[] }) {
  return (
    <div>
      <h2 className="text-sm font-semibold text-white">{title}</h2>
      <ul className="mt-4 flex flex-col gap-3">
        {links.map(([label, href]) => (
          <li key={href}>
            {href.startsWith("/#") ? (
              <a href={href} className="inline-flex p-0 text-sm font-medium text-[#a1a1aa] hover:text-white">
                {label}
              </a>
            ) : (
              <Link href={href} className="p-0 text-[#a1a1aa] hover:text-white" underline={false}>
                {label}
              </Link>
            )}
          </li>
        ))}
      </ul>
    </div>
  );
}
