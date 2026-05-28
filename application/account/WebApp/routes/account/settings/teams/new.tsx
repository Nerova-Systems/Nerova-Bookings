import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { isFeatureFlagEnabled } from "@repo/infrastructure/featureFlags/useFeatureFlag";
import { AppLayout } from "@repo/ui/components/AppLayout";
import { Button } from "@repo/ui/components/Button";
import { Card, CardContent } from "@repo/ui/components/Card";
import { TextField } from "@repo/ui/components/TextField";
import { createFileRoute, redirect, useNavigate } from "@tanstack/react-router";
import { useState } from "react";
import { toast } from "sonner";

import { api } from "@/shared/lib/api/client";

// Convert a free-form team name into a URL-friendly slug suggestion.
function suggestSlug(name: string): string {
  return name
    .toLowerCase()
    .trim()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "");
}

export const Route = createFileRoute("/account/settings/teams/new")({
  beforeLoad: () => {
    if (!isFeatureFlagEnabled("tier-teams")) {
      throw redirect({ to: "/account/settings" });
    }
  },
  staticData: { trackingTitle: "New team" },
  component: NewTeamPage
});

function NewTeamPage() {
  const navigate = useNavigate();

  const [name, setName] = useState("");
  const [slug, setSlug] = useState("");
  const [slugEdited, setSlugEdited] = useState(false);

  const createTeamMutation = api.useMutation("post", "/api/account/teams");

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
        onSuccess: (created) => {
          toast.success(t`Team created: ${created.name}`);
          navigate({ to: "/account/settings/teams/$teamId", params: { teamId: created.id } });
        }
      }
    );
  };

  return (
    <AppLayout
      variant="center"
      maxWidth="32rem"
      title={t`New team`}
      subtitle={t`Pick a name and a URL slug. You can configure branding and invite members after creating the team.`}
    >
      <Card>
        <CardContent>
          <form onSubmit={handleSubmit}>
            <div className="flex flex-col gap-4">
              <TextField
                autoFocus={true}
                required={true}
                name="name"
                label={t`Team name`}
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
              <div className="flex justify-end">
                <Button type="submit" isPending={createTeamMutation.isPending}>
                  {createTeamMutation.isPending ? <Trans>Creating...</Trans> : <Trans>Create team</Trans>}
                </Button>
              </div>
            </div>
          </form>
        </CardContent>
      </Card>
    </AppLayout>
  );
}
