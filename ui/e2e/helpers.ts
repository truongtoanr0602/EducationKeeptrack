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

// Monthly report: the draft form and the PDF preview each open in a dialog over the page.
export async function openReportDraft(region: Locator): Promise<Locator> {
  await region
    .getByRole("button", { name: "Soạn nội dung báo cáo", exact: true })
    .click();
  const dialog = region
    .page()
    .getByRole("dialog", { name: "Nội dung báo cáo" });
  await dialog.waitFor();
  return dialog;
}

export async function openReportPreview(region: Locator): Promise<Locator> {
  await region
    .getByRole("button", { name: "Xem trước báo cáo", exact: true })
    .click();
  const dialog = region
    .page()
    .getByRole("dialog", { name: "Xem trước báo cáo" });
  await dialog.waitFor();
  return dialog;
}

// Rates and fee adjustments open in a dialog; every field, the submit button and "Tải lại bảng phí" are inside it.
export async function openRateForm(region: Locator): Promise<Locator> {
  await region
    .getByRole("button", { name: "Thêm đơn giá", exact: true })
    .click();
  const dialog = region
    .page()
    .getByRole("dialog", { name: "Đơn giá theo ngày hiệu lực" });
  await dialog.waitFor();
  return dialog;
}

export async function openAdjustmentForm(card: Locator): Promise<Locator> {
  await card
    .getByRole("button", { name: "Điều chỉnh học phí", exact: true })
    .click();
  const dialog = card
    .page()
    .getByRole("dialog", { name: /^Điều chỉnh học phí/ });
  await dialog.waitFor();
  return dialog;
}

// The receiving account form opens in a dialog from the payments screen.
export async function openRecipientSettings(region: Locator): Promise<Locator> {
  await region
    .getByRole("button", { name: "Thiết lập tài khoản nhận tiền", exact: true })
    .click();
  const dialog = region
    .page()
    .getByRole("dialog", { name: "Tài khoản nhận tiền" });
  await dialog.waitFor();
  return dialog;
}
