import { test, expect } from "@playwright/test";

test("teacher sees which saved PDFs are outdated, reads what changed and exports an updated one", async ({ page }) => {
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
  async function heldSession(classId: string, studentId: string, date: string) {
    const session = await write(`/classes/${classId}/sessions`, { date, startTime: "18:00:00", endTime: "19:00:00", kind: "regular" });
    await write(`/classes/${classId}/sessions/${session.id}`, { status: "held", version: 1 }, "PUT");
    await write(`/classes/${classId}/sessions/${session.id}/attendance/${studentId}`, { status: "present", version: 0 }, "PUT");
    await expect.poll(async () => (await (await page.request.get(`/api/billing/classes/${classId}/payments?month=2026-10`)).json()).status).toBe("ready");
  }
  const classroom = await write("/classes", { name: `Lỗi thời ${Date.now()}`, subject: "Toán" });
  const student = await write("/students", { name: "An lỗi thời" });
  await write(`/classes/${classroom.id}/enrollments`, { studentId: student.id, startDate: "2026-10-01" });
  await write(`/billing/classes/${classroom.id}/rates`, { effectiveFrom: "2026-10-01", unitPriceVnd: "500000", version: 0 });
  await heldSession(classroom.id, student.id, "2026-10-02");
  await page.goto(`/?reportClass=${classroom.id}&reportStudent=${student.id}&reportMonth=2026-10#/classes/${classroom.id}/reports`);
  const region = page.getByRole("region", { name: "Báo cáo PDF tháng", exact: true });
  await expect(region.getByLabel("Nhận xét báo cáo")).toBeVisible();

  await region.getByLabel("Nhận xét báo cáo").fill("Bản đầu tiên");
  await region.getByRole("button", { name: "Lưu bản nháp báo cáo" }).click();
  await region.getByRole("button", { name: "Xem trước báo cáo", exact: true }).click();
  await region.getByRole("button", { name: "Xuất PDF", exact: true }).click();
  await expect(region.getByTestId("freshness")).toHaveText("Còn mới: khớp dữ liệu hiện tại");
  await expect(region.getByTestId("outdated-banner")).toHaveCount(0);

  // Another session is held elsewhere (here through the API): the saved PDF is now behind the data.
  await heldSession(classroom.id, student.id, "2026-10-09");
  await region.getByRole("button", { name: "Kiểm tra lại bản xuất" }).click();
  await expect(region.getByTestId("outdated-banner")).toContainText("Có 1 bản PDF đã lỗi thời");
  await expect(region.getByTestId("freshness")).toContainText("Đã lỗi thời");
  await region.getByText("Xem thay đổi (").click();
  await expect(region.getByText("1 buổi có mặt trên 1 buổi đã diễn ra → 2 buổi có mặt trên 2 buổi đã diễn ra")).toBeVisible();
  await expect(region.getByText("Học phí phát sinh trong tháng: 500.000 đồng → 1.000.000 đồng")).toBeVisible();

  // Update: preview from current data, export; the old file stays and is marked outdated, the new one is current.
  await region.getByRole("button", { name: "Cập nhật báo cáo" }).click();
  await region.getByRole("button", { name: "Xuất PDF", exact: true }).click();
  await expect(region.locator("article[data-freshness='current']")).toHaveCount(1);
  await expect(region.locator("article[data-freshness='outdated']")).toHaveCount(1);
  await expect(region.getByRole("button", { name: "Tải PDF" })).toHaveCount(2);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBeTruthy();
});
