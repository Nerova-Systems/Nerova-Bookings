import type { AppCatalogItem } from "./appCatalog";

export function AppPreview({ app }: { app: AppCatalogItem }) {
  return (
    <div className={`relative overflow-hidden rounded-lg bg-gradient-to-br ${app.accentClassName} p-6`}>
      <div className="rounded-md border border-white/15 bg-white/92 p-4 text-neutral-900 shadow-xl">
        <div className="mb-4 flex items-center gap-2 border-b border-neutral-200 pb-3">
          <div className="size-7 rounded bg-neutral-900/85" />
          <div className="h-2.5 w-28 rounded-full bg-neutral-300" />
          <div className="ml-auto h-2.5 w-16 rounded-full bg-neutral-200" />
        </div>
        <div className="grid grid-cols-[4.5rem_repeat(5,minmax(0,1fr))] gap-px overflow-hidden rounded border border-neutral-200 bg-neutral-200">
          {Array.from({ length: 36 }, (_, index) => (
            <div key={index} className="h-9 bg-white" />
          ))}
        </div>
        <div className="absolute top-[44%] left-[34%] h-8 w-36 rounded bg-blue-500/80" />
        <div className="absolute top-[58%] left-[54%] h-8 w-32 rounded bg-emerald-500/80" />
      </div>
    </div>
  );
}
