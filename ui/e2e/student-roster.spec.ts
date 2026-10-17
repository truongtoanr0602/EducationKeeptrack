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
  await page.route("**/api/classes/*/enrollments", (route) =>
    route.request().method() === "POST"
      ? route.fulfill({
          status: 503,
          contentType: "application/json",
          body: JSON.stringify({
            message: "Chưa thể đăng ký lớp. Vui lòng thử lại.",
          }),
        })
      : route.continue(),
  );
  await page.getByRole("button", { name: "Lưu học sinh", exact: true }).click();
  await expect(page.getByRole("alert")).toHaveText(
    "Chưa thể đăng ký lớp. Vui lòng thử lại.",
  );
  await expect(
    page.getByText("Hồ sơ đã lưu. Tiếp tục lưu để hoàn tất đăng ký lớp."),
  ).toBeVisible();
  const profilesBeforeRetry = await (
    await page.request.get("/api/students")
  ).json();
  await page.unroute("**/api/classes/*/enrollments");
  await page.getByRole("button", { name: "Lưu học sinh", exact: true }).click();
  const row = page.getByRole("article").filter({
    has: page.getByRole("heading", { name: "Nguyễn An", exact: true }),
  });
  await expect(row).toContainText("Nguyễn Bình");
  const profilesAfterRetry = await (
    await page.request.get("/api/students")
  ).json();
  // Retrying a partially successful add must not create another student profile.
  expect(profilesAfterRetry.length).toBe(profilesBeforeRetry.length);
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
  const classId = new URL(page.url()).hash.slice(1);
  const roster = await (
    await page.request.get(`/api/classes/${classId}/enrollments`)
  ).json();
  await row.getByRole("button", { name: "Đổi tình trạng" }).click();
  await page.getByLabel("Tình trạng mới").selectOption("stopped");
  await page.getByLabel("Ngày hiệu lực").fill("2026-10-10");
  const csrf = await (await page.request.get("/api/auth/csrf")).json();
  // A second tab changes this enrollment after the form has loaded its version.
  const otherTab = await page.request.put(
    `/api/classes/${classId}/enrollments/${roster[0].id}`,
    {
      headers: { "X-CSRF-TOKEN": csrf.token },
      data: {
        status: "active",
        effectiveDate: "2026-10-07",
        version: roster[0].version,
      },
    },
  );
  expect(otherTab.ok()).toBeTruthy();
  await page.getByRole("button", { name: "Lưu tình trạng" }).click();
  await expect(page.getByRole("alert")).toContainText("đã thay đổi");
  await page.getByRole("button", { name: "Tải lại danh sách" }).click();
  await expect(row).toContainText("2026-10-07");
  await expect(page.getByLabel("Tình trạng mới")).toHaveValue("stopped");
  await expect(page.getByLabel("Ngày hiệu lực")).toHaveValue("2026-10-10");
  await page.getByRole("button", { name: "Lưu tình trạng" }).click();
  await expect(page.getByLabel("Ngày hiệu lực")).toHaveCount(0);
  await expect(row).toContainText("Kết thúc: 2026-10-10");
  await page
    .getByRole("button", { name: "Chọn học sinh đã có", exact: true })
    .click();
  await page
    .getByLabel("Học sinh", { exact: true })
    .selectOption(roster[0].studentId);
  await page.getByLabel("Ngày bắt đầu", { exact: true }).fill("2026-10-06");
  await page.getByRole("button", { name: "Lưu đăng ký", exact: true }).click();
  await expect(page.getByRole("alert")).toContainText("trùng thời gian");
  await expect(page.getByLabel("Học sinh", { exact: true })).toHaveValue(
    roster[0].studentId,
  );
  await expect(page.getByLabel("Ngày bắt đầu", { exact: true })).toHaveValue(
    "2026-10-06",
  );
  await page.getByRole("button", { name: "Hủy", exact: true }).click();
  await row.getByRole("button", { name: "Sửa hồ sơ" }).click();
  await page
    .getByLabel("Tên phụ huynh", { exact: true })
    .fill("Nội dung chưa lưu khi hết hạn");
  await page.route("**/api/students/*", (route) =>
    route.request().method() === "PUT"
      ? route.fulfill({
          status: 403,
          contentType: "application/json",
          body: JSON.stringify({ message: "Quyền sử dụng đã hết hạn." }),
        })
      : route.continue(),
  );
  await page.route("**/api/auth/me", (route) =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        email: process.env.DEMO_EMAIL,
        access: {
          canRead: true,
          canWrite: false,
          canUseAi: false,
          status: "expired",
        },
      }),
    }),
  );
  await page.getByRole("button", { name: "Lưu học sinh", exact: true }).click();
  await expect(page.getByRole("alert")).toContainText("hết hạn");
  await expect(page.getByLabel("Tên phụ huynh", { exact: true })).toHaveValue(
    "Nội dung chưa lưu khi hết hạn",
  );
  await expect(
    page.getByRole("button", { name: "Lưu học sinh", exact: true }),
  ).toBeDisabled();
  await expect(row).toContainText("Nguyễn Bình đã sửa");
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth,
    ),
  ).toBeTruthy();
});
