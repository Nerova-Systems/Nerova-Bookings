import type { ReactNode } from "react";

import { Children, isValidElement } from "react";

import { getEmailRenderMode } from "./renderMode";

type IfProps = {
  path: string;
  sample: boolean;
  children: ReactNode;
};

type ElseProps = {
  children: ReactNode;
};

// Emits {{ if path }}...{{ else }}...{{ end }} in build mode and selects the matching branch in
// preview mode. The truthy branch is everything outside <Else>, and the falsy branch is the
// (optional) <Else> child. Authors write a single <If> with a nested <Else> sibling rather than a
// paired component.
export function If({ path, sample, children }: IfProps) {
  const { truthy, falsy } = splitElseBranch(children);

  if (getEmailRenderMode() === "build") {
    return (
      <>
        {`{{ if ${path} }}`}
        {truthy}
        {falsy === null ? null : (
          <>
            {"{{ else }}"}
            {falsy}
          </>
        )}
        {"{{ end }}"}
      </>
    );
  }

  return <>{sample ? truthy : (falsy ?? null)}</>;
}

export function Else({ children }: ElseProps) {
  return <>{children}</>;
}

function splitElseBranch(children: ReactNode): { truthy: ReactNode[]; falsy: ReactNode | null } {
  const truthy: ReactNode[] = [];
  let falsy: ReactNode | null = null;
  Children.forEach(children, (child) => {
    if (isValidElement(child) && child.type === Else) {
      falsy = (child.props as ElseProps).children;
      return;
    }

    truthy.push(child);
  });
  return { truthy, falsy };
}
