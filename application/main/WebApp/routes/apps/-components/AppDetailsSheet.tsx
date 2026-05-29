import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Badge } from "@repo/ui/components/Badge";
import { Button } from "@repo/ui/components/Button";
import { Sheet, SheetContent, SheetHeader, SheetTitle, SheetDescription } from "@repo/ui/components/Sheet";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@repo/ui/components/Tabs";
import {
  SettingsIcon,
  UserCircleIcon,
  ZapIcon,
  BarChart3Icon,
  CheckCircle2Icon,
  AlertTriangleIcon,
  Trash2Icon,
  Loader2Icon,
  ArrowRightIcon
} from "lucide-react";
import { useState, lazy, Suspense } from "react";
import { toast } from "sonner";

import { api, queryClient } from "@/shared/lib/api/client";

import type { App } from "./appsTypes";

import { getAppCategoryLabel, getMissingPrerequisite } from "./appsTypes";

// Dynamically load WABA tabs from the account remote via Module Federation
const SetupTab = lazy(() => import("account/whatsapp/SetupTab").then((m) => ({ default: m.SetupTab })));
const ProfileTab = lazy(() => import("account/whatsapp/ProfileTab").then((m) => ({ default: m.ProfileTab })));
const WorkflowsTab = lazy(() => import("account/whatsapp/WorkflowsTab").then((m) => ({ default: m.WorkflowsTab })));
const UsageTab = lazy(() => import("account/whatsapp/UsageTab").then((m) => ({ default: m.UsageTab })));

interface AppDetailsSheetProps {
  app: App | null;
  allApps: readonly App[];
  isOpen: boolean;
  onOpenChange: (open: boolean) => void;
  onUninstall: (app: App) => void;
}

