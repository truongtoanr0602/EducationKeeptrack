import { test, expect } from "@playwright/test";

test("teacher manages a persistent roster and retains input after a failed save", async ({
  page,
}) => {
  await page.goto("/", { waitUntil: "domcontentloaded" });
  await page.getByLabel("Email", { exact: true }).fill(process.env.DEMO_EMAIL!);
  await page
    .getByLabel("Mật khẩu", { exact: true })
    .fill(process.env.DEMO_PASSWORD!);
  await page.getByRole("button", { name: "Đăng nhập", exact: true }).click();
  await page.getByRole("button", { name: "Tạo lớp mới", exact: true }).click();
  const name = `Lớp danh sách ${Date.now()}`;
  await page.getByLabel("Tên lớp", { exact: true }).fill(name);
  await page.getByLabel("Môn học", { exact: true }).fill("Toán");
  await page.getByRole("button", { name: "Lưu lớp", exact: true }).click();
  await page
    .getByRole("article")
    .filter({ has: page.getByRole("heading", { name, exact: true }) })
    .getByRole("button", { name: "Danh sách học sinh" })
    .click();
  await page
    .getByRole("button", { name: "Thêm học sinh mới", exact: true })
    .click();
  await page.getByLabel("Tên học sinh", { exact: true }).fill("Nguyễn An");
  await page.getByLabel("Tên phụ huynh", { exact: true }).fill("Nguyễn Bình");
  await page
    .getByLabel("Điện thoại liên hệ", { exact: true })
    .fill("0901234567");
  await page.getByLabel("Ngày bắt đầu", { exact: true }).fill("2026-10-01");
  await page.route("**/api/students", (route) =>
    route.request().method() === "POST"
      ? route.fulfill({
          status: 503,
          contentType: "application/json",
          body: JSON.stringify({
            message: "Không thể lưu học sinh. Vui lòng thử lại.",
          }),
        })
      : route.continue(),
  );
  await page.getByRole("button", { name: "Lưu học sinh", exact: true }).click();
  await expect(page.getByRole("alert")).toHaveText(
    "Không thể lưu học sinh. Vui lòng thử lại.",
  );
  await expect(page.getByLabel("Tên học sinh", { exact: true })).toHaveValue(
    "Nguyễn An",
  );
  await page.unroute("**/api/students");
  await page.getByRole("button", { name: "Lưu học sinh", exact: true }).click();
  const row = page
    .getByRole("article")
    .filter({
      has: page.getByRole("heading", { name: "Nguyễn An", exact: true }),
    });
  await expect(row).toContainText("Nguyễn Bình");
  await row.getByRole("button", { name: "Sửa hồ sơ" }).click();
  await page
    .getByLabel("Tên phụ huynh", { exact: true })
    .fill("Nguyễn Bình đã sửa");
  await page.getByRole("button", { name: "Lưu học sinh", exact: true }).click();
  await row.getByRole("button", { name: "Đổi tình trạng" }).click();
  await page.getByLabel("Tình trạng mới").selectOption("leave");
  await page.getByLabel("Ngày hiệu lực").fill("2026-10-05");
  await page.getByRole("button", { name: "Lưu tình trạng" }).click();
  await expect(row).toContainText("Nghỉ có phép");
  await page.reload({ waitUntil: "domcontentloaded" });
  await expect(row).toContainText("Nghỉ có phép");
  await expect(row).toContainText("Nguyễn Bình đã sửa");
  await row.getByText("Lịch sử theo học", { exact: true }).click();
  await expect(row).toContainText("2026-10-05");
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth,
    ),
  ).toBeTruthy();
});
