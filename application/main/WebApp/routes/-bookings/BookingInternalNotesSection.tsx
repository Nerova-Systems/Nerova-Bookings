import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { Skeleton } from "@repo/ui/components/Skeleton";
import { Textarea } from "@repo/ui/components/Textarea";
import { useFormatDate } from "@repo/ui/hooks/useSmartDate";
import { Trash2Icon } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";

import type { Schemas } from "@/shared/lib/api/client";

import { api, queryClient } from "@/shared/lib/api/client";

import { GeneralApiErrors } from "../-scheduling/ApiErrors";
import { SectionTitle } from "./BookingDetailsSheetParts";

type BookingInternalNote = Schemas["BookingInternalNoteResponse"];
type BookingInternalNoteId = Schemas["BookingInternalNoteId"];

export function BookingInternalNotesSection({
  bookingId,
  isLoading,
  notes
}: Readonly<{ bookingId: string; isLoading: boolean; notes: BookingInternalNote[] }>) {
  const [draft, setDraft] = useState("");
  const formatDate = useFormatDate();
  const addMutation = api.useMutation("post", "/api/bookings/{id}/notes", {
    onSuccess: () => {
      setDraft("");
      void queryClient.invalidateQueries();
      toast.success(t`Note added`);
    }
  });
  const deleteMutation = api.useMutation("delete", "/api/bookings/{id}/notes/{noteId}", {
    onSuccess: () => {
      void queryClient.invalidateQueries();
      toast.success(t`Note deleted`);
    }
  });
  const trimmed = draft.trim();

  return (
    <section className="rounded-md border p-4">
      <SectionTitle>
        <Trans>Internal notes</Trans>
      </SectionTitle>
      <GeneralApiErrors error={addMutation.error ?? deleteMutation.error} />
      {isLoading ? (
        <Skeleton className="h-10 w-full" />
      ) : notes.length === 0 ? (
        <span className="text-sm text-muted-foreground">
          <Trans>No internal notes yet.</Trans>
        </span>
      ) : (
        <ul className="flex flex-col gap-3">
          {notes.map((note) => (
            <li key={note.id} className="flex items-start justify-between gap-2 rounded-md border bg-muted/30 p-3">
              <div className="flex min-w-0 flex-col gap-1">
                <span className="text-sm break-words">{note.body}</span>
                <span className="text-xs text-muted-foreground">{formatDate(note.createdAt, true)}</span>
              </div>
              <Button
                type="button"
                variant="ghost"
                size="sm"
                aria-label={t`Delete note`}
                disabled={deleteMutation.isPending}
                onClick={() =>
                  deleteMutation.mutate({
                    params: { path: { id: bookingId, noteId: note.id as BookingInternalNoteId } }
                  })
                }
              >
                <Trash2Icon className="size-4" />
              </Button>
            </li>
          ))}
        </ul>
      )}
      <div className="mt-3 flex flex-col gap-2">
        <Textarea
          value={draft}
          placeholder={t`Add an internal note`}
          onChange={(event) => setDraft(event.currentTarget.value)}
        />
        <div className="flex justify-end">
          <Button
            type="button"
            size="sm"
            disabled={trimmed.length === 0}
            isPending={addMutation.isPending}
            onClick={() =>
              addMutation.mutate({ params: { path: { id: bookingId } }, body: { id: bookingId, body: trimmed } })
            }
          >
            <Trans>Add note</Trans>
          </Button>
        </div>
      </div>
    </section>
  );
}
