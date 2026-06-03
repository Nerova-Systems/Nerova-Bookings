import { t } from "@lingui/core/macro";
import { AppLayout } from "@repo/ui/components/AppLayout";
import { SidebarInset, SidebarProvider } from "@repo/ui/components/Sidebar";
import { createFileRoute, useNavigate } from "@tanstack/react-router";
import { useCallback, useEffect, useState } from "react";
import { z } from "zod";

import { api, type components, SortableClientProperties, SortOrder } from "@/shared/lib/api/client";
import { MainSideMenu } from "@/shared/components/MainSideMenu";

import { ClientProfileSidePane } from "./-components/ClientProfileSidePane";
import { ClientTable } from "./-components/ClientTable";
import { ClientToolbar } from "./-components/ClientToolbar";
import { DeleteClientDialog } from "./-components/DeleteClientDialog";
import { ManageClientDialog } from "./-components/ManageClientDialog";

type ClientDetails = components["schemas"]["ClientDetails"];

const clientPageSearchSchema = z.object({
  search: z.string().optional(),
  startDate: z.string().optional(),
  endDate: z.string().optional(),
  orderBy: z.nativeEnum(SortableClientProperties).optional(),
  sortOrder: z.nativeEnum(SortOrder).optional(),
  pageOffset: z.number().optional(),
  clientId: z.string().optional()
});

export const Route = createFileRoute("/clients/")({
  staticData: { trackingTitle: "Clients" },
  component: ClientsPage,
  validateSearch: clientPageSearchSchema
});

export default function ClientsPage() {
  const [selectedClients, setSelectedClients] = useState<ClientDetails[]>([]);
  const [profileClient, setProfileClient] = useState<ClientDetails | null>(null);
  const [clientToDelete, setClientToDelete] = useState<ClientDetails | null>(null);
  const [clientToManage, setClientToManage] = useState<ClientDetails | null>(null);
  const [isInitialLoad, setIsInitialLoad] = useState(true);

  const navigate = useNavigate({ from: Route.fullPath });
  const { clientId } = Route.useSearch();

  const handleCloseProfile = useCallback(() => {
    setProfileClient(null);
    navigate({ search: (prev) => ({ ...prev, clientId: undefined }) });

    if (selectedClients.length === 1) {
      setTimeout(() => {
        const selectedRow = document.querySelector(`[data-key="${selectedClients[0].id}"]`);
        if (selectedRow) {
          (selectedRow as HTMLElement).focus();
        }
      }, 0);
    }
  }, [navigate, selectedClients]);

  const handleViewProfile = useCallback(
    (client: ClientDetails | null) => {
      setProfileClient(client);
      navigate({ search: (prev) => ({ ...prev, clientId: client?.id ?? undefined }) });
    },
    [navigate]
  );

  const { data: clientData, isLoading: isLoadingClient } = api.useQuery(
    "get",
    "/api/main/clients/{id}",
    { params: { path: { id: clientId || "" } } },
    { enabled: !!clientId }
  );

  useEffect(() => {
    if (clientId && clientData) {
      setProfileClient(clientData);
      if (isInitialLoad) {
        setSelectedClients([clientData]);
        setIsInitialLoad(false);
      }
    } else if (!clientId && isInitialLoad) {
      setIsInitialLoad(false);
    }
  }, [clientId, clientData, isInitialLoad]);

  const handleDeleteClient = useCallback((client: ClientDetails) => {
    setClientToDelete(client);
  }, []);

  const handleManageClient = useCallback((client: ClientDetails) => {
    setClientToManage(client);
  }, []);

  const getSidePane = () => {
    if (profileClient) {
      return (
        <ClientProfileSidePane
          client={profileClient}
          isOpen={!!profileClient}
          onClose={handleCloseProfile}
          onManageClient={handleManageClient}
          onDeleteClient={handleDeleteClient}
          isLoading={isLoadingClient || !!(clientId && profileClient.id !== clientId)}
        />
      );
    }
    return undefined;
  };

  return (
    <SidebarProvider>
      <MainSideMenu />
      <SidebarInset>
      <>
      <AppLayout
        variant="center"
        sidePane={getSidePane()}
        maxWidth="64rem"
        title={t`Clients`}
        subtitle={t`Manage your booking clients here.`}
      >
        <div className="flex min-h-0 flex-1 flex-col">
          <div className="max-sm:sticky max-sm:top-12">
            <ClientToolbar selectedClients={selectedClients} onSelectedClientsChange={setSelectedClients} />
          </div>
          <div className="flex min-h-0 flex-1 flex-col">
            <ClientTable
              selectedClients={selectedClients}
              onSelectedClientsChange={setSelectedClients}
              onViewProfile={handleViewProfile}
              onManageClient={handleManageClient}
              onDeleteClient={handleDeleteClient}
            />
          </div>
        </div>
      </AppLayout>

      <ManageClientDialog
        client={clientToManage}
        isOpen={clientToManage !== null}
        onOpenChange={(isOpen) => !isOpen && setClientToManage(null)}
      />

      <DeleteClientDialog
        clients={clientToDelete ? [clientToDelete] : []}
        isOpen={clientToDelete !== null}
        onOpenChange={(isOpen) => !isOpen && setClientToDelete(null)}
        onClientsDeleted={() => {
          setSelectedClients([]);
          setProfileClient(null);
          navigate({ search: (prev) => ({ ...prev, clientId: undefined }) });
        }}
      />
    </>
      </SidebarInset>
    </SidebarProvider>
  );
}
