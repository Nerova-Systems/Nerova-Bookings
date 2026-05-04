// SAFETY NOTE — DO NOT REMOVE OR DRIFT FROM @lingui/babel-plugin-lingui-macro
//
// Why this file exists:
//   The email build runs templates in Node via tsx (esbuild). esbuild does NOT execute Lingui's
//   babel/SWC `Trans` macro the way the SPA's rsbuild + @lingui/swc-plugin pipeline does, so a
//   template that imports `@lingui/react/macro` would crash at runtime — the shipped shim throws
//   "macro executed outside compilation" when loaded raw.
//
//   `application/shared-webapp/emails/tsconfig.json` aliases `@lingui/react/macro` -> this file.
//   tsx respects the tsconfig `paths` mapping, so every email-template import of `Trans` from
//   `@lingui/react/macro` resolves here. Lingui's CLI extractor STILL runs the real Babel macro at
//   extract time (it walks file source via `@lingui/babel-plugin-lingui-macro` regardless of how
//   modules resolve at runtime), so the .po `msgid` set we ship is identical to what the SPA build
//   would produce for the same templates.
//
// MUST stay byte-for-byte aligned with the macro's id-generation algorithm:
//   The macro hashes `${message}` (and optional `context`) into a stable id via
//   `@lingui/message-utils/generateMessageId`. This wrapper imports the SAME function and feeds it
//   the SAME serialized message string the macro would build (literal text + indexed `<N/>` /
//   `<N>...</N>` placeholders for inline JSX, `{N}` for non-element values). If either side drifts:
//
//     - The runtime id won't match the catalog id Lingui CLI extracted, so `<Trans>` will fall back
//       to its `message` prop (the source English) for EVERY locale. Translations silently stop
//       being applied. Build still succeeds; emails ship in English regardless of the recipient's
//       locale. Hard to notice in code review.
//
//   If the macro's algorithm ever changes upstream (semver-major in @lingui/message-utils, or a new
//   placeholder convention in @lingui/babel-plugin-lingui-macro), THIS FILE MUST CHANGE TOO. After
//   any Lingui upgrade, run `dotnet run --project developer-cli -- build --emails --quiet` and
//   verify a non-English Demo.<locale>.txt actually shows translated copy (not just the English
//   fallback). The Demo template lives at `application/shared-webapp/emails/showcase/Demo.tsx` and
//   doubles as the canary for this contract.
import type { ReactElement, ReactNode } from "react";

import { generateMessageId } from "@lingui/message-utils/generateMessageId";
import { Trans as RuntimeTrans } from "@lingui/react";
import { Children, isValidElement } from "react";

type TransProps = {
  id?: string;
  context?: string;
  children: ReactNode;
};

export function Trans({ id, context, children }: Readonly<TransProps>): ReactElement | null {
  const { message, values, components } = serializeChildren(children);
  const finalId = id ?? generateMessageId(message, context);
  return <RuntimeTrans id={finalId} message={message} values={values} components={components} />;
}

function serializeChildren(children: ReactNode): {
  message: string;
  values: Record<string, unknown>;
  components: Record<string, ReactElement>;
} {
  const values: Record<string, unknown> = {};
  const components: Record<string, ReactElement> = {};
  // Single counter shared across recursion depths. Lingui's babel macro assigns placeholder indices
  // globally across the whole <Trans> tree (so a <strong><Value/></strong> tree extracts as
  // `<0><1/></0>`, not `<0><0/></0>`). Using a fresh counter per recursion would diverge from the
  // macro's id-generation algorithm — `generateMessageId` would hash a different string, the catalog
  // lookup would silently miss for every nested-element template, and components stored at colliding
  // keys would render swapped content (e.g. <Link> children replaced by the <Value> render).
  const counter = { next: 0 };

  function visit(nodes: ReactNode): string {
    const parts: string[] = [];
    Children.forEach(nodes, (child) => {
      if (typeof child === "string" || typeof child === "number") {
        parts.push(String(child));
        return;
      }

      if (isValidElement(child)) {
        const componentIndex = String(counter.next++);
        components[componentIndex] = child;
        const elementChildren = (child.props as { children?: ReactNode }).children;
        if (elementChildren === undefined || elementChildren === null) {
          parts.push(`<${componentIndex}/>`);
        } else {
          parts.push(`<${componentIndex}>${visit(elementChildren)}</${componentIndex}>`);
        }
        return;
      }

      if (child === null || child === undefined || typeof child === "boolean") return;

      // Treat any remaining renderable (objects, arrays) as opaque values via {0}, {1}, ... placeholders.
      const valueKey = String(counter.next++);
      values[valueKey] = child;
      parts.push(`{${valueKey}}`);
    });
    return parts.join("");
  }

  return { message: visit(children), values, components };
}
