import { AppLayout } from "@repo/ui/components/AppLayout";
import { SidebarInset, SidebarProvider } from "@repo/ui/components/Sidebar";

import { MainSideMenu } from "@/shared/components/MainSideMenu";

interface SchedulingPageShellProps {
  title: string;
  subtitle: string;
  actions?: React.ReactNode;
  children: React.ReactNode;
}

export function SchedulingPageShell({ title, subtitle, actions, children }: Readonly<SchedulingPageShellProps>) {
  const renderedTitle = actions ? (
    <div className="flex items-start justify-between gap-4">
      <span>{title}</span>
      <div className="hidden shrink-0 sm:block">{actions}</div>
    </div>
  ) : (
    title
  );

  return (
    <SidebarProvider>
      <MainSideMenu />
      <SidebarInset>
        <AppLayout variant="center" maxWidth="72rem" browserTitle={title} title={renderedTitle} subtitle={subtitle}>
          {actions && <div className="mb-4 sm:hidden">{actions}</div>}
          {children}
        </AppLayout>
      </SidebarInset>
    </SidebarProvider>
  );
}
