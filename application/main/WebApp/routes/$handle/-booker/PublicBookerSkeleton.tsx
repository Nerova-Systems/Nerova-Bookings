import { Skeleton } from "@repo/ui/components/Skeleton";

export function PublicBookerSkeleton() {
  return (
    <div className="mx-auto grid max-w-[70rem] gap-0 overflow-hidden rounded-lg border bg-background shadow-sm lg:grid-cols-[20rem_1fr]">
      <div className="flex flex-col gap-5 p-6">
        <Skeleton className="size-12 rounded-full" />
        <Skeleton className="h-8 w-3/4" />
        <Skeleton className="h-20 w-full" />
      </div>
      <div className="grid min-h-[38rem] border-t p-6 lg:border-t-0 lg:border-l">
        <Skeleton className="h-full min-h-[28rem] w-full" />
      </div>
    </div>
  );
}
