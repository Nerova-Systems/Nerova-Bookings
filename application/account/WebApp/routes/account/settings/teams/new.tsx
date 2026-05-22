import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { isFeatureFlagEnabled } from "@repo/infrastructure/featureFlags/useFeatureFlag";
import { AppLayout } from "@repo/ui/components/AppLayout";
import { Button } from "@repo/ui/components/Button";
import { Form } from "@repo/ui/components/Form";
import { Input } from "@repo/ui/components/Input";
import { Label } from "@repo/ui/components/Label";
import { useQueryClient } from "@tanstack/react-query";
import { createFileRoute, redirect, useNavigate } from "@tanstack/react-router";
import { useState } from "react";
import { toast } from "sonner";

import { api } from "@/shared/lib/api/client";

export const Route = createFileRoute("/account/settings/teams/new")({
  beforeLoad: () => {
    if (!isFeatureFlagEnabled("tier-teams")) {
      throw redirect({ to: "/account/settings" });
    }
  },
  staticData: { trackingTitle: "Create team" },
  component: NewTeamPage
});

// Convert a free-form team name into a URL-friendly slug suggestion. Users can override the value.
function suggestSlug(name: string): string {
  return name
    .toLowerCase()
    .trim()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "");
}

function NewTeamPage() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const [name, setName] = useState("");
  const [slug, setSlug] = useState("");
  const [slugEdited, setSlugEdited] = useState(false);

  const createTeamMutation = api.useMutation("post", "/api/account/teams", {
    meta: { skipQueryInvalidation: true }
  });

  const handleNameChange = (next: string) => {
    setName(next);
    if (!slugEdited) {
      setSlug(suggestSlug(next));
    }
  };

  const handleSubmit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const trimmedName = name.trim();
    const trimmedSlug = slug.trim();
    await createTeamMutation.mutateAsync(
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
          navigate({ to: "/account/settings/teams/$teamId", params: { teamId: created.id } });
        }
      }
    );
  };

  return (
    <AppLayout
      variant="center"
      maxWidth="64rem"
      title={t`Create team`}
      subtitle={t`Pick a name and a unique URL slug. You can configure branding and invite members after creating the team.`}
    >
      <Form onSubmit={handleSubmit} validationErrors={createTeamMutation.error?.errors} className="flex flex-col gap-6">
        <div className="flex flex-col gap-2">
          <Label htmlFor="team-name">
            <Trans>Name</Trans>
          </Label>
          <Input
            id="team-name"
            name="name"
            value={name}
            onChange={(e) => handleNameChange(e.target.value)}
            disabled={createTeamMutation.isPending}
            required={true}
            autoFocus={true}
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
            onChange={(e) => {
              setSlug(e.target.value);
              setSlugEdited(true);
            }}
            disabled={createTeamMutation.isPending}
            aria-label={t`Team slug`}
            placeholder={t`acme-design`}
          />
          <p className="text-sm text-muted-foreground">
            <Trans>The slug becomes part of your team's public URLs. Leave blank to auto-generate later.</Trans>
          </p>
        </div>

        <div className="flex justify-end gap-2">
          <Button
            type="button"
            variant="secondary"
            onClick={() => navigate({ to: "/account/settings/teams" })}
            disabled={createTeamMutation.isPending}
          >
            <Trans>Cancel</Trans>
          </Button>
          <Button type="submit" isPending={createTeamMutation.isPending}>
            {createTeamMutation.isPending ? <Trans>Creating...</Trans> : <Trans>Create team</Trans>}
          </Button>
        </div>

        {createTeamMutation.error && !createTeamMutation.error.errors && (
          <p className="text-sm text-destructive">
            <Trans>Failed to create team. Please try again.</Trans>
          </p>
        )}
      </Form>
    </AppLayout>
  );
}
