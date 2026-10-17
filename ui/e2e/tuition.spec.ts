import { test, expect } from "@playwright/test";

test("teacher sets effective tuition, reloads the monthly breakdown, and sees corrected attendance", async ({
  page,
}) => {
  await page.goto("/", { waitUntil: "domcontentloaded" });
  await page.getByLabel("Email", { exact: true }).fill(process.env.DEMO_EMAIL!);
  await page
    .getByLabel("Mật khẩu", { exact: true })
    .fill(process.env.DEMO_PASSWORD!);
  await page.getByRole("button", { name: "Đăng nhập", exact: true }).click();
  await page.getByRole("button", { name: "Tạo lớp mới", exact: true }).click();
  const name = `Lớp học phí ${Date.now()}`;
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
  await page.getByLabel("Tên học sinh", { exact: true }).fill("An học phí");
  await page.getByLabel("Ngày bắt đầu", { exact: true }).fill("2026-10-01");
  await page.getByRole("button", { name: "Lưu học sinh", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "An học phí", exact: true }),
  ).toBeVisible();
  const tuition = page.getByRole("region", { name: "Học phí theo buổi" });
  await tuition.getByLabel("Đơn giá áp dụng từ ngày").fill("2026-10-01");
  await tuition.getByLabel("Đơn giá mỗi buổi (đồng)").fill("150000");
  await page.route("**/api/billing/classes/*/rates", (route) =>
    route.request().method() === "POST"
      ? route.fulfill({
          status: 503,
          contentType: "application/json",
          body: JSON.stringify({
            message: "Không thể lưu đơn giá. Vui lòng thử lại.",
          }),
        })
      : route.continue(),
  );
  await tuition
    .getByRole("button", { name: "Lưu đơn giá", exact: true })
    .click();
  await expect(tuition.getByRole("alert")).toContainText("Vui lòng thử lại");
  await expect(tuition.getByLabel("Đơn giá mỗi buổi (đồng)")).toHaveValue(
    "150000",
  );
  await page.unroute("**/api/billing/classes/*/rates");
  await tuition
    .getByRole("button", { name: "Lưu đơn giá", exact: true })
    .click();
  await expect(
    tuition.getByText("Đã lưu đơn giá.", { exact: true }),
  ).toBeVisible();
  const classId = new URL(page.url()).hash.slice(1);
  const enrollments = await (
    await page.request.get(`/api/classes/${classId}/enrollments`)
  ).json();
  const studentId = enrollments[0].studentId;
  async function write(path: string, data: unknown, method = "POST") {
    const csrf = await (await page.request.get("/api/auth/csrf")).json();
    const response = await page.request.fetch(`/api${path}`, {
      method,
      headers: { "X-CSRF-TOKEN": csrf.token },
      data,
    });
    expect(response.ok()).toBeTruthy();
    return response.json();
  }
  let firstSession = "";
  for (let day = 2; day <= 9; day++) {
    const session = await write(`/classes/${classId}/sessions`, {
      date: `2026-10-${String(day).padStart(2, "0")}`,
      startTime: "18:00:00",
      endTime: "19:00:00",
      kind: "regular",
    });
    if (!firstSession) firstSession = session.id;
    await write(
      `/classes/${classId}/sessions/${session.id}`,
      { status: "held", version: 1 },
      "PUT",
    );
    await write(
      `/classes/${classId}/sessions/${session.id}/attendance/${studentId}`,
      { status: "present", version: 0 },
      "PUT",
    );
  }
  await tuition.getByLabel("Tháng học phí").fill("2026-10");
  await tuition.getByRole("button", { name: "Tải lại bảng phí" }).click();
  await expect(
    tuition.getByText("Đã cập nhật bảng phí.", { exact: true }),
  ).toBeVisible();
  await expect(
    tuition.getByRole("status", { name: "Tổng học phí" }),
  ).toContainText("1.200.000");
  await tuition.getByRole("button", { name: "Chi tiết An học phí" }).click();
  await expect(
    tuition
      .getByRole("table", { name: "Căn cứ học phí An học phí" })
      .getByRole("row"),
  ).toHaveCount(9);
  await page.reload({ waitUntil: "domcontentloaded" });
  await expect(
    tuition.getByRole("status", { name: "Tổng học phí" }),
  ).toContainText("1.200.000");
  await write(
    `/classes/${classId}/sessions/${firstSession}/attendance/${studentId}`,
    { status: "absent", version: 1 },
    "PUT",
  );
  // The UI must label stale totals explicitly until a fresh projection arrives.
  const current = await (
    await page.request.get(
      `/api/billing/classes/${classId}/tuition?month=2026-10`,
    )
  ).json();
  await page.route("**/api/billing/classes/*/tuition?*", (route) =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({ ...current, status: "pending" }),
    }),
  );
  await tuition.getByRole("button", { name: "Tải lại bảng phí" }).click();
  await expect(tuition.getByText(/Đang chờ cập nhật/)).toBeVisible();
  await page.unroute("**/api/billing/classes/*/tuition?*");
  await tuition.getByRole("button", { name: "Tải lại bảng phí" }).click();
  await expect(
    tuition.getByText("Đã cập nhật bảng phí.", { exact: true }),
  ).toBeVisible();
  await expect(
    tuition.getByRole("status", { name: "Tổng học phí" }),
  ).toContainText("1.050.000");
  await tuition.getByLabel("Đơn giá áp dụng từ ngày").fill("2026-10-20");
  await tuition.getByLabel("Đơn giá mỗi buổi (đồng)").fill("250000");
  await write(`/billing/classes/${classId}/rates`, {
    effectiveFrom: "2026-10-16",
    unitPriceVnd: "200000",
    version: 1,
  });
  await tuition
    .getByRole("button", { name: "Lưu đơn giá", exact: true })
    .click();
  await expect(tuition.getByRole("alert")).toContainText("đã thay đổi");
  await tuition.getByRole("button", { name: "Tải lại bảng phí" }).click();
  await expect(tuition.getByLabel("Đơn giá mỗi buổi (đồng)")).toHaveValue(
    "250000",
  );
  await expect(
    tuition.getByText("Đã cập nhật bảng phí.", { exact: true }),
  ).toBeVisible();
  await tuition
    .getByRole("button", { name: "Lưu đơn giá", exact: true })
    .click();
  await expect(tuition.getByRole("alert")).toHaveCount(0);
  await expect(
    tuition.getByRole("list", { name: "Đơn giá đã lưu" }),
  ).toContainText("250.000");
  await tuition.getByLabel("Đơn giá áp dụng từ ngày").fill("2026-10-25");
  await tuition.getByLabel("Đơn giá mỗi buổi (đồng)").fill("300000");
  await page.route("**/api/billing/classes/*/rates", (route) =>
    route.request().method() === "POST"
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
  await tuition
    .getByRole("button", { name: "Lưu đơn giá", exact: true })
    .click();
  await expect(tuition.getByRole("alert")).toContainText("hết hạn");
  await expect(tuition.getByLabel("Đơn giá mỗi buổi (đồng)")).toHaveValue(
    "300000",
  );
  await expect(
    tuition.getByRole("button", { name: "Lưu đơn giá", exact: true }),
  ).toBeDisabled();
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth,
    ),
  ).toBeTruthy();
});
