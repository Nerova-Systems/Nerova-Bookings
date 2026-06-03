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
  return (
    <SidebarProvider>
      <MainSideMenu />
      <SidebarInset>
        <AppLayout variant="center" maxWidth={maxWidth} browserTitle={title} title={title} subtitle={subtitle}>
          <div className="mb-6 flex flex-col gap-3 border-b border-border pb-px sm:flex-row sm:items-center sm:justify-between">
            <nav className="flex gap-6 text-sm font-medium">
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
            </nav>
            {actions && <div className="pb-2 sm:pb-0">{actions}</div>}
          </div>
          {children}
        </AppLayout>
      </SidebarInset>
    </SidebarProvider>
  );
}
