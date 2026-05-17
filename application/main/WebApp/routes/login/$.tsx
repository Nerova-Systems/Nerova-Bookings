import { createFileRoute } from "@tanstack/react-router";

import { AccountRouteBridge } from "../-account/AccountRouteBridge";

export const Route = createFileRoute("/login/$")({
  component: AccountRouteBridge
});
