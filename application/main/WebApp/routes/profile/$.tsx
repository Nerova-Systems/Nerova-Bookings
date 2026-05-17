import { createFileRoute } from "@tanstack/react-router";

import { AccountRouteBridge } from "../-account/AccountRouteBridge";

export const Route = createFileRoute("/profile/$")({
  component: AccountRouteBridge
});
