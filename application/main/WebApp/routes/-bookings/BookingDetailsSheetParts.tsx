import type React from "react";

export function SectionTitle({ children }: Readonly<{ children: React.ReactNode }>) {
  return <h3 className="mb-3">{children}</h3>;
}

export function DetailRow({
  icon,
  label,
  children
}: Readonly<{ icon: React.ReactNode; label: React.ReactNode; children: React.ReactNode }>) {
  return (
    <div className="grid grid-cols-[1.25rem_1fr] gap-3">
      <span className="mt-0.5 text-muted-foreground [&_svg]:size-4">{icon}</span>
      <div className="flex min-w-0 flex-col gap-1">
        <span className="text-xs text-muted-foreground">{label}</span>
        <span className="text-sm break-words">{children}</span>
      </div>
    </div>
  );
}
