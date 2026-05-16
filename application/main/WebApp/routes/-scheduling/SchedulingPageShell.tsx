import { AppLayout } from "@repo/ui/components/AppLayout";
import { SidebarInset, SidebarProvider } from "@repo/ui/components/Sidebar";

import { MainSideMenu } from "@/shared/components/MainSideMenu";

interface SchedulingPageShellProps {
  title: string;
  titleContent?: React.ReactNode;
  subtitle: string;
  actions?: React.ReactNode;
  maxWidth?: string;
  children: React.ReactNode;
}

export function SchedulingPageShell({
  title,
  titleContent,
  subtitle,
  actions,
  maxWidth = "72rem",
  children
}: Readonly<SchedulingPageShellProps>) {
  const heading = titleContent ?? title;
  const renderedTitle = actions ? (
    <div className="flex items-start justify-between gap-4">
      <div className="min-w-0">{heading}</div>
      <div className="hidden shrink-0 sm:block">{actions}</div>
    </div>
  ) : (
    heading
  );

  return (
    <SidebarProvider>
      <MainSideMenu />
      <SidebarInset>
        <AppLayout variant="center" maxWidth={maxWidth} browserTitle={title} title={renderedTitle} subtitle={subtitle}>
          {actions && <div className="mb-4 sm:hidden">{actions}</div>}
          {children}
        </AppLayout>
      </SidebarInset>
    </SidebarProvider>
  );
}
