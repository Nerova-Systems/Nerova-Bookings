import { useEffect, useLayoutEffect } from "react";

/**
 * Runs `useLayoutEffect` on the client and falls back to `useEffect` on the server
 * to avoid the "Warning: useLayoutEffect does nothing on the server" message.
 *
 * Ported from cal.com `packages/lib/hooks/useIsomorphicLayoutEffect.ts` (cf2a55c).
 */
export const useIsomorphicLayoutEffect = typeof document !== "undefined" ? useLayoutEffect : useEffect;
