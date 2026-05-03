import { BarChart3Icon, BotIcon, CalendarDaysIcon, MonitorPlayIcon, VideoIcon } from "lucide-react";

import type { AppCategory } from "./appCatalog";

export const STORE_CATEGORIES: {
  name: AppCategory;
  label: string;
  description: string;
  Icon: typeof CalendarDaysIcon;
}[] = [
  { name: "Conferencing", label: "Conferencing", description: "3 apps", Icon: VideoIcon },
  { name: "AI & Automation", label: "Automation", description: "2 apps", Icon: BotIcon },
  { name: "Analytics", label: "Analytics", description: "1 app", Icon: BarChart3Icon },
  { name: "Other", label: "Other", description: "1 app", Icon: MonitorPlayIcon },
  { name: "Calendar", label: "Calendar", description: "2 apps", Icon: CalendarDaysIcon }
];

export const INSTALLED_CATEGORIES: AppCategory[] = [
  "Analytics",
  "AI & Automation",
  "Calendar",
  "Conferencing",
  "CRM",
  "Messaging",
  "Payment",
  "Other"
];
