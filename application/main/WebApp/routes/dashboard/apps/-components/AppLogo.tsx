import type { AppCatalogItem } from "./appCatalog";

export function AppLogo({ app, size = "md" }: { app: AppCatalogItem; size?: "sm" | "md" | "lg" }) {
  const sizeClassName = size === "lg" ? "size-24 text-4xl" : size === "sm" ? "size-12 text-base" : "size-16 text-xl";
  return (
    <div
      className={`${sizeClassName} flex shrink-0 items-center justify-center rounded-[10px] font-display font-semibold shadow-sm ${app.logoClassName}`}
    >
      {app.logoText}
    </div>
  );
}
