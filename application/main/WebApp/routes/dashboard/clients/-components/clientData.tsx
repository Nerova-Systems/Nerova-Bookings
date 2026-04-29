export type ClientFlag = "alert" | "overdue" | "blocked" | null;

export type Filter = "all" | "vip" | "new" | "blocked";

export const FILTER_LABELS: Record<Filter, string> = {
  all: "All clients",
  vip: "VIP",
  new: "New",
  blocked: "Blocked"
};

export function FlagDot({ flag }: { flag: ClientFlag }) {
  if (flag === "alert")
    return (
      <span className="inline-flex size-5 items-center justify-center rounded-full bg-warning/12 text-[11px] text-warning">
        !
      </span>
    );
  if (flag === "overdue")
    return (
      <span className="inline-flex size-5 items-center justify-center rounded-full bg-destructive/12 text-[11px] text-destructive">
        $
      </span>
    );
  if (flag === "blocked")
    return (
      <span className="inline-flex size-5 items-center justify-center rounded-full bg-black/8 text-[11px] text-muted-foreground">
        ✕
      </span>
    );
  return null;
}
