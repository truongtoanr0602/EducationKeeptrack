import { test, expect } from "@playwright/test";

test("receipt corrections preserve history and restore balances after reload", async ({
  page,
}) => {
  await page.goto("/", { waitUntil: "domcontentloaded" });
  await page.getByLabel("Email", { exact: true }).fill(process.env.DEMO_EMAIL!);
  await page
    .getByLabel("Mật khẩu", { exact: true })
    .fill(process.env.DEMO_PASSWORD!);
  await page.getByRole("button", { name: "Đăng nhập", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "Lớp học của tôi" }),
  ).toBeVisible();
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
  const classroom = await write("/classes", {
    name: `Sửa thu ${Date.now()}`,
    subject: "Toán",
  });
  const student = await write("/students", { name: "An sửa thu" });
  const classId = classroom.id;
  await write(`/classes/${classId}/enrollments`, {
    studentId: student.id,
    startDate: "2026-10-01",
  });
  await write(`/billing/classes/${classId}/rates`, {
    effectiveFrom: "2026-10-01",
    unitPriceVnd: "500000",
    version: 0,
  });
  const session = await write(`/classes/${classId}/sessions`, {
    date: "2026-10-02",
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
    `/classes/${classId}/sessions/${session.id}/attendance/${student.id}`,
    { status: "present", version: 0 },
    "PUT",
  );
  await page.goto(`/?tuitionMonth=2026-10#${classId}`, {
    waitUntil: "domcontentloaded",
  });
  const card = page.getByRole("article", { name: "Công nợ An sửa thu" });
  await expect(
    card.getByText("Tổng cần thu: 500.000", { exact: false }),
  ).toBeVisible();
  await card.getByLabel("Số tiền nhận (đồng)").fill("500000");
  await card
    .getByRole("button", { name: "Ghi nhận tiền", exact: true })
    .click();
  await expect(
    card.getByText("Tổng cần thu: 0", { exact: false }),
  ).toBeVisible();
  const history = card.getByRole("list", {
    name: "Lịch sử nhận tiền",
    exact: true,
  });
  await history
    .getByRole("button", { name: "Sửa khoản nhận tiền", exact: true })
    .click();
  const form = history.getByRole("form", {
    name: "Sửa hoặc hủy khoản nhận tiền",
  });
  await form.getByLabel("Số tiền thay thế (đồng)").fill("200000");
  await form.getByLabel("Ngày thực nhận thay thế").fill("2026-10-08");
  await form
    .getByRole("button", { name: "Lưu sửa khoản nhận tiền", exact: true })
    .click();
  await expect(form.getByRole("alert")).toContainText("lý do");
  await form.getByLabel("Lý do sửa/hủy").fill("Nhập nhầm tiền và ngày");
  await page.route(
    "**/api/billing/classes/*/payments/*/corrections",
    async (route) => {
      const response = await route.fetch();
      expect(response.status()).toBe(200);
      await route.abort("failed");
    },
  );
  await form
    .getByRole("button", { name: "Lưu sửa khoản nhận tiền", exact: true })
    .click();
  await expect(form.getByRole("alert")).toContainText("chưa rõ");
  await page.unroute("**/api/billing/classes/*/payments/*/corrections");
  await form
    .getByRole("button", { name: "Thử lại sửa/hủy", exact: true })
    .click();
  await expect(
    card.getByText("Tổng cần thu: 300.000", { exact: false }),
  ).toBeVisible();
  await expect(history).toContainText("Nhập nhầm tiền và ngày");
  await page.reload({ waitUntil: "domcontentloaded" });
  await expect(
    card.getByText("Tổng cần thu: 300.000", { exact: false }),
  ).toBeVisible();
  await expect(history).toContainText("2026-10-08");
  await history
    .getByRole("button", { name: "Xem trước/sau", exact: true })
    .click();
  await expect(
    history.getByRole("table", { name: "Đối chiếu trước/sau" }),
  ).toContainText("500.000");
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth,
    ),
  ).toBeTruthy();
  await history
    .getByRole("button", { name: "Hủy khoản nhận tiền", exact: true })
    .click();
  const cancel = history.getByRole("form", {
    name: "Sửa hoặc hủy khoản nhận tiền",
  });
  await cancel.getByLabel("Lý do sửa/hủy").fill("Khoản nhận bị trùng");
  await cancel
    .getByRole("button", { name: "Xác nhận hủy khoản nhận tiền", exact: true })
    .click();
  await expect(
    card.getByText("Tổng cần thu: 500.000", { exact: false }),
  ).toBeVisible();
  await page.reload({ waitUntil: "domcontentloaded" });
  await expect(
    card.getByText("Tổng cần thu: 500.000", { exact: false }),
  ).toBeVisible();
  await expect(history).toContainText("Khoản nhận bị trùng");
  await expect(
    history.getByRole("button", { name: "Sửa khoản nhận tiền", exact: true }),
  ).toHaveCount(0);
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth,
    ),
  ).toBeTruthy();
});
