import { test, expect } from "@playwright/test";

test("teacher deducts an approved reserve from the next month, reverses it and cancels the reserve", async ({ page }) => {
  await page.goto("/");
  await page.getByLabel("Email", { exact: true }).fill(process.env.REPORT_EMAIL ?? process.env.DEMO_EMAIL!);
  await page.getByLabel("Mật khẩu", { exact: true }).fill(process.env.DEMO_PASSWORD!);
  await page.getByRole("button", { name: "Đăng nhập", exact: true }).click();
  await page.evaluate(() => (window.location.hash = "#/classes"));
  await expect(page.getByRole("heading", { name: "Lớp học của tôi" })).toBeVisible();
  async function write(path: string, data: unknown, method = "POST") {
    const csrf = await (await page.request.get("/api/auth/csrf")).json();
    const response = await page.request.fetch(`/api${path}`, { method, data, headers: { "X-CSRF-TOKEN": csrf.token } });
    expect(response.ok()).toBeTruthy();
    return response.json();
  }
  const classroom = await write("/classes", { name: `Khấu trừ ${Date.now()}`, subject: "Toán" });
  const student = await write("/students", { name: "An khấu trừ" });
  await write(`/classes/${classroom.id}/enrollments`, { studentId: student.id, startDate: "2026-10-01" });
  await write(`/billing/classes/${classroom.id}/rates`, { effectiveFrom: "2026-10-01", unitPriceVnd: "250000", version: 0 });
  async function held(date: string) {
    const session = await write(`/classes/${classroom.id}/sessions`, { date, startTime: "18:00:00", endTime: "19:00:00", kind: "regular" });
    await write(`/classes/${classroom.id}/sessions/${session.id}`, { status: "held", version: 1 }, "PUT");
    await write(`/classes/${classroom.id}/sessions/${session.id}/attendance/${student.id}`, { status: "present", version: 0 }, "PUT");
  }
  async function ready() {
    let report = { version: 0, status: "" };
    await expect.poll(async () => { report = await (await page.request.get(`/api/billing/classes/${classroom.id}/payments?month=2026-11`)).json(); return report.status; }).toBe("ready");
    return report.version;
  }
  for (const date of ["2026-10-02", "2026-10-03", "2026-10-05"]) await held(date);
  await write(`/billing/classes/${classroom.id}/payments`, { studentId: student.id, amountVnd: "1200000", receivedOn: "2026-11-05", note: "Đóng trước", idempotencyKey: crypto.randomUUID(), version: await ready() });
  const requested = await write(`/billing/classes/${classroom.id}/reserve/requests`, { studentId: student.id, absentFrom: "2026-11-02", absentTo: "2026-11-06", reason: "Con ốm", amountVnd: "450000", idempotencyKey: crypto.randomUUID() });
  await write(`/billing/classes/${classroom.id}/reserve/requests/${requested.id}/decision`, { decision: "approve", reason: "Có giấy khám", idempotencyKey: crypto.randomUUID(), version: requested.version });
  for (const date of ["2026-11-09", "2026-11-10"]) await held(date);
  await ready();

  await page.goto(`/#/classes/${classroom.id}/tuition`);
  await page.reload();
  const panel = page.getByRole("region", { name: "Tiền trả trước và bảo lưu" });
  const balance = panel.getByTestId(`reserve-balance-${student.id}`);
  await expect(balance).toContainText("Đã duyệt 450.000");
  await expect(balance).toContainText("Còn lại 450.000");

  const form = panel.getByRole("form", { name: "Khấu trừ bảo lưu vào học phí" });
  await form.getByLabel("Kỳ học phí").selectOption("2026-11");
  await form.getByLabel("Số tiền khấu trừ (đồng)").fill("500001");
  await form.getByRole("button", { name: "Khấu trừ", exact: true }).click();
  await expect(form.getByRole("alert")).toContainText("tối đa 500000");
  await form.getByLabel("Số tiền khấu trừ (đồng)").fill("450000");
  await form.getByRole("button", { name: "Khấu trừ", exact: true }).click();
  await expect(balance).toContainText("Đã khấu trừ vào học phí 450.000");
  await expect(balance).toContainText("Còn lại 0");
  await expect(panel.getByRole("list", { name: "Các lần khấu trừ bảo lưu" })).toContainText("Kỳ 2026-11");
  await expect(panel.getByTestId(`reserve-movements-${student.id}`)).toContainText("chuyển sang kỳ sau 0");
  // The tuition screen shows the November debt net of the reserve: 500.000 of fees - 450.000 = 50.000.
  await expect(page.getByText("Tổng công nợ các kỳ: 50.000")).toBeVisible();

  // A reserve with an active use cannot be cancelled until the use is reversed.
  await panel.getByRole("button", { name: "Hủy bảo lưu", exact: true }).count().then((n) => expect(n).toBe(0));
  await expect(panel.getByText(/Hủy các lần khấu trừ trước khi hủy bảo lưu/)).toBeVisible();
  await panel.getByRole("button", { name: "Hủy lần khấu trừ" }).click();
  await panel.getByLabel("Lý do hủy khấu trừ").fill("Nhập nhầm kỳ");
  await panel.getByRole("button", { name: "Xác nhận hủy khấu trừ" }).click();
  await expect(balance).toContainText("Còn lại 450.000");
  await expect(page.getByText("Tổng công nợ các kỳ: 500.000")).toBeVisible();

  await panel.getByRole("button", { name: "Hủy bảo lưu", exact: true }).click();
  await panel.getByRole("button", { name: "Xác nhận hủy bảo lưu" }).click();
  await expect(panel.getByRole("alert")).toContainText("Nhập lý do hủy bảo lưu");
  await panel.getByLabel("Lý do hủy bảo lưu").fill("Phụ huynh xin hủy");
  await panel.getByRole("button", { name: "Xác nhận hủy bảo lưu" }).click();
  await expect(panel.locator(".reserve-request[data-status='cancelled']")).toContainText("Đã hủy bảo lưu");
  await expect(panel.getByTestId(`reserve-summary-${student.id}`)).toContainText("Đã nằm trong bảo lưu 0");
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBeTruthy();
});
