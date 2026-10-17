import { test, expect } from "@playwright/test";

test("teacher creates a class, reloads, and signs out", async ({
  page,
}, testInfo) => {
  await page.goto("/");
  await page.getByLabel("Email").fill(process.env.DEMO_EMAIL!);
  await page
    .getByLabel("Mật khẩu", { exact: true })
    .fill(process.env.DEMO_PASSWORD!);
  await page.getByRole("button", { name: "Đăng nhập", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "Lớp học của tôi" }),
  ).toBeVisible();
  await page.getByRole("button", { name: "Tạo lớp mới", exact: true }).click();
  await page.getByRole("button", { name: "Lưu lớp" }).click();
  await expect(page.getByText("Vui lòng nhập tên lớp.")).toBeVisible();
  const name = `Toán ôn tập ${Date.now()}`;
  await page.getByLabel("Tên lớp", { exact: true }).fill(name);
  await page.getByLabel("Môn học", { exact: true }).fill("Toán");
  await page.getByLabel("Mô tả (tùy chọn)").fill("Lớp luyện tập");
  // Exercise a failed save at the HTTP boundary, then retry against the real API.
  let finishFailedSave!: () => void;
  const pendingSave = new Promise<void>((resolve) => {
    finishFailedSave = resolve;
  });
  await page.route("**/api/classes", async (route) => {
    if (route.request().method() === "POST") {
      await pendingSave;
      await route.fulfill({
        status: 503,
        contentType: "application/json",
        body: JSON.stringify({
          message: "Không thể lưu lớp. Vui lòng thử lại.",
        }),
      });
    } else await route.continue();
  });
  await page.getByRole("button", { name: "Lưu lớp" }).click();
  await expect(page.getByRole("button", { name: "Đang lưu…" })).toBeDisabled();
  await expect(
    page.getByRole("button", { name: "Hủy", exact: true }),
  ).toBeDisabled();
  finishFailedSave();
  await expect(page.getByRole("alert")).toHaveText(
    "Không thể lưu lớp. Vui lòng thử lại.",
  );
  await expect(page.getByLabel("Tên lớp", { exact: true })).toHaveValue(name);
  await page.unroute("**/api/classes");
  await page.getByRole("button", { name: "Lưu lớp" }).click();
  await expect(page.getByRole("heading", { name, exact: true })).toBeVisible();
  await page.reload();
  await expect(page.getByRole("heading", { name, exact: true })).toBeVisible();
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth,
    ),
  ).toBeTruthy();
  await page.screenshot({
    path: `../.local/demo-${testInfo.project.name}.png`,
    fullPage: true,
  });
  await page.getByRole("button", { name: "Đăng xuất" }).click();
  await expect(page.getByRole("heading", { name: "Đăng nhập" })).toBeVisible();
});
