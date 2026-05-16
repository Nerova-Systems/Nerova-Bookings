import { AppLayout } from "@repo/ui/components/AppLayout";
import { SidebarInset, SidebarProvider } from "@repo/ui/components/Sidebar";

import { MainSideMenu } from "@/shared/components/MainSideMenu";

interface SchedulingPageShellProps {
  title: string;
  subtitle: string;
  children: React.ReactNode;
}

export function SchedulingPageShell({ title, subtitle, children }: Readonly<SchedulingPageShellProps>) {
  return (
    <SidebarProvider>
      <MainSideMenu />
      <SidebarInset>
        <AppLayout variant="center" maxWidth="72rem" browserTitle={title} title={title} subtitle={subtitle}>
          {children}
        </AppLayout>
      </SidebarInset>
    </SidebarProvider>
  );
}
