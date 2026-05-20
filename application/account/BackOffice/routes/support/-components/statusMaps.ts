import type { MessageDescriptor } from "@lingui/core";

import { msg } from "@lingui/core/macro";

import { SupportTicketCategory, SupportTicketStatus } from "@/shared/lib/api/client";

export const staffStatusLabels: Record<SupportTicketStatus, MessageDescriptor> = {
  [SupportTicketStatus.New]: msg`New`,
  [SupportTicketStatus.AwaitingAgent]: msg`Awaiting agent`,
  [SupportTicketStatus.AwaitingUser]: msg`Awaiting user`,
  [SupportTicketStatus.AwaitingInternal]: msg`Awaiting internal`,
  [SupportTicketStatus.Resolved]: msg`Resolved`,
  [SupportTicketStatus.Closed]: msg`Closed`
};

type StatusPalette = {
  pillClass: string;
  dotClass: string;
  tileClass: string;
};

export const statusPalettes: Record<SupportTicketStatus, StatusPalette> = {
  [SupportTicketStatus.New]: {
    pillClass: "bg-info/10 text-info ring-info/25",
    dotClass: "bg-info",
    tileClass: "bg-info/10 text-info ring-info/25"
  },
  [SupportTicketStatus.AwaitingAgent]: {
    pillClass: "bg-warning/15 text-warning ring-warning/30",
    dotClass: "bg-warning",
    tileClass: "bg-warning/10 text-warning ring-warning/25"
  },
  [SupportTicketStatus.AwaitingUser]: {
    pillClass: "bg-primary/10 text-primary ring-primary/25",
    dotClass: "bg-primary",
    tileClass: "bg-primary/10 text-primary ring-primary/25"
  },
  [SupportTicketStatus.AwaitingInternal]: {
    pillClass: "bg-muted text-muted-foreground ring-border",
    dotClass: "bg-muted-foreground",
    tileClass: "bg-muted text-muted-foreground ring-border"
  },
  [SupportTicketStatus.Resolved]: {
    pillClass: "bg-success/10 text-success ring-success/25",
    dotClass: "bg-success",
    tileClass: "bg-success/10 text-success ring-success/25"
  },
  [SupportTicketStatus.Closed]: {
    pillClass: "bg-muted text-muted-foreground ring-border",
    dotClass: "bg-muted-foreground",
    tileClass: "bg-muted text-muted-foreground ring-border"
  }
};

export const categoryLabels: Record<SupportTicketCategory, MessageDescriptor> = {
  [SupportTicketCategory.Billing]: msg`Billing`,
  [SupportTicketCategory.Account]: msg`Account`,
  [SupportTicketCategory.HowTo]: msg`How-to`,
  [SupportTicketCategory.Bug]: msg`Bug`,
  [SupportTicketCategory.Feature]: msg`Feature`,
  [SupportTicketCategory.Feedback]: msg`Feedback`,
  [SupportTicketCategory.Other]: msg`Other`
};
