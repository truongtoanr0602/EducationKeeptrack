import { test, expect, type Page } from "@playwright/test";

// Lessons, question bank and online exam (#78–#82) against the real backend: the teacher imports the Excel
// template, builds a 4-3-2-1 exam, opens it; a student joins by link and code on a phone; the teacher closes and
// confirms, and the board shows right answers per skill.

async function login(page: Page) {
  await page.goto("/", { waitUntil: "domcontentloaded" });
  await page.getByLabel("Email", { exact: true }).fill(process.env.DEMO_EMAIL!);
  await page.getByLabel("Mật khẩu", { exact: true }).fill(process.env.DEMO_PASSWORD!);
  await page.getByRole("button", { name: "Đăng nhập", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Hôm nay" })).toBeVisible();
}

async function write(page: Page, path: string, data: unknown, method = "POST") {
  const csrf = await (await page.request.get("/api/auth/csrf")).json();
  const response = await page.request.fetch(`/api${path}`, { method, data, headers: { "X-CSRF-TOKEN": csrf.token } });
  expect(response.ok(), await response.text()).toBeTruthy();
  return response.json();
}

const suffix = () => Math.random().toString(36).replace(/[^a-z]/g, "").padEnd(5, "x").slice(0, 5);

test("teacher imports the template, runs a 4-3-2-1 exam and sees results per skill", async ({ page, browser }) => {
  test.setTimeout(180_000);
  await login(page);
  const classroom = await write(page, "/classes", { name: `Toán 2 đề ${suffix()}`, subject: "Toán" });
  const student = await write(page, "/students", { name: `Nguyễn Văn E${suffix()}` });
  await write(page, `/classes/${classroom.id}/enrollments`, { studentId: student.id, startDate: "2026-01-01" });

  // Import the downloadable template as is.
  const template = await page.request.get(`/api/learning/classes/${classroom.id}/import-template`);
  expect(template.ok()).toBeTruthy();
  await page.evaluate((id) => (window.location.hash = `#/classes/${id}/personalization/lessons`), classroom.id);
  await page.getByLabel("File Excel").setInputFiles({
    name: "mau.xlsx",
    mimeType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
    buffer: Buffer.from(await template.body()),
  });
  await page.getByRole("button", { name: "Xem trước", exact: true }).first().click();
  await expect(page.getByText("Xem trước: chưa ghi gì.")).toBeVisible();
  await page.getByRole("button", { name: "Ghi vào lớp" }).click();
  await expect(page.getByText("Đã ghi vào lớp.")).toBeVisible();
  await expect(page.getByRole("heading", { name: "Buổi chương trình (30)" })).toBeVisible();

  // Build the exam from the bank.
  await page.getByRole("link", { name: "Đề kiểm tra" }).click();
  await page.getByRole("button", { name: "Tạo đề kiểm tra" }).click();
  await page.getByLabel("Đến buổi").fill("30");
  await page.getByRole("button", { name: "Sinh đề" }).click();
  await expect(page.getByRole("heading", { name: "Xem trước 10 câu" })).toBeVisible();
  await page.getByRole("button", { name: "Tạo đề (bản nháp)" }).click();
  page.on("dialog", (dialog) => void dialog.accept());
  await page.getByRole("button", { name: "Mở đề cho học sinh làm" }).click();
  const link = await page.getByLabel("Đường link làm bài").inputValue();
  const row = page.getByRole("row").filter({ hasText: student.name });
  const code = (await row.locator(".seat-code").innerText()).trim();
  expect(code).toMatch(/^\d{4}$/);

  // The student, on a phone, without an account.
  const phone = await browser.newContext({ viewport: { width: 390, height: 844 } });
  const kid = await phone.newPage();
  await kid.goto(new URL(link).pathname + new URL(link).hash);
  const shortName = student.name.split(" ").at(-1);
  await kid.getByRole("button", { name: new RegExp(`^${shortName}`) }).click();
  await kid.getByLabel("Mã vào bài").fill(code);
  await kid.getByRole("button", { name: "Vào làm bài" }).click();
  for (let i = 0; i < 10; i++) {
    const choice = kid.locator(".take-options button").first();
    if (await choice.count()) await choice.click();
    else await kid.locator(".take-chips button").first().click();
    await expect(kid.getByText("Đã lưu ✓")).toBeVisible();
    if (i < 9) await kid.getByRole("button", { name: "Câu sau →" }).click();
  }
  await kid.getByRole("button", { name: "Nộp bài" }).click();
  await kid.getByRole("alertdialog").getByRole("button", { name: "Nộp bài" }).click();
  await expect(kid.getByRole("heading", { name: /Em đã nộp bài/ })).toBeVisible();
  await expect(kid.getByText("Đáp án")).toHaveCount(0);
  await phone.close();

  // Close, confirm, and read the board by skill.
  await page.getByRole("button", { name: "Tải lại" }).click();
  await page.getByRole("button", { name: "Đóng bài" }).click();
  await expect(page.getByRole("heading", { name: "Kết quả theo kỹ năng" })).toBeVisible();
  await page.getByRole("button", { name: "Xác nhận kết quả" }).click();
  await expect(page.getByText("Kết quả đã vào bài đánh giá và hồ sơ năng lực.")).toBeVisible();
  await expect(page.getByRole("columnheader", { name: /^E\d/ }).first()).toBeVisible();
});
