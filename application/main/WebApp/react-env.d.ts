/// <reference types="@repo/build/react-env.d.ts" />
/// <reference types="@repo/build/module-federation-types/account.d.ts" />

declare module "account/whatsapp/SetupTab" {
  import type { FC } from "react";
  export const SetupTab: FC;
}

declare module "account/whatsapp/ProfileTab" {
  import type { FC } from "react";
  export const ProfileTab: FC;
}

declare module "account/whatsapp/WorkflowsTab" {
  import type { FC } from "react";
  export const WorkflowsTab: FC;
}

declare module "account/whatsapp/UsageTab" {
  import type { FC } from "react";
  export const UsageTab: FC;
}
