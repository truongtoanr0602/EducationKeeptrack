import type { Page } from "@playwright/test";

// Screens are addressed as #/classes/<classId>/<section> (see src/router.ts).
export function classIdOf(page: Page): string {
  const match = /#\/classes\/([0-9a-f-]{36})/i.exec(page.url());
  if (!match) throw new Error(`No class in URL: ${page.url()}`);
  return match[1];
}

export async function openSection(page: Page, section: string) {
  const id = classIdOf(page);
  await page.evaluate(
    ([classId, name]) =>
      (window.location.hash = `#/classes/${classId}/${name}`),
    [id, section],
  );
}
