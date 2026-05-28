import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import {
  Dialog,
  DialogBody,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogForm,
  DialogHeader,
  DialogTitle
} from "@repo/ui/components/Dialog";
import { TextField } from "@repo/ui/components/TextField";
import { useQueryClient } from "@tanstack/react-query";
import { useNavigate } from "@tanstack/react-router";
import { useEffect, useState } from "react";
import { toast } from "sonner";

import { api } from "@/shared/lib/api/client";

// Convert a free-form team name into a URL-friendly slug suggestion. Users can override the value.
function suggestSlug(name: string): string {
  return name
    .toLowerCase()
    .trim()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "");
}

interface CreateTeamDialogProps {
  isOpen: boolean;
  onOpenChange: (isOpen: boolean) => void;
}

export function CreateTeamDialog({ isOpen, onOpenChange }: Readonly<CreateTeamDialogProps>) {
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const [name, setName] = useState("");
  const [slug, setSlug] = useState("");
  const [slugEdited, setSlugEdited] = useState(false);

  const createTeamMutation = api.useMutation("post", "/api/account/teams", {
    meta: { skipQueryInvalidation: true }
  });

  // Reset form each time the dialog reopens.
  useEffect(() => {
    if (isOpen) {
      setName("");
      setSlug("");
      setSlugEdited(false);
      createTeamMutation.reset();
    }
    // createTeamMutation.reset identity is stable per mutation instance — safe to omit.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isOpen]);

  const handleNameChange = (next: string) => {
    setName(next);
    if (!slugEdited) {
      setSlug(suggestSlug(next));
    }
  };

  const handleSubmit = (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const trimmedName = name.trim();
    const trimmedSlug = slug.trim();
    createTeamMutation.mutate(
      {
        body: {
          name: trimmedName,
          slug: trimmedSlug === "" ? null : trimmedSlug
        }
      },
      {
        onSuccess: async (created) => {
          await queryClient.invalidateQueries({
            predicate: (query) => Array.isArray(query.queryKey) && query.queryKey[1] === "/api/account/teams"
          });
          toast.success(t`Team created: ${created.name}`);
          onOpenChange(false);
          navigate({ to: "/account/settings/teams/$teamId", params: { teamId: created.id } });
        }
      }
    );
  };

  return (
    <Dialog trackingTitle="Create team" open={isOpen} onOpenChange={onOpenChange}>
      <DialogContent className="sm:w-dialog-md">
        <DialogHeader>
          <DialogTitle>
            <Trans>Create team</Trans>
          </DialogTitle>
          <DialogDescription>
            <Trans>
              Pick a name and a URL slug. You can configure branding and invite members after creating the team.
            </Trans>
          </DialogDescription>
        </DialogHeader>
        <DialogForm onSubmit={handleSubmit} validationErrors={createTeamMutation.error?.errors}>
          <DialogBody>
            <TextField
              autoFocus={true}
              required={true}
              name="name"
              label={t`Name`}
              value={name}
              onChange={handleNameChange}
              disabled={createTeamMutation.isPending}
            />
            <TextField
              name="slug"
              label={t`Slug`}
              value={slug}
              onChange={(value) => {
                setSlug(value);
                setSlugEdited(true);
              }}
              disabled={createTeamMutation.isPending}
              placeholder={t`acme-design`}
              description={t`Becomes part of your team's public URLs. Leave blank to auto-generate later.`}
            />
          </DialogBody>
          <DialogFooter>
            <DialogClose render={<Button type="reset" variant="secondary" disabled={createTeamMutation.isPending} />}>
              <Trans>Cancel</Trans>
            </DialogClose>
            <Button type="submit" isPending={createTeamMutation.isPending}>
              {createTeamMutation.isPending ? <Trans>Creating...</Trans> : <Trans>Create team</Trans>}
            </Button>
          </DialogFooter>
        </DialogForm>
      </DialogContent>
    </Dialog>
  );
}