export function AppDetailsSheet({ app, allApps, isOpen, onOpenChange, onUninstall }: Readonly<AppDetailsSheetProps>) {
  const [activeTab, setActiveTab] = useState<string>("setup");
  const [confirmUninstallWaba, setConfirmUninstallWaba] = useState<boolean>(false);

  const installMutation = api.useMutation("post", "/api/apps/{slug}/install", {
    onSuccess: (response) => {
      window.location.href = response.authorizeUrl;
    },
    onError: (error) => {
      const detail = (error as { detail?: string } | null | undefined)?.detail;
      toast.error(detail ?? t`Failed to start install. Please try again.`);
    }
  });

  // WABA specific uninstall mutations (sequentially delete main installation and disconnect in account-api)
  const disconnectWabaMutation = (api as any).useMutation("delete", "/api/whatsapp/disconnect", {
    onSuccess: () => {
      toast.success(t`WhatsApp Business integration completely disconnected.`);
      void queryClient.invalidateQueries();
      setConfirmUninstallWaba(false);
      onOpenChange(false);
    },
    onError: (error: any) => {
      const detail = error?.detail;
      toast.error(detail ?? t`Failed to disconnect WhatsApp Business completely.`);
    }
  });

  const uninstallWabaMutation = api.useMutation("delete", "/api/apps/{slug}/uninstall", {
    onSuccess: () => {
      // Step 2: Clear account-api config
      disconnectWabaMutation.mutate(undefined as any);
    },
    onError: (error) => {
      const detail = (error as { detail?: string } | null | undefined)?.detail;
      toast.error(detail ?? t`Failed to uninstall app.`);
    }
  });

  if (!app) return null;

  const isWaba = app.slug === "whatsapp";
  const isInstalled = app.slug === "whatsapp" ? app.isInstalledForTenant : app.isConnectedForUser;
  const missingPrerequisite = getMissingPrerequisite(app, allApps);
  const canInstall = app.isActive && missingPrerequisite === null && !isInstalled;

  const handleUninstallWaba = () => {
    uninstallWabaMutation.mutate({ params: { path: { slug: "whatsapp" } } });
  };

  return (
    <Sheet open={isOpen} onOpenChange={onOpenChange}>
      <SheetContent
        side="right"
        className={`flex h-full flex-col border-l border-border bg-popover p-0 shadow-2xl transition duration-300 ease-in-out ${
          isWaba && isInstalled ? "w-[720px] sm:max-w-3xl" : "w-[500px] sm:max-w-xl"
        }`}
      >
        {/* Scrollable Container */}
        <div className="flex flex-1 flex-col gap-6 overflow-y-auto p-6">
          {/* Header */}
          <div className="flex items-start gap-4 border-b border-border pb-5">
            <AppIcon app={app} />
            <div className="min-w-0 flex-1">
              <div className="flex flex-wrap items-center gap-2">
                <SheetTitle className="text-xl leading-tight font-bold text-foreground">{app.name}</SheetTitle>
                <Badge variant="outline" className="text-[10px] font-semibold text-muted-foreground uppercase">
                  {getAppCategoryLabel(app.category)}
                </Badge>
                {isInstalled && (
                  <Badge className="flex items-center gap-1 border-emerald-500/20 bg-emerald-500/10 text-[10px] font-semibold text-emerald-600 dark:bg-emerald-500/20 dark:text-emerald-400">
                    <CheckCircle2Icon className="size-3" />
                    <Trans>Installed</Trans>
                  </Badge>
                )}
              </div>
              <SheetDescription className="mt-1 text-xs text-muted-foreground">{app.description}</SheetDescription>
            </div>
          </div>

          {/* Dynamic Content */}
          {isWaba && isInstalled ? (
            /* Premium WABA Configuration Tabs inside the Drawer */
            <div className="flex flex-1 flex-col">
              <Tabs value={activeTab} onValueChange={setActiveTab} className="flex flex-1 flex-col">
                <TabsList className="mb-4 flex gap-1 rounded-lg bg-muted/50 p-1">
                  <TabsTrigger
                    value="setup"
                    className="flex flex-1 items-center justify-center gap-1.5 py-1.5 text-xs font-medium transition-all duration-200"
                  >
                    <SettingsIcon className="size-3.5" />
                    <Trans>Setup</Trans>
                  </TabsTrigger>
                  <TabsTrigger
                    value="profile"
                    className="flex flex-1 items-center justify-center gap-1.5 py-1.5 text-xs font-medium transition-all duration-200"
                  >
                    <UserCircleIcon className="size-3.5" />
                    <Trans>Profile</Trans>
                  </TabsTrigger>
                  <TabsTrigger
                    value="workflows"
                    className="flex flex-1 items-center justify-center gap-1.5 py-1.5 text-xs font-medium transition-all duration-200"
                  >
                    <ZapIcon className="size-3.5" />
                    <Trans>Workflows</Trans>
                  </TabsTrigger>
                  <TabsTrigger
                    value="usage"
                    className="flex flex-1 items-center justify-center gap-1.5 py-1.5 text-xs font-medium transition-all duration-200"
                  >
                    <BarChart3Icon className="size-3.5" />
                    <Trans>Usage</Trans>
                  </TabsTrigger>
                </TabsList>

                <div className="min-h-[400px] flex-1 rounded-xl border border-border bg-card/30 p-5">
                  <Suspense
                    fallback={
                      <div className="flex h-48 w-full items-center justify-center">
                        <Loader2Icon className="size-6 animate-spin text-primary" />
                      </div>
                    }
                  >
                    <TabsContent value="setup" className="mt-0">
                      <SetupTab />
                    </TabsContent>
                    <TabsContent value="profile" className="mt-0">
                      <ProfileTab />
                    </TabsContent>
                    <TabsContent value="workflows" className="mt-0">
                      <WorkflowsTab />
                    </TabsContent>
                    <TabsContent value="usage" className="mt-0">
                      <UsageTab />
                    </TabsContent>
                  </Suspense>
                </div>
              </Tabs>

              {/* Inline Premium Uninstall controls for WABA */}
              <div className="mt-6 border-t border-border pt-4">
                {confirmUninstallWaba ? (
                  <div className="flex flex-col gap-3 rounded-xl border border-destructive/20 bg-destructive/5 p-4 duration-200 animate-in fade-in slide-in-from-bottom-2">
                    <div className="flex items-start gap-2.5">
                      <AlertTriangleIcon className="mt-0.5 size-4 shrink-0 text-destructive" />
                      <div className="text-xs">
                        <p className="font-semibold text-destructive">
                          <Trans>Confirm Disconnection</Trans>
                        </p>
                        <p className="mt-0.5 text-muted-foreground">
                          <Trans>
                            This will delete your WhatsApp Business details, RSA key pair, and Paystack links
                            completely. Existing bookings will not be altered, but new bookings will no longer run
                            WhatsApp flows.
                          </Trans>
                        </p>
                      </div>
                    </div>
                    <div className="flex justify-end gap-2">
                      <Button
                        size="xs"
                        variant="outline"
                        onClick={() => setConfirmUninstallWaba(false)}
                        disabled={uninstallWabaMutation.isPending}
                      >
                        <Trans>Cancel</Trans>
                      </Button>
                      <Button
                        size="xs"
                        variant="destructive"
                        isPending={uninstallWabaMutation.isPending || disconnectWabaMutation.isPending}
                        onClick={handleUninstallWaba}
                      >
                        <Trans>Yes, Disconnect</Trans>
                      </Button>
                    </div>
                  </div>
                ) : (
                  <div className="flex justify-end">
                    <Button
                      variant="ghost"
                      size="sm"
                      className="flex items-center gap-1.5 text-destructive hover:bg-destructive/5 hover:text-destructive"
                      onClick={() => setConfirmUninstallWaba(true)}
                    >
                      <Trash2Icon className="size-4" />
                      <Trans>Uninstall WhatsApp Business</Trans>
                    </Button>
                  </div>
                )}
              </div>
            </div>
          ) : (
            /* Detail card display for uninstalled WABA or Standard Apps */
            <div className="flex flex-1 flex-col justify-between gap-6">
              <div className="flex flex-col gap-4">
                <div>
                  <h4 className="mb-1 text-xs font-semibold tracking-wider text-muted-foreground uppercase">
                    <Trans>About {app.name}</Trans>
                  </h4>
                  <div className="prose prose-sm dark:prose-invert text-sm leading-relaxed text-muted-foreground">
                    {isWaba ? (
                      <p>
                        <Trans>
                          Connect WhatsApp Business Account to power up booking messages and workflows directly to
                          customers. Customers can receive instant confirmation, reminders, feedback collection, and
                          process secure payments inside SA's favorite messaging app.
                        </Trans>
                      </p>
                    ) : (
                      <p>
                        <Trans>
                          Integrate {app.name} with your Nerova platform to synchronize your availability, automatically
                          copy bookings to your workspace schedule, and spin up direct video conferencing links for all
                          scheduled appointments.
                        </Trans>
                      </p>
                    )}
                  </div>
                </div>

                <div className="my-2 grid grid-cols-2 gap-4 border-t border-b border-border py-4">
                  <div>
                    <span className="block text-[10px] font-semibold text-muted-foreground uppercase">
                      <Trans>Price</Trans>
                    </span>
                    <span className="text-sm font-medium text-foreground">
                      <Trans>Included in Plan</Trans>
                    </span>
                  </div>
                  <div>
                    <span className="block text-[10px] font-semibold text-muted-foreground uppercase">
                      <Trans>Publisher</Trans>
                    </span>
                    <span className="text-sm font-medium text-foreground">
                      <Trans>Nerova Systems</Trans>
                    </span>
                  </div>
                </div>

                {missingPrerequisite && (
                  <div className="flex gap-3 rounded-lg border border-amber-200 bg-amber-50/50 p-4 dark:border-amber-900/50 dark:bg-amber-950/20">
                    <AlertTriangleIcon className="mt-0.5 size-4 shrink-0 text-amber-600 dark:text-amber-400" />
                    <div className="text-xs">
                      <p className="font-semibold text-amber-800 dark:text-amber-300">
                        <Trans>Prerequisite Required</Trans>
                      </p>
                      <p className="mt-0.5 text-amber-700/95 dark:text-amber-400/90">
                        <Trans>
                          You must install and connect <strong>{missingPrerequisite.name}</strong> first before you can
                          enable {app.name}.
                        </Trans>
                      </p>
                    </div>
                  </div>
                )}
              </div>

              {/* Action Buttons */}
              <div className="mt-auto flex items-center justify-between border-t border-border pt-5">
                <div>
                  {!isInstalled && (
                    <span className="block text-xs text-muted-foreground">
                      <Trans>Requires authorization permission</Trans>
                    </span>
                  )}
                </div>
                <div>
                  {isInstalled ? (
                    <Button
                      variant="destructive"
                      size="sm"
                      onClick={() => {
                        onOpenChange(false);
                        onUninstall(app);
                      }}
                    >
                      <Trash2Icon className="size-4" />
                      <Trans>Uninstall App</Trans>
                    </Button>
                  ) : isWaba ? (
                    /* For WABA, clicking install starts the Setup tab flow by setting isConnectedForUser client-side */
                    <Button
                      size="sm"
                      onClick={() => {
                        // For WABA, we directly mount the SetupTab within the Sheet by simulating install
                        installMutation.mutate({ params: { path: { slug: "whatsapp" } } });
                      }}
                      isPending={installMutation.isPending}
                    >
                      <Trans>Connect Account</Trans>
                      <ArrowRightIcon className="size-4" />
                    </Button>
                  ) : (
                    <Button
                      size="sm"
                      disabled={!canInstall}
                      isPending={installMutation.isPending}
                      onClick={() => installMutation.mutate({ params: { path: { slug: app.slug } } })}
                    >
                      <Trans>Install App</Trans>
                      <ArrowRightIcon className="size-4" />
                    </Button>
                  )}
                </div>
              </div>
            </div>
          )}
        </div>
      </SheetContent>
    </Sheet>
  );
}

function AppIcon({ app }: Readonly<{ app: App }>) {
  if (app.logoUrl) {
    return (
      <img
        src={app.logoUrl}
        alt=""
        className="h-12 w-12 shrink-0 rounded-xl border bg-background object-contain p-2 shadow-sm"
      />
    );
  }
  return (
    <div
      aria-hidden="true"
      className="flex h-12 w-12 shrink-0 items-center justify-center rounded-xl border bg-muted text-lg font-bold text-muted-foreground shadow-sm"
    >
      {app.name.slice(0, 1).toUpperCase()}
    </div>
  );
}
