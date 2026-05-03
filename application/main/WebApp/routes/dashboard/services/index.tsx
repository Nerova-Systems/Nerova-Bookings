import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { createFileRoute } from "@tanstack/react-router";
import { useEffect, useState } from "react";
import { toast } from "sonner";

import {
  useAppointmentShell,
  useArchiveService,
  useCreateService,
  useRestoreService,
  useUpdateService,
  type Service,
  type ServiceCategory,
  type ServiceMutationRequest
} from "@/shared/lib/appointmentsApi";

import { ServiceCard } from "./-components/ServiceCard";
import { ServiceFormDialog } from "./-components/ServiceFormDialog";

export const Route = createFileRoute("/dashboard/services/")({
  staticData: { trackingTitle: "Services" },
  component: ServicesPage
});

function ServicesPage() {
  const [editingService, setEditingService] = useState<Service | null>(null);
  const [formOpen, setFormOpen] = useState(false);
  const shellQuery = useAppointmentShell();
  const createService = useCreateService();
  const updateService = useUpdateService();
  const archiveService = useArchiveService();
  const restoreService = useRestoreService();
  const categories = groupServices(shellQuery.data?.categories ?? [], shellQuery.data?.services ?? []);
  const archived = (shellQuery.data?.services ?? []).filter((service) => service.archived);
  const activeServices = (shellQuery.data?.services ?? []).filter((service) => !service.archived);
  const totalBookings = activeServices.reduce((sum, service) => sum + service.bookingsThisMonth, 0);
  const handleArchive = (id: string) =>
    archiveService.mutate(id, {
      onSuccess: () => toast.success("Service archived."),
      onError: (error) => toast.error(error instanceof Error ? error.message : "Could not archive service.")
    });
  const handleRestore = (id: string) =>
    restoreService.mutate(id, {
      onSuccess: () => toast.success("Service restored."),
      onError: (error) => toast.error(error instanceof Error ? error.message : "Could not restore service.")
    });

  useEffect(() => {
    document.title = t`Services | Nerova`;
  }, []);

  return (
    <div className="flex min-h-0 flex-1 flex-col overflow-hidden">
      <header className="sticky top-0 z-20 flex shrink-0 items-center gap-4 border-b border-border bg-background px-7 py-3.5">
        <div className="flex flex-col gap-0.5">
          <h1 className="font-display text-[1.375rem] leading-tight">
            <Trans>Services</Trans>
          </h1>
          <span className="text-[12.5px] text-muted-foreground">
            {activeServices.length} active across {categories.length} categories
          </span>
        </div>
        <div className="ml-auto flex items-center gap-2">
          <Button variant="outline" size="sm">
            <Trans>Manage categories</Trans>
          </Button>
          <Button
            size="sm"
            onClick={() => {
              setEditingService(null);
              setFormOpen(true);
            }}
          >
            <Trans>New service</Trans>
          </Button>
        </div>
      </header>

      <div className="flex-1 overflow-y-auto px-7 py-6">
        <div className="mb-6 grid grid-cols-4 gap-3">
          {[
            { value: activeServices.length, label: "Active services" },
            { value: categories.length, label: "Categories" },
            { value: totalBookings, label: "Bookings this month" },
            { value: archived.length, label: "Archived" }
          ].map((stat) => (
            <div key={stat.label} className="rounded-xl border border-border bg-background p-4">
              <div className="font-display text-[1.75rem] leading-none font-semibold">{stat.value}</div>
              <div className="mt-1 text-xs text-muted-foreground">{stat.label}</div>
            </div>
          ))}
        </div>

        {categories.map((cat) => (
          <div key={cat.name} className="mb-7">
            <div className="mb-2.5 flex items-baseline gap-3">
              <h3 className="font-display text-[15px]">{cat.name}</h3>
              <span className="text-xs text-muted-foreground">{cat.services.length} services</span>
              <button
                type="button"
                className="ml-auto text-[12.5px] font-medium text-foreground underline decoration-border underline-offset-3 transition-colors hover:decoration-foreground"
              >
                <Trans>Reorder</Trans>
              </button>
            </div>
            <div className="grid grid-cols-[repeat(auto-fill,minmax(16.25rem,1fr))] gap-3">
              {cat.services.map((svc) => (
                <ServiceCard
                  key={svc.id}
                  service={svc}
                  onEdit={(service) => openServiceForm(service, setEditingService, setFormOpen)}
                  onArchive={handleArchive}
                  onRestore={handleRestore}
                />
              ))}
            </div>
          </div>
        ))}

        {archived.length > 0 && (
          <div className="mb-7">
            <div className="mb-2.5 flex items-baseline gap-3">
              <h3 className="font-display text-[15px]">
                <Trans>Archived</Trans>
              </h3>
              <span className="text-xs text-muted-foreground">{archived.length} service</span>
            </div>
            <div className="grid grid-cols-[repeat(auto-fill,minmax(16.25rem,1fr))] gap-3">
              {archived.map((svc) => (
                <ServiceCard
                  key={svc.id}
                  service={svc}
                  onEdit={(service) => openServiceForm(service, setEditingService, setFormOpen)}
                  onArchive={handleArchive}
                  onRestore={handleRestore}
                />
              ))}
            </div>
            <div className="mt-2 rounded-lg bg-muted px-3.5 py-2.5 text-xs text-muted-foreground">
              <Trans>Archived services are hidden from the booking flow.</Trans>{" "}
              <strong className="font-medium text-foreground">
                <Trans>Restore any service to make it bookable again.</Trans>
              </strong>
            </div>
          </div>
        )}
      </div>
      {formOpen && (
        <ServiceFormDialog
          service={editingService ?? undefined}
          categories={shellQuery.data?.categories ?? []}
          pending={createService.isPending || updateService.isPending}
          onClose={() => setFormOpen(false)}
          onSubmit={(request: ServiceMutationRequest) => {
            if (editingService) {
              updateService.mutate(
                { id: editingService.id, request },
                {
                  onSuccess: () => closeServiceForm("Service updated.", setFormOpen),
                  onError: (error) => toast.error(error instanceof Error ? error.message : "Could not update service.")
                }
              );
            } else {
              createService.mutate(request, {
                onSuccess: () => closeServiceForm("Service created.", setFormOpen),
                onError: (error) => toast.error(error instanceof Error ? error.message : "Could not create service.")
              });
            }
          }}
        />
      )}
    </div>
  );
}

function groupServices(categories: ServiceCategory[], services: Service[]) {
  return categories
    .map((category) => ({
      name: category.name,
      services: services.filter((service) => service.categoryId === category.id && !service.archived)
    }))
    .filter((category) => category.services.length > 0);
}

function openServiceForm(
  service: Service,
  setEditingService: (service: Service) => void,
  setFormOpen: (open: boolean) => void
) {
  setEditingService(service);
  setFormOpen(true);
}

function closeServiceForm(message: string, setFormOpen: (open: boolean) => void) {
  toast.success(message);
  setFormOpen(false);
}
