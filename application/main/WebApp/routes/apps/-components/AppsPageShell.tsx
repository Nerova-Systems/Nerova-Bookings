import { Trans } from "@lingui/react/macro";
import { AppLayout } from "@repo/ui/components/AppLayout";
import { SidebarInset, SidebarProvider } from "@repo/ui/components/Sidebar";
import { Link } from "@tanstack/react-router";

import { MainSideMenu } from "@/shared/components/MainSideMenu";

interface AppsPageShellProps {
  title: string;
  subtitle: string;
  actions?: React.ReactNode;
  maxWidth?: string;
  children: React.ReactNode;
}

export function AppsPageShell({
  title,
  subtitle,
  actions,
  maxWidth = "72rem",
  children
}: Readonly<AppsPageShellProps>) {
  const renderedTitle = actions ? (
    <div className="flex items-start justify-between gap-4">
      <div className="min-w-0">{title}</div>
      <div className="hidden shrink-0 sm:block">{actions}</div>
    </div>
  ) : (
    title
  );

  return (
    <SidebarProvider>
      <MainSideMenu />
      <SidebarInset>
        <AppLayout variant="center" maxWidth={maxWidth} browserTitle={title} title={renderedTitle} subtitle={subtitle}>
          {actions && <div className="mb-4 sm:hidden">{actions}</div>}
          <div className="mb-6 flex gap-6 border-b border-border pb-px text-sm font-medium">
            <Link
              to="/apps"
              className="border-b-2 border-transparent px-1 py-2 text-muted-foreground transition-colors hover:text-foreground [&.active]:border-primary [&.active]:text-foreground"
              activeOptions={{ exact: true }}
            >
              <Trans>App Store</Trans>
            </Link>
            <Link
              to="/apps/installed"
              className="border-b-2 border-transparent px-1 py-2 text-muted-foreground transition-colors hover:text-foreground [&.active]:border-primary [&.active]:text-foreground"
            >
              <Trans>Connected Apps</Trans>
            </Link>
          </div>
          {children}
        </AppLayout>
      </SidebarInset>
    </SidebarProvider>
  );
}
