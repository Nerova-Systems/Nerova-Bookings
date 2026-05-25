import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { Form } from "@repo/ui/components/Form";
import { Input } from "@repo/ui/components/Input";
import { Label } from "@repo/ui/components/Label";
import { Section } from "@repo/ui/components/Section";
import { SettingsToggle } from "@repo/ui/components/SettingsToggle";
import { Textarea } from "@repo/ui/components/Textarea";
import { useQueryClient } from "@tanstack/react-query";
import { Trash2Icon } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";

import { api, type Schemas } from "@/shared/lib/api/client";

type TeamResponse = Schemas["TeamResponse"];

interface TeamGeneralTabProps {
  team: TeamResponse;
  canManage: boolean;
  canDelete: boolean;
  onDelete: () => void;
}

export function TeamGeneralTab({ team, canManage, canDelete, onDelete }: Readonly<TeamGeneralTabProps>) {
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
        }
      }
    );
  };

  const readOnly = !canManage;
  const disabled = readOnly || updateMutation.isPending;

  return (
    <Form onSubmit={handleSubmit} validationErrors={updateMutation.error?.errors} className="flex flex-col gap-6">
      <Section title={t`Profile`} description={t`Public information shown on your team's booking pages.`}>
        <div className="flex flex-col gap-2">
          <Label htmlFor="team-name">
            <Trans>Name</Trans>
          </Label>
          <Input
            id="team-name"
            name="name"
            value={name}
            onChange={(e) => setName(e.target.value)}
            disabled={disabled}
            required={true}
            aria-label={t`Team name`}
          />
        </div>

        <div className="flex flex-col gap-2">
          <Label htmlFor="team-slug">
            <Trans>Slug</Trans>
          </Label>
          <Input
            id="team-slug"
            name="slug"
            value={slug}
            onChange={(e) => setSlug(e.target.value)}
            disabled={disabled}
            aria-label={t`Team slug`}
          />
        </div>

        <div className="flex flex-col gap-2">
          <Label htmlFor="team-bio">
            <Trans>About</Trans>
          </Label>
          <Textarea
            id="team-bio"
            name="bio"
            value={bio}
            onChange={(e) => setBio(e.target.value)}
            disabled={disabled}
            rows={4}
            aria-label={t`Team description`}
          />
        </div>
      </Section>

      <Section title={t`Visibility & branding`} description={t`Control how your team appears to bookers.`}>
        <SettingsToggle
          title={<Trans>Private team</Trans>}
          description={<Trans>Hide the team's member list and profile page from public visitors.</Trans>}
          checked={isPrivate}
          onCheckedChange={setIsPrivate}
          disabled={disabled}
        />
        <SettingsToggle
          title={<Trans>Hide branding</Trans>}
          description={<Trans>Hide the "Powered by" footer on booking pages.</Trans>}
          checked={hideBranding}
          onCheckedChange={setHideBranding}
          disabled={disabled}
        />
        <SettingsToggle
          title={<Trans>Hide "Book a team member"</Trans>}
          description={<Trans>Hide the option to book any available member from the team profile page.</Trans>}
          checked={hideBookATeamMember}
          onCheckedChange={setHideBookATeamMember}
          disabled={disabled}
        />
        <SettingsToggle
          title={<Trans>Hide team profile link</Trans>}
          description={<Trans>Remove the link back to the team profile from individual member pages.</Trans>}
          checked={hideTeamProfileLink}
          onCheckedChange={setHideTeamProfileLink}
          disabled={disabled}
        />
      </Section>

      {!readOnly && (
        <div className="flex justify-end">
          <Button type="submit" isPending={updateMutation.isPending}>
            {updateMutation.isPending ? <Trans>Saving...</Trans> : <Trans>Save changes</Trans>}
          </Button>
        </div>
      )}

      {canDelete && (
        <Section title={t`Danger zone`} description={t`Deleting a team is permanent and cannot be undone.`}>
          <div>
            <Button type="button" variant="destructive" onClick={onDelete}>
              <Trash2Icon />
              <Trans>Delete team</Trans>
            </Button>
          </div>
        </Section>
      )}
    </Form>
  );
}
