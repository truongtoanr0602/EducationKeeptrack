import { useCallback, useEffect, useRef, useState } from "react";
import { currentGeneration, peek, store } from "./cache";

// Fills the cache ahead of the screen that will read it. Nothing is stored if a write happened meanwhile.
export function prefetch<T>(key: string, fetcher: () => Promise<T>) {
  const started = currentGeneration();
  void fetcher().then(
    (value) => {
      if (started === currentGeneration()) store(key, value);
    },
    () => undefined,
  );
}

// Shows the cached value for `key` immediately (if any) and always revalidates from the server.
export function useSwr<T>(key: string, fetcher: () => Promise<T>) {
  const [data, setData] = useState<T | undefined>(() => peek<T>(key));
  const [error, setError] = useState<unknown>(null);
  const latest = useRef(fetcher);
  latest.current = fetcher;
  const active = useRef(key);
  active.current = key;
  const reload = useCallback(async () => {
    const started = currentGeneration();
    try {
      const value = await latest.current();
      if (active.current !== key) return;
      if (started === currentGeneration()) store(key, value);
      setData(value);
      setError(null);
    } catch (e) {
      if (active.current === key) setError(e);
    }
  }, [key]);
  useEffect(() => {
    setData(peek<T>(key));
    setError(null);
    void reload();
  }, [key, reload]);
  return { data, error, reload };
}
