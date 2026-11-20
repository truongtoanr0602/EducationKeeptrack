import type { Locator, Page } from "@playwright/test";

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

// The payments screen shows the receipt form in a dialog opened from the student's card. While it is open the
// rest of the page is inert, so every field, the submit button and "Tải lại công nợ" are looked up inside it.
export async function openReceiptForm(card: Locator): Promise<Locator> {
  await card
    .getByRole("button", { name: "Nhập khoản nhận tiền", exact: true })
    .click();
  const dialog = card.page().getByRole("dialog");
  await dialog.waitFor();
  return dialog;
}
