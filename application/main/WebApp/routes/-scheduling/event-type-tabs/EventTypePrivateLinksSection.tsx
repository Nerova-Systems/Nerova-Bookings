import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { TextField } from "@repo/ui/components/TextField";
import { CopyIcon, LinkIcon, TrashIcon } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";

import { api, queryClient } from "@/shared/lib/api/client";

export function EventTypePrivateLinksSection({ eventTypeId }: Readonly<{ eventTypeId: string }>) {
  const [filter, setFilter] = useState("");
  const { data, isLoading } = api.useQuery("get", "/api/event-types/{id}/hashed-links", {
    params: { path: { id: eventTypeId } }
  });
  const hashedLinks = data?.hashedLinks ?? [];
  const filteredLinks = filter ? hashedLinks.filter((link) => link.hash.includes(filter)) : hashedLinks;

  const createMutation = api.useMutation("post", "/api/event-types/{id}/hashed-links", {
    onSuccess: () => {
      toast.success(t`Private link created`);
      void queryClient.invalidateQueries();
    }
  });
  const deleteMutation = api.useMutation("delete", "/api/event-types/{id}/hashed-links/{hashedLinkId}", {
    onSuccess: () => {
      toast.success(t`Private link removed`);
      void queryClient.invalidateQueries();
    }
  });

  const handleCreate = () => {
    createMutation.mutate({
      params: { path: { id: eventTypeId } },
      body: { eventTypeId, hash: null, expiresAt: null, expiresAfterUses: null }
    });
  };

  const handleDelete = (hashedLinkId: string) => {
    deleteMutation.mutate({ params: { path: { id: eventTypeId, hashedLinkId } } });
  };

  const handleCopy = async (hash: string) => {
    await navigator.clipboard.writeText(`${window.location.origin}/d/${hash}`);
    toast.success(t`Link copied`);
  };

  return (
    <div className="grid gap-3">
      <TextField name="privateLinksFilter" label={t`Private links`} value={filter} onChange={setFilter} />
      <div className="text-sm text-muted-foreground">
        <Trans>Generate one-off links that bypass the public schedule and expire after use.</Trans>
      </div>
      {isLoading ? (
        <div className="text-sm text-muted-foreground">
          <Trans>Loading private links…</Trans>
        </div>
      ) : filteredLinks.length === 0 ? (
        <div className="rounded-md border p-3 text-sm text-muted-foreground">
          <Trans>No private links yet.</Trans>
        </div>
      ) : (
        <ul className="grid gap-2">
          {filteredLinks.map((link) => (
            <li key={link.id} className="flex items-center justify-between gap-2 rounded-md border p-3">
              <div className="flex min-w-0 items-center gap-2">
                <LinkIcon className="h-4 w-4 shrink-0" aria-hidden="true" />
                <code className="truncate text-sm">{link.hash}</code>
              </div>
              <div className="flex shrink-0 items-center gap-1">
                <Button
                  variant="ghost"
                  size="sm"
                  aria-label={t`Copy link`}
                  onClick={() => {
                    void handleCopy(link.hash);
                  }}
                >
                  <CopyIcon className="h-4 w-4" aria-hidden="true" />
                </Button>
                <Button
                  variant="ghost"
                  size="sm"
                  aria-label={t`Remove link`}
                  onClick={() => handleDelete(link.id)}
                  disabled={deleteMutation.isPending}
                >
                  <TrashIcon className="h-4 w-4" aria-hidden="true" />
                </Button>
              </div>
            </li>
          ))}
        </ul>
      )}
      <div>
        <Button variant="secondary" onClick={handleCreate} disabled={createMutation.isPending}>
          <Trans>Create private link</Trans>
        </Button>
      </div>
    </div>
  );
}
