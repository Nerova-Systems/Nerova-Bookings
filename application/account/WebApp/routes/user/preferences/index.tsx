import { createFileRoute, redirect } from "@tanstack/react-router";

export const Route = createFileRoute("/user/preferences/")({
  beforeLoad: () => {
    throw redirect({ to: "/user/general" });
  }
});
