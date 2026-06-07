import { createFileRoute, useSearch } from "@tanstack/react-router";
import { z } from "zod";

const searchSchema = z.object({
  id: z.string().optional()
});

export const Route = createFileRoute("/data-deletion")({
  validateSearch: (search) => searchSchema.parse(search),
  component: DataDeletionPage
});

function DataDeletionPage() {
  const { id } = useSearch({ from: "/data-deletion" });

  return (
    <div className="flex min-h-screen items-center justify-center bg-background p-6">
      <div className="max-w-md text-center">
        <h1 className="mb-4 text-2xl font-semibold">Data Deletion Request</h1>
        <p className="mb-4 text-muted-foreground">
          Your data deletion request has been received and processed. We do not retain any personal data associated with
          your Facebook account.
        </p>
        {id && (
          <p className="text-sm text-muted-foreground">
            Confirmation code: <span className="font-mono font-medium">{id}</span>
          </p>
        )}
      </div>
    </div>
  );
}
