import { loggedInPath } from "@repo/infrastructure/auth/constants";
import { createFileRoute, redirect } from "@tanstack/react-router";

import { LandingPageContent } from "./-components/LandingPageContent";

export const Route = createFileRoute("/")({
  beforeLoad: () => {
    const { isAuthenticated } = import.meta.user_info_env;
    if (isAuthenticated) {
      throw redirect({ to: loggedInPath });
    }
    return { disableAuthSync: true };
  },
  component: LandingPageContent
});
