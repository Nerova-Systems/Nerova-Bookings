import { useCallback, useEffect, useMemo } from "react";
import { z } from "zod";

import { useRouterQuery } from "./useRouterQuery";

type OptionalKeys<T> = {
  [K in keyof T]-?: Record<string, unknown> extends Pick<T, K> ? K : never;
}[keyof T];

type FilteredKeys<T, U> = {
  [K in keyof T as T[K] extends U ? K : never]: T[K];
};

/** Accepts comma-separated string, number, or array of numbers and returns number[]. */
export const queryNumberArray = z
  .string()
  .or(z.number())
  .or(z.array(z.number()))
  .transform((a) => {
    if (typeof a === "string") return a.split(",").map((v) => Number(v));
    if (Array.isArray(a)) return a;
    return [a];
  });

/** Accepts comma-separated string or string[] and returns string[]. */
export const queryStringArray = z
  .preprocess((a) => z.string().parse(a).split(","), z.string().array())
  .or(z.string().array());

/**
 * Typed URL query-string hook backed by a Zod schema.
 * Reads the current URL search params, parses them with the schema, and exposes
 * helpers to mutate individual keys via `history.replaceState` (no hard reload).
 *
 * Ported from cal.com `packages/lib/hooks/useTypedQuery.ts` (cf2a55c).
 *
 * Deviation: cal.com uses Next.js `useRouter` + `usePathname`. Nerova uses browser
 * `history.replaceState` directly so the hook is router-agnostic and SSR-safe.
 */
export function useTypedQuery<T extends z.ZodObject<z.ZodRawShape>>(schema: T) {
  type Output = z.infer<typeof schema>;
  type FullOutput = Required<Output>;
  type OutputKeys = Required<keyof FullOutput>;
  type OutputOptionalKeys = OptionalKeys<Output>;
  type ArrayOutput = FilteredKeys<FullOutput, Array<unknown>>;
  type ArrayOutputKeys = keyof ArrayOutput;

  const unparsedQuery = useRouterQuery();
  const pathname = typeof window !== "undefined" ? window.location.pathname : "";
  const parsedQuerySchema = schema.safeParse(unparsedQuery);

  let parsedQuery: Output = useMemo(() => ({}) as Output, []);

  const replaceQuery = useCallback((search: URLSearchParams) => {
    const newUrl = `${window.location.pathname}?${search.toString()}`;
    history.replaceState(history.state, "", newUrl);
    // Fire popstate so useCompatSearchParams subscribers update.
    window.dispatchEvent(new PopStateEvent("popstate", { state: history.state }));
  }, []);

  // Initialise defaults for schema keys that are absent from the URL.
  useEffect(() => {
    if (parsedQuerySchema.success && parsedQuerySchema.data) {
      const search = new URLSearchParams(window.location.search);
      let mutated = false;
      for (const [key, value] of Object.entries(parsedQuerySchema.data)) {
        if (!(key in unparsedQuery) && value !== undefined) {
          search.set(key, String(value));
          mutated = true;
        }
      }
      if (mutated) replaceQuery(search);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  if (parsedQuerySchema.success) {
    parsedQuery = parsedQuerySchema.data;
  } else {
    console.error(parsedQuerySchema.error);
  }

  const setQuery = useCallback(
    <J extends OutputKeys>(key: J, value: Output[J]) => {
      const search = new URLSearchParams(window.location.search);
      search.set(String(key), String(value));
      replaceQuery(search);
    },
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [pathname, replaceQuery]
  );

  function removeByKey(key: OutputOptionalKeys) {
    const search = new URLSearchParams(window.location.search);
    search.delete(String(key));
    replaceQuery(search);
  }

  function pushItemToKey<J extends ArrayOutputKeys>(key: J, value: ArrayOutput[J] extends Array<infer I> ? I : never) {
    const existingValue = parsedQuery[key];
    if (Array.isArray(existingValue)) {
      if (existingValue.includes(value)) return;
      setQuery(key as OutputKeys, [...existingValue, value] as Output[typeof key]);
    } else {
      setQuery(key as OutputKeys, [value] as Output[typeof key]);
    }
  }

  function removeItemByKeyAndValue<J extends ArrayOutputKeys>(
    key: J,
    value: ArrayOutput[J] extends Array<infer I> ? I : never
  ) {
    const existingValue = parsedQuery[key];
    if (Array.isArray(existingValue) && existingValue.length > 1) {
      setQuery(key as OutputKeys, existingValue.filter((item: unknown) => item !== value) as Output[typeof key]);
    } else {
      removeByKey(key as unknown as OutputOptionalKeys);
    }
  }

  function removeAllQueryParams() {
    history.replaceState(history.state, "", pathname);
    window.dispatchEvent(new PopStateEvent("popstate", { state: history.state }));
  }

  return {
    data: parsedQuery,
    setQuery,
    removeByKey,
    pushItemToKey,
    removeItemByKeyAndValue,
    removeAllQueryParams
  };
}
