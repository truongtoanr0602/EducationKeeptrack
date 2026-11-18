// Tiny stale-while-revalidate store for read screens. Entries live only in memory (gone on reload) and
// are dropped as a whole after any successful write, so a screen never shows data older than the
// teacher's own last change while still painting instantly from the previous visit.
const entries = new Map<string, unknown>();
let generation = 0;

export const peek = <T>(key: string) => entries.get(key) as T | undefined;
export const store = (key: string, value: unknown) => {
  entries.set(key, value);
};
export function clearCache() {
  entries.clear();
  generation++;
}
// A response that started before a write must not be stored after it.
export const currentGeneration = () => generation;
