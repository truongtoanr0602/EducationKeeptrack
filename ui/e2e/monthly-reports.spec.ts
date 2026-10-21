import { test, expect, type Page } from "@playwright/test";
import { readFile } from "node:fs/promises";

async function setup(page: Page, withFee = false) {
  await page.goto("/");
  await page.getByLabel("Email", { exact: true }).fill(process.env.REPORT_EMAIL ?? process.env.DEMO_EMAIL!);
  await page.getByLabel("Mật khẩu", { exact: true }).fill(process.env.DEMO_PASSWORD!);
  await page.getByRole("button", { name: "Đăng nhập", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Lớp học của tôi" })).toBeVisible();
  async function write(path: string, data: unknown, method = "POST") {
    const csrf = await (await page.request.get("/api/auth/csrf")).json();
    const response = await page.request.fetch(`/api${path}`, { method, data, headers: { "X-CSRF-TOKEN": csrf.token } });
    expect(response.ok()).toBeTruthy(); return response.json();
  }
  const classroom = await write("/classes", { name: `PDF ${Date.now()}`, subject: "Toán" });
  const student = await write("/students", { name: "An báo cáo tháng" });
  const other = await write("/students", { name: "Bình không được chọn" });
  for (const s of [student, other]) await write(`/classes/${classroom.id}/enrollments`, { studentId: s.id, startDate: "2026-10-01" });
  if (withFee) {
    await write(`/billing/classes/${classroom.id}/rates`, { effectiveFrom: "2026-10-01", unitPriceVnd: "500000", version: 0 });
    const session = await write(`/classes/${classroom.id}/sessions`, { date: "2026-10-02", startTime: "18:00:00", endTime: "19:00:00", kind: "regular" });
    await write(`/classes/${classroom.id}/sessions/${session.id}`, { status: "held", version: 1 }, "PUT");
    await write(`/classes/${classroom.id}/sessions/${session.id}/attendance/${student.id}`, { status: "present", version: 0 }, "PUT");
    await expect.poll(async () => (await (await page.request.get(`/api/billing/classes/${classroom.id}/payments?month=2026-10`)).json()).status).toBe("ready");
  }
  await page.goto(`/?reportClass=${classroom.id}&reportStudent=${student.id}&reportMonth=2026-10#${classroom.id}`);
  const region = page.getByRole("region", { name: "Báo cáo PDF tháng", exact: true });
  await expect(region.getByLabel("Nhận xét báo cáo")).toBeVisible();
  return { region, classroom, student, write };
}

test("teacher edits monthly draft, previews and downloads only selected student", async ({ page }) => {
  const { region, classroom } = await setup(page, true);
  const comment = "Tiến bộ về phân số <script>window.reportInjected=true</script>";
  await region.getByLabel("Nhận xét báo cáo").fill(comment);
  await region.getByLabel("Lộ trình báo cáo").fill("Luyện 10 bài toán");
  await expect(region.getByRole("button", { name: "Xem trước báo cáo", exact: true })).toBeDisabled();
  await region.getByRole("button", { name: "Lưu bản nháp báo cáo" }).click();
  await region.getByRole("button", { name: "Xem trước báo cáo", exact: true }).click();
  const frame = page.frameLocator('iframe[title="Xem trước báo cáo tháng"]');
  await expect(frame.getByText(comment, { exact: true })).toBeVisible();
  await expect(frame.locator("p").filter({ hasText: /^Học sinh: An báo cáo tháng\s+Lớp:/ })).toBeVisible();
  await expect(frame.getByText("Bình không được chọn")).toHaveCount(0);
  expect(await page.evaluate(() => Reflect.get(window, "reportInjected"))).toBeUndefined();
  await expect(region.getByText("Tổng cần thu: 500.000 đồng", { exact: true })).toBeVisible();
  await region.getByRole("button", { name: "Xuất PDF", exact: true }).click();
  await expect(region.getByText("Chưa cấu hình thông tin thanh toán. Vui lòng liên hệ giáo viên.")).toBeVisible();
  const downloadEvent = page.waitForEvent("download");
  await region.getByRole("button", { name: "Tải PDF", exact: true }).first().click();
  const downloaded = await downloadEvent; const file = await downloaded.path();
  expect((await readFile(file!)).subarray(0, 5).toString()).toBe("%PDF-");
  await region.getByRole("button", { name: "Đối chiếu học phí" }).click();
  expect(new URL(page.url()).hash).toBe(`#${classroom.id}`);
  await page.reload();
  await expect(region.getByLabel("Nhận xét báo cáo")).toHaveValue(comment);
  await expect(region.getByLabel("Tháng báo cáo")).toHaveValue("2026-10");
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBeTruthy();
});

test("read-only access keeps saved preview and export available", async ({ page }) => {
  const { region, classroom, student, write } = await setup(page);
  const path = `/reporting/classes/${classroom.id}/students/${student.id}/months/2026-10`;
  await write(path + "/draft", { comment: "Bản đã lưu", roadmap: "Luyện đọc", emoji: "none", version: 0 }, "PUT");
  await page.route("**/api/auth/me", async route => {
    const response = await route.fetch(); const me = await response.json();
    await route.fulfill({ response, json: { ...me, access: { ...me.access, canWrite: false, status: "expired" } } });
  });
  await page.reload();
  await expect(region.getByLabel("Nhận xét báo cáo")).toBeDisabled();
  await region.getByRole("button", { name: "Xem trước báo cáo", exact: true }).click();
  await region.getByRole("button", { name: "Xuất PDF", exact: true }).click();
  await expect(region.getByRole("button", { name: "Tải PDF", exact: true })).toBeVisible();
  await expect(region.getByLabel("Nhận xét báo cáo")).toBeDisabled();
  expect((await (await page.request.get(`/api${path}/draft`)).json()).version).toBe(1);
});

test("uncertain export retries same command and source conflict preserves draft", async ({ page }) => {
  const { region, classroom, student, write } = await setup(page);
  await region.getByLabel("Nhận xét báo cáo").fill("Giữ nội dung khi lỗi nguồn");
  await region.getByRole("button", { name: "Lưu bản nháp báo cáo" }).click();
  await region.getByRole("button", { name: "Xem trước báo cáo", exact: true }).click();
  let first: unknown;
  await page.route("**/api/reporting/**/exports", async (route) => {
    if (route.request().method() !== "POST") return route.continue();
    first = route.request().postDataJSON(); await route.fetch(); await route.abort();
  }, { times: 1 });
  await region.getByRole("button", { name: "Xuất PDF", exact: true }).click();
  await expect(region.getByRole("button", { name: "Thử lại lệnh xuất" })).toBeVisible();
  const retry = page.waitForRequest(r => r.method() === "POST" && r.url().endsWith("/exports"));
  await region.getByRole("button", { name: "Thử lại lệnh xuất" }).click();
  expect((await retry).postDataJSON()).toEqual(first);
  await expect(region.getByRole("button", { name: "Tải PDF", exact: true })).toHaveCount(1);
  await region.getByRole("button", { name: "Xem trước báo cáo", exact: true }).click();
  await write(`/students/${student.id}`, { name: "Tên nguồn mới", version: 1 }, "PUT");
  await region.getByRole("button", { name: "Xuất PDF", exact: true }).click();
  await expect(region.getByRole("alert")).toContainText("làm mới");
  await expect(region.getByLabel("Nhận xét báo cáo")).toHaveValue("Giữ nội dung khi lỗi nguồn");
  expect(new URL(page.url()).hash).toBe(`#${classroom.id}`);
});
