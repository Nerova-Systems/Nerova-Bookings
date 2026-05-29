import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import {
  DialogBody,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogForm
} from "@repo/ui/components/Dialog";
import { DirtyDialog } from "@repo/ui/components/DirtyDialog";
import { useDialogSetDirty } from "@repo/ui/components/DirtyDialogContext";
import { Input } from "@repo/ui/components/Input";
import { Label } from "@repo/ui/components/Label";
import { Textarea } from "@repo/ui/components/Textarea";
import { SettingsToggle } from "@repo/ui/components/SettingsToggle";
import { useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { toast } from "sonner";

import { api, type Schemas } from "@/shared/lib/api/client";

type TeamResponse = Schemas["TeamResponse"];

interface EditTeamDialogProps {
  team: TeamResponse | null;
  isOpen: boolean;
  onOpenChange: (isOpen: boolean) => void;
}

export function EditTeamDialog({ team, isOpen, onOpenChange }: Readonly<EditTeamDialogProps>) {
  if (!team) {
    return null;
  }

  return (
    <DirtyDialog open={isOpen} onOpenChange={onOpenChange} trackingTitle="Edit team">
      <DialogContent className="sm:w-dialog-md">
        <DialogHeader>
          <DialogTitle>
            <Trans>Edit team - {team.name}</Trans>
          </DialogTitle>
          <DialogDescription>
            <Trans>Update the team's profile information, visibility, and branding settings.</Trans>
          </DialogDescription>
        </DialogHeader>
        <EditTeamDialogBody team={team} onClose={() => onOpenChange(false)} />
      </DialogContent>
    </DirtyDialog>
  );
}

function EditTeamDialogBody({ team, onClose }: { team: TeamResponse; onClose: () => void }) {
  const setDirty = useDialogSetDirty();
  const queryClient = useQueryClient();

  const [name, setName] = useState(team.name);
  const [slug, setSlug] = useState(team.slug ?? "");
  const [bio, setBio] = useState(team.bio ?? "");
  const [isPrivate, setIsPrivate] = useState(team.isPrivate);
  const [hideBranding, setHideBranding] = useState(team.hideBranding);
  const [hideBookATeamMember, setHideBookATeamMember] = useState(team.hideBookATeamMember);
  const [hideTeamProfileLink, setHideTeamProfileLink] = useState(team.hideTeamProfileLink);

  const updateMutation = api.useMutation("put", "/api/account/teams/{id}", {
    meta: { skipQueryInvalidation: true }
  });

  const handleSubmit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const trimmedSlug = slug.trim();
    const trimmedBio = bio.trim();
    await updateMutation.mutateAsync(
      {
        params: { path: { id: team.id } },
        body: {
          name: name.trim(),
          slug: trimmedSlug === "" ? null : trimmedSlug,
          bio: trimmedBio === "" ? null : trimmedBio,
          isPrivate,
          hideBranding,
          hideBookATeamMember,
          hideTeamProfileLink
        }
      },
      {
        onSuccess: async (updated) => {
          await queryClient.invalidateQueries({
            predicate: (query) => Array.isArray(query.queryKey) && query.queryKey[1] === "/api/account/teams"
          });
          await queryClient.invalidateQueries({
            predicate: (query) => Array.isArray(query.queryKey) && query.queryKey[1] === "/api/account/teams/{id}"
          });
          toast.success(t`Team updated: ${updated.name}`);
          onClose();
        }
      }
    );
  };

  return (
    <DialogForm onSubmit={handleSubmit} validationErrors={updateMutation.error?.errors}>
      <DialogBody className="flex flex-col gap-4 max-h-[30rem] overflow-y-auto pr-1">
        <div className="flex flex-col gap-2">
          <Label htmlFor="edit-team-name">
            <Trans>Name</Trans>
          </Label>
          <Input
            id="edit-team-name"
            value={name}
            onChange={(e) => {
              setName(e.target.value);
              setDirty(true);
            }}
            disabled={updateMutation.isPending}
            required
          />
        </div>

        <div className="flex flex-col gap-2">
          <Label htmlFor="edit-team-slug">
            <Trans>Slug</Trans>
          </Label>
          <Input
            id="edit-team-slug"
            value={slug}
            onChange={(e) => {
              setSlug(e.target.value);
              setDirty(true);
            }}
            disabled={updateMutation.isPending}
          />
        </div>

        <div className="flex flex-col gap-2">
          <Label htmlFor="edit-team-bio">
            <Trans>About</Trans>
          </Label>
          <Textarea
            id="edit-team-bio"
            value={bio}
            onChange={(e) => {
              setBio(e.target.value);
              setDirty(true);
            }}
            disabled={updateMutation.isPending}
            rows={3}
          />
        </div>

        <div className="border-t border-border/50 pt-4 flex flex-col gap-4">
          <h4 className="text-sm font-semibold tracking-tight text-foreground">
            <Trans>Visibility & branding</Trans>
          </h4>
          <SettingsToggle
            title={<Trans>Private team</Trans>}
            description={<Trans>Hide the team's member list and profile page from public visitors.</Trans>}
            checked={isPrivate}
            onCheckedChange={(val) => {
              setIsPrivate(val);
              setDirty(true);
            }}
            disabled={updateMutation.isPending}
          />
          <SettingsToggle
            title={<Trans>Hide branding</Trans>}
            description={<Trans>Hide the "Powered by" footer on booking pages.</Trans>}
            checked={hideBranding}
            onCheckedChange={(val) => {
              setHideBranding(val);
              setDirty(true);
            }}
            disabled={updateMutation.isPending}
          />
          <SettingsToggle
            title={<Trans>Hide "Book a team member"</Trans>}
            description={<Trans>Hide the option to book any available member from the team profile page.</Trans>}
            checked={hideBookATeamMember}
            onCheckedChange={(val) => {
              setHideBookATeamMember(val);
              setDirty(true);
            }}
            disabled={updateMutation.isPending}
          />
          <SettingsToggle
            title={<Trans>Hide team profile link</Trans>}
            description={<Trans>Remove the link back to the team profile from individual member pages.</Trans>}
            checked={hideTeamProfileLink}
            onCheckedChange={(val) => {
              setHideTeamProfileLink(val);
              setDirty(true);
            }}
            disabled={updateMutation.isPending}
          />
        </div>
      </DialogBody>
      <DialogFooter>
        <DialogClose render={<Button type="reset" variant="secondary" disabled={updateMutation.isPending} />}>
          <Trans>Cancel</Trans>
        </DialogClose>
        <Button type="submit" isPending={updateMutation.isPending}>
          {updateMutation.isPending ? <Trans>Saving...</Trans> : <Trans>Save changes</Trans>}
        </Button>
      </DialogFooter>
    </DialogForm>
  );
}
