import { Card, CardContent, CardHeader } from "@repo/ui/components/Card";
import { Skeleton } from "@repo/ui/components/Skeleton";

export function ImportSkeleton() {
  return (
    <Card>
      <CardHeader>
        <Skeleton className="h-6 w-40" />
        <Skeleton className="h-4 w-64" />
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        <Skeleton className="h-12 w-full" />
        <Skeleton className="h-44 w-full" />
      </CardContent>
    </Card>
  );
}
