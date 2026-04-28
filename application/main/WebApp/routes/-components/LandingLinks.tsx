import { Link } from "@repo/ui/components/Link";
import { ArrowRightIcon } from "lucide-react";

export function TrialLink({ children = "Start free trial" }: { readonly children?: string }) {
  return (
    <Link href="/signup" variant="button-primary" underline={false} className="h-12 px-6">
      {children}
      <ArrowRightIcon className="size-4" />
    </Link>
  );
}
