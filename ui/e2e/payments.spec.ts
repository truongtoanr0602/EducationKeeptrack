import { test, expect } from "@playwright/test";

test("teacher records partial receipts, retries a lost response once, and reloads debt history", async ({
  page,
}) => {
  await page.goto("/", { waitUntil: "domcontentloaded" });
  await page.getByLabel("Email", { exact: true }).fill(process.env.DEMO_EMAIL!);
  await page
    .getByLabel("Mật khẩu", { exact: true })
    .fill(process.env.DEMO_PASSWORD!);
  await page.getByRole("button", { name: "Đăng nhập", exact: true }).click();
  await page.getByRole("button", { name: "Tạo lớp mới", exact: true }).click();
  const name = `Lớp công nợ ${Date.now()}`;
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
  await page.getByLabel("Tên học sinh", { exact: true }).fill("An công nợ");
  await page.getByLabel("Ngày bắt đầu", { exact: true }).fill("2026-10-01");
  await page.getByRole("button", { name: "Lưu học sinh", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "An công nợ", exact: true }),
  ).toBeVisible();
  const payments = page.getByRole("region", { name: "Thanh toán và công nợ" });
  await expect(payments).toBeVisible();
  const classId = new URL(page.url()).hash.slice(1);
  const enrollment = await (
    await page.request.get(`/api/classes/${classId}/enrollments`)
  ).json();
  const studentId = enrollment[0].studentId;
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
  for (const [month, amount, version] of [
    ["10", "500000", 0],
    ["11", "900000", 1],
  ] as const) {
    await write(`/billing/classes/${classId}/rates`, {
      effectiveFrom: `2026-${month}-01`,
      unitPriceVnd: amount,
      version,
    });
    const session = await write(`/classes/${classId}/sessions`, {
      date: `2026-${month}-02`,
      startTime: "18:00:00",
      endTime: "19:00:00",
      kind: "regular",
    });
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
  await page.getByLabel("Tháng học phí").fill("2026-11");
  await page.getByRole("button", { name: "Tải lại bảng phí" }).click();
  await payments.getByRole("button", { name: "Tải lại công nợ" }).click();
  const student = payments.getByRole("article", { name: "Công nợ An công nợ" });
  await expect(
    student.getByText("Tổng cần thu: 1.400.000", { exact: false }),
  ).toBeVisible();
  await expect(
    student.getByRole("button", { name: "Ghi nhận tiền", exact: true }),
  ).toBeEnabled();
  await student.getByLabel("Số tiền nhận (đồng)").fill("700000");
  await student.getByLabel("Ngày nhận tiền").fill("2026-11-05");
  await student.getByLabel("Ghi chú nhận tiền").fill("Lần đầu");
  await page.route("**/api/billing/classes/*/payments", (route) =>
    route.request().method() === "POST"
      ? route.fulfill({
          status: 409,
          contentType: "application/json",
          body: JSON.stringify({
            message: "Công nợ đã thay đổi. Vui lòng tải lại.",
          }),
        })
      : route.continue(),
  );
  await student
    .getByRole("button", { name: "Ghi nhận tiền", exact: true })
    .click();
  await expect(student.getByRole("alert")).toContainText("đã thay đổi");
  await expect(student.getByLabel("Số tiền nhận (đồng)")).toHaveValue("700000");
  await page.unroute("**/api/billing/classes/*/payments");
  await payments.getByRole("button", { name: "Tải lại công nợ" }).click();
  await expect(
    student.getByRole("button", { name: "Ghi nhận tiền", exact: true }),
  ).toBeEnabled();
  // The server commits, but the browser loses the response. Retry must reuse the original request.
  await page.route("**/api/billing/classes/*/payments", async (route) => {
    if (route.request().method() !== "POST") return route.continue();
    const response = await route.fetch();
    expect(response.status()).toBe(201);
    await route.abort("failed");
  });
  await student
    .getByRole("button", { name: "Ghi nhận tiền", exact: true })
    .click();
  await expect(student.getByRole("alert")).toContainText("chưa rõ");
  await expect(student.getByLabel("Số tiền nhận (đồng)")).toHaveValue("700000");
  await expect(student.getByLabel("Số tiền nhận (đồng)")).toBeDisabled();
  await page.unroute("**/api/billing/classes/*/payments");
  await student
    .getByRole("button", { name: "Thử lại khoản nhận tiền", exact: true })
    .click();
  await expect(
    student.getByText("Tổng cần thu: 700.000", { exact: false }),
  ).toBeVisible();
  await expect(
    student
      .getByRole("list", { name: "Lịch sử nhận tiền" })
      .getByRole("listitem"),
  ).toHaveCount(1);
  await expect(student).toContainText("Đã trả đủ");
  await expect(student).toContainText("Trả một phần");
  await expect(student).toContainText("2026-10: 500.000");
  await expect(student).toContainText("2026-11: 200.000");
  await page.reload({ waitUntil: "domcontentloaded" });
  await expect(
    student.getByText("Tổng cần thu: 700.000", { exact: false }),
  ).toBeVisible();
  await student.getByLabel("Số tiền nhận (đồng)").fill("800000");
  await student.getByLabel("Ghi chú nhận tiền").fill("Trả đủ và dư");
  await student
    .getByRole("button", { name: "Ghi nhận tiền", exact: true })
    .click();
  await expect(
    student.getByText("Tổng cần thu: 0", { exact: false }),
  ).toBeVisible();
  await expect(
    student.getByText("Tiền chưa phân bổ: 100.000", { exact: false }),
  ).toBeVisible();
  await expect(
    student
      .getByRole("list", { name: "Lịch sử nhận tiền" })
      .getByRole("listitem"),
  ).toHaveCount(2);
  await student.getByLabel("Số tiền nhận (đồng)").fill("10000");
  await page.route("**/api/billing/classes/*/payments", (route) =>
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
  await student
    .getByRole("button", { name: "Ghi nhận tiền", exact: true })
    .click();
  await expect(student.getByRole("alert")).toContainText("hết hạn");
  await expect(student.getByLabel("Số tiền nhận (đồng)")).toHaveValue("10000");
  await expect(
    student.getByRole("button", { name: "Ghi nhận tiền", exact: true }),
  ).toBeDisabled();
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth,
    ),
  ).toBeTruthy();
});
