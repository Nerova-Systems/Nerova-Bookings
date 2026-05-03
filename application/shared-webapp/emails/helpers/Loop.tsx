import type { ReactNode } from "react";

import { getEmailRenderMode } from "./renderMode";

type LoopProps<T> = {
  path: string;
  sample: T[];
  children: (item: T, index: number) => ReactNode;
};

// Emits {{ for item in path }}...{{ end }} in build mode. The iteration variable is always named
// `item` — references to fields inside the loop body must use <Value path="item.field" sample="..." />.
// Scriban requires explicit binding for field access (no implicit `this` like Handlebars #each), so
// authors prepend `item.` to every field reference. In preview mode the loop iterates over `sample`
// so the React Email dev server renders realistic rows.
export function Loop<T>({ path, sample, children }: LoopProps<T>) {
  if (getEmailRenderMode() === "build") {
    return (
      <>
        {`{{ for item in ${path} }}`}
        {children(sample[0] as T, 0)}
        {"{{ end }}"}
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
