import type { ReactNode } from "react";

import { getEmailRenderMode } from "./renderMode";

type LoopProps<T> = {
  path: string;
  sample: T[];
  children: (item: T, index: number) => ReactNode;
};

// Emits {{#each path}}...{{/each}} in build mode. In preview mode iterates over `sample` so the
// dev server renders realistic rows. The render function is invoked once with `this` to capture the
// per-item markup; field references inside should use <Value path="field" .../> (Handlebars treats
// {{field}} inside #each as relative to the current item).
export function Loop<T>({ path, sample, children }: LoopProps<T>) {
  if (getEmailRenderMode() === "build") {
    return (
      <>
        {`{{#each ${path}}}`}
        {children(sample[0] as T, 0)}
        {"{{/each}}"}
      </>
    );
  }

  return (
    <>
      {sample.map((item, index) => (
        <span key={index}>{children(item, index)}</span>
      ))}
    </>
  );
}
