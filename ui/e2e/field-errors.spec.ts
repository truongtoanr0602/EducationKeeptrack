import { test, expect, type Page } from "@playwright/test";

// Validation messages sit next to the field they are about (GOV.UK pattern): the field is marked invalid and points at
// its message, the first invalid field takes focus, and the form keeps its summary line.

async function signIn(page: Page) {
  await page.goto("/", { waitUntil: "domcontentloaded" });
  await page.getByLabel("Email", { exact: true }).fill(process.env.DEMO_EMAIL!);
  await page
    .getByLabel("Mật khẩu", { exact: true })
    .fill(process.env.DEMO_PASSWORD!);
  await page.getByRole("button", { name: "Đăng nhập", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Hôm nay" })).toBeVisible();
}

async function newClass(page: Page, name: string) {
  const csrf = await (await page.request.get("/api/auth/csrf")).json();
  const response = await page.request.post("/api/classes", {
    data: { name, subject: "Toán" },
    headers: { "X-CSRF-TOKEN": csrf.token },
  });
  expect(response.ok()).toBeTruthy();
  return (await response.json()).id as string;
}

test("a student form names the field that is wrong and moves focus to it", async ({
  page,
}) => {
  await signIn(page);
  const id = await newClass(page, `Lớp lỗi ô ${Date.now()}`);
  await page.evaluate(
    (c) => (window.location.hash = `#/classes/${c}/students`),
    id,
  );
  await page
    .getByRole("button", { name: "Thêm học sinh mới", exact: true })
    .click();
  const dialog = page.getByRole("dialog");
  await dialog.getByLabel("Ngày bắt đầu", { exact: true }).fill("");
  await dialog
    .getByRole("button", { name: "Lưu học sinh", exact: true })
    .click();

  const name = dialog.getByLabel("Tên học sinh", { exact: true });
  const start = dialog.getByLabel("Ngày bắt đầu", { exact: true });
  await expect(dialog.getByText("Nhập tên học sinh.")).toBeVisible();
  await expect(dialog.getByText("Chọn ngày bắt đầu.")).toBeVisible();
  // Both fields are invalid and described by their own message; the first one has focus.
  await expect(name).toHaveAttribute("aria-invalid", "true");
  await expect(start).toHaveAttribute("aria-invalid", "true");
  const describedBy = await name.getAttribute("aria-describedby");
  await expect(dialog.locator(`[id="${describedBy}"]`)).toContainText(
    "Nhập tên học sinh.",
  );
  await expect(name).toBeFocused();
  // The summary line the form already had is still there.
  await expect(dialog.getByRole("alert")).toHaveText(
    "Vui lòng nhập tên học sinh và ngày bắt đầu.",
  );

  // Submitting again with only the date fixed leaves one error, on the name.
  await start.fill("2026-10-01");
  await dialog
    .getByRole("button", { name: "Lưu học sinh", exact: true })
    .click();
  await expect(dialog.getByText("Chọn ngày bắt đầu.")).toHaveCount(0);
  await expect(start).not.toHaveAttribute("aria-invalid", "true");
  await expect(name).toHaveAttribute("aria-invalid", "true");
});

test("a rate the server rejects shows its message under the amount, not under the date", async ({
  page,
}) => {
  await signIn(page);
  const id = await newClass(page, `Lớp lỗi giá ${Date.now()}`);
  await page.route("**/api/billing/classes/*/rates", (route) =>
    route.request().method() === "POST"
      ? route.fulfill({
          status: 400,
          contentType: "application/json",
          body: JSON.stringify({
            message: "Dữ liệu không hợp lệ.",
            errors: { unitPriceVnd: ["Đơn giá vượt mức cho phép."] },
          }),
        })
      : route.continue(),
  );
  await page.evaluate(
    (c) => (window.location.hash = `#/classes/${c}/tuition`),
    id,
  );
  await page.getByRole("button", { name: "Thêm đơn giá", exact: true }).click();
  const dialog = page.getByRole("dialog");
  await dialog.getByLabel("Đơn giá áp dụng từ ngày").fill("2026-10-01");
  await dialog.getByLabel("Đơn giá mỗi buổi (đồng)").fill("150000");
  await dialog
    .getByRole("button", { name: "Lưu đơn giá", exact: true })
    .click();

  const amount = dialog.getByLabel("Đơn giá mỗi buổi (đồng)");
  await expect(amount).toHaveAttribute("aria-invalid", "true");
  await expect(amount).toBeFocused();
  await expect(
    dialog.getByLabel("Đơn giá áp dụng từ ngày"),
  ).not.toHaveAttribute("aria-invalid", "true");
  const describedBy = await amount.getAttribute("aria-describedby");
  await expect(dialog.locator(`[id="${describedBy}"]`)).toContainText(
    "Đơn giá vượt mức cho phép.",
  );
  // What was typed is kept.
  await expect(amount).toHaveValue("150000");
});
