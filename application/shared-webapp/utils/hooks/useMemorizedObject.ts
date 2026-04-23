import { useRef } from "react";

/**
 * Use a memorized object to prevent unnecessary re-renders.
 * Returns the same reference as long as the content hash is unchanged.
 */
export function useMemorizedObject<T>(value: T): T {
  const contentHash = JSON.stringify(value);
  const ref = useRef<{ hash: string; value: T }>({ hash: contentHash, value });
  if (ref.current.hash !== contentHash) {
    ref.current = { hash: contentHash, value };
  }
  return ref.current.value;
}
