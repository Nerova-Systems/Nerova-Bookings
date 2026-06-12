import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@repo/ui/components/Card";
import { Dropzone } from "@repo/ui/components/Dropzone";
import { UploadIcon } from "lucide-react";
import { toast } from "sonner";

import { api, queryClient } from "@/shared/lib/api/client";

import { importJobsQueryKey } from "./importHelpers";

export function UploadCard({ onJobStarted }: Readonly<{ onJobStarted: (jobId: string) => void }>) {
  const startMutation = api.useMutation("post", "/api/main/import-jobs", {
    onSuccess: (response) => {
      toast.success(t`We are checking your client list.`);
      onJobStarted(response.id);
      queryClient.invalidateQueries({ queryKey: importJobsQueryKey });
    }
  });

  const handleFile = (file: File) => {
    const reader = new FileReader();
    reader.onload = () => {
      startMutation.mutate({
        body: { fileName: file.name, fileContent: String(reader.result ?? "") }
      });
    };
    reader.readAsText(file);
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle>
          <Trans>Choose your client CSV</Trans>
        </CardTitle>
        <CardDescription>
          <Trans>We will read it here, then show what we found before importing clients.</Trans>
        </CardDescription>
      </CardHeader>
      <CardContent>
        <Dropzone
          accept={{ "text/csv": [".csv"], "text/plain": [".csv"] }}
          maxFiles={1}
          multiple={false}
          disabled={startMutation.isPending}
          onDrop={(files) => {
            const [file] = files;
            if (file) {
              handleFile(file);
            }
          }}
        >
          <UploadIcon className="size-8 text-muted-foreground" />
          <div className="text-sm font-medium">
            {startMutation.isPending ? (
              <Trans>Reading your file...</Trans>
            ) : (
              <Trans>Drop a CSV here, or click to choose one</Trans>
            )}
          </div>
        </Dropzone>
      </CardContent>
    </Card>
  );
}
