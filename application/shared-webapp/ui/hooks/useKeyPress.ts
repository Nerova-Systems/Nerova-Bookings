import { useEffect, useState } from "react";

/**
 * Tracks whether a specific key is currently pressed.
 * Optionally scope event listeners to a specific input element via `ref`.
 * Fires an optional `handler` callback when the target key is pressed.
 *
 * Ported from cal.com `packages/lib/hooks/useKeyPress.ts` (cf2a55c).
 *
 * Deviation from Nerova `useKeyboardNavigation`: that hook is list-navigation oriented.
 * This hook is generic key-detection and is intentionally kept as a separate primitive.
 */
export function useKeyPress(
  targetKey: string,
  ref?: React.RefObject<HTMLInputElement | null>,
  handler?: () => void
): boolean {
  const [keyPressed, setKeyPressed] = useState(false);

  useEffect(() => {
    const element = ref?.current ?? window;

    const downHandler = ({ key }: { key: string }) => {
      if (key === targetKey) {
        setKeyPressed(true);
        handler?.();
      }
    };

    const upHandler = ({ key }: { key: string }) => {
      if (key === targetKey) {
        setKeyPressed(false);
      }
    };

    element.addEventListener("keydown", downHandler as EventListener);
    element.addEventListener("keyup", upHandler as EventListener);

    return () => {
      element.removeEventListener("keydown", downHandler as EventListener);
      element.removeEventListener("keyup", upHandler as EventListener);
    };
    // Intentionally omit deps to match cal.com behaviour (mount/unmount only).
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return keyPressed;
}
