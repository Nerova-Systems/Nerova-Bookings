import { createFileRoute } from "@tanstack/react-router";

import { AccountRouteBridge } from "../-account/AccountRouteBridge";

export const Route = createFileRoute("/channels/$")({
  component: AccountRouteBridge
});
