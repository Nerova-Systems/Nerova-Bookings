import { useEffect, useRef } from "react";

/**
 * **Development-only** utility that logs which props changed between renders.
 * Has no effect in production (the body is a no-op when you remove `console.log`
 * calls via a build plugin).
 *
 * Ported from cal.com `packages/lib/hooks/useTraceUpdate.ts` (cf2a55c).
 */
export function useTraceUpdate(props: Record<string, unknown>): void {
  const prev = useRef(props);

  useEffect(() => {
    const changedProps = Object.entries(props).reduce<Record<string, [unknown, unknown]>>((ps, [k, v]) => {
      if (prev.current[k] !== v) {
        ps[k] = [prev.current[k], v];
      }
      return ps;
    }, {});

    if (Object.keys(changedProps).length > 0) {
      console.log("Changed props:", changedProps);
    }

    prev.current = props;
  });
}
