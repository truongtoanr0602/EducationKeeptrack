import { test, expect } from "@playwright/test";

test("teacher sees prepaid sources, records a sick-leave request and approves it with a reason", async ({ page }) => {
  await page.goto("/");
  await page.getByLabel("Email", { exact: true }).fill(process.env.REPORT_EMAIL ?? process.env.DEMO_EMAIL!);
  await page.getByLabel("Mật khẩu", { exact: true }).fill(process.env.DEMO_PASSWORD!);
  await page.getByRole("button", { name: "Đăng nhập", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Lớp học của tôi" })).toBeVisible();
  async function write(path: string, data: unknown, method = "POST") {
    const csrf = await (await page.request.get("/api/auth/csrf")).json();
    const response = await page.request.fetch(`/api${path}`, { method, data, headers: { "X-CSRF-TOKEN": csrf.token } });
    expect(response.ok()).toBeTruthy();
    return response.json();
  }
  const classroom = await write("/classes", { name: `Bảo lưu ${Date.now()}`, subject: "Toán" });
  const student = await write("/students", { name: "An trả trước" });
  await write(`/classes/${classroom.id}/enrollments`, { studentId: student.id, startDate: "2026-10-01" });
  await write(`/billing/classes/${classroom.id}/rates`, { effectiveFrom: "2026-10-01", unitPriceVnd: "250000", version: 0 });
  for (const date of ["2026-10-02", "2026-10-03", "2026-10-05"]) {
    const session = await write(`/classes/${classroom.id}/sessions`, { date, startTime: "18:00:00", endTime: "19:00:00", kind: "regular" });
    await write(`/classes/${classroom.id}/sessions/${session.id}`, { status: "held", version: 1 }, "PUT");
    await write(`/classes/${classroom.id}/sessions/${session.id}/attendance/${student.id}`, { status: "present", version: 0 }, "PUT");
  }
  let version = 0;
  await expect.poll(async () => {
    const report = await (await page.request.get(`/api/billing/classes/${classroom.id}/payments?month=2026-10`)).json();
    version = report.version;
    return report.status;
  }).toBe("ready");
  await write(`/billing/classes/${classroom.id}/payments`, { studentId: student.id, amountVnd: "1200000", receivedOn: "2026-11-05", note: "Đóng trước", idempotencyKey: crypto.randomUUID(), version });

  await page.goto(`/#/classes/${classroom.id}/tuition`);
  await page.reload();
  const panel = page.getByRole("region", { name: "Tiền trả trước và bảo lưu" });
  await expect(panel.getByTestId("reserve-policy")).toContainText("Chưa có chính sách bảo lưu");
  const summary = panel.getByTestId(`reserve-summary-${student.id}`);
  await expect(summary).toContainText("Đã thu 1.200.000");
  await expect(summary).toContainText("Đã dùng cho học phí 750.000");
  await expect(summary).toContainText("Chưa dùng 450.000");
  await expect(summary).toContainText("Có thể xét bảo lưu tối đa 450.000");

  const form = panel.getByRole("form", { name: "Ghi đề nghị bảo lưu" });
  await form.getByLabel("Lý do nghỉ").fill("Con ốm cả tuần");
  await form.getByLabel("Số tiền đề nghị bảo lưu (đồng)").fill("450001");
  await form.getByRole("button", { name: "Ghi đề nghị" }).click();
  await expect(form.getByRole("alert")).toContainText("tối đa 450000");
  await form.getByLabel("Số tiền đề nghị bảo lưu (đồng)").fill("450000");
  await form.getByRole("button", { name: "Ghi đề nghị" }).click();
  const request = panel.locator(".reserve-request[data-status='pending']");
  await expect(request).toContainText("Chờ quyết định");

  await request.getByRole("button", { name: "Duyệt bảo lưu" }).click();
  await expect(request.getByRole("alert")).toContainText("Nhập lý do quyết định");
  await request.getByLabel("Lý do quyết định").fill("Phụ huynh có giấy khám");
  await request.getByRole("button", { name: "Duyệt bảo lưu" }).click();
  const approved = panel.locator(".reserve-request[data-status='approved']");
  await expect(approved).toContainText("Đã duyệt bảo lưu");
  await expect(approved).toContainText("Lấy từ: khoản nhận 2026-11-05");
  await expect(summary).toContainText("Đã nằm trong bảo lưu 450.000");
  await expect(summary).toContainText("Có thể xét bảo lưu tối đa 0");
  await expect(form).toHaveCount(0); // nothing left to reserve
  await approved.getByText("Lịch sử (2)").click();
  await expect(approved.getByText("Duyệt: Phụ huynh có giấy khám")).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBeTruthy();
});
