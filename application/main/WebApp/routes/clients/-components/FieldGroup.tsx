import { AlertTriangleIcon } from "lucide-react";

export function FieldGroup({
  title,
  tone,
  children
}: Readonly<{ title: string; tone?: "warning"; children: React.ReactNode }>) {
  return (
    <div
      className={
        tone === "warning"
          ? "rounded-md border border-warning/40 bg-warning/10 p-3"
          : "rounded-md border bg-background p-3"
      }
    >
      <div className="mb-3 flex items-center gap-2">
        {tone === "warning" && <AlertTriangleIcon className="size-4 text-warning" />}
        <h4 className="text-sm font-medium">{title}</h4>
      </div>
      <div className="space-y-3">{children}</div>
    </div>
  );
}
