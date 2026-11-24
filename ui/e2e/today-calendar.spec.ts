import { test, expect, type Page } from "@playwright/test";

const vietnamToday = () =>
  new Intl.DateTimeFormat("en-CA", { timeZone: "Asia/Ho_Chi_Minh" }).format(
    new Date(),
  );
const shift = (date: string, days: number) => {
  const d = new Date(`${date}T00:00:00Z`);
  d.setUTCDate(d.getUTCDate() + days);
  return d.toISOString().slice(0, 10);
};
const weekday = (date: string) => new Date(`${date}T00:00:00Z`).getUTCDay();

async function signIn(page: Page) {
  await page.goto("/");
  await page.getByLabel("Email", { exact: true }).fill(process.env.DEMO_EMAIL!);
  await page
    .getByLabel("Mật khẩu", { exact: true })
    .fill(process.env.DEMO_PASSWORD!);
  await page.getByRole("button", { name: "Đăng nhập", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Hôm nay" })).toBeVisible();
}
async function write(page: Page, path: string, data: unknown, method = "POST") {
  const csrf = await (await page.request.get("/api/auth/csrf")).json();
  const response = await page.request.fetch(`/api${path}`, {
    method,
    data,
    headers: { "X-CSRF-TOKEN": csrf.token },
  });
  expect(response.ok(), await response.text()).toBeTruthy();
  return response.json();
}

test("calendar shows the week, marks an unplanned slot and an overdue session, and keeps the picked day across views", async ({
  page,
}) => {
  await signIn(page);
  const today = vietnamToday();
  const name = `Lịch ${Date.now()}`;
  const classroom = await write(page, "/classes", { name, subject: "Toán" });
  // A 06:00 weekly slot on today's weekday; today's real session is at 18:00, so 06:00 is "Chưa tạo buổi".
  await write(
    page,
    `/classes/${classroom.id}/schedule`,
    {
      version: 0,
      effectiveFrom: "2020-01-01",
      slots: [
        {
          dayOfWeek: weekday(today),
          startTime: "06:00:00",
          endTime: "07:00:00",
        },
      ],
    },
    "PUT",
  );
  await write(page, `/classes/${classroom.id}/sessions`, {
    date: today,
    startTime: "18:00:00",
    endTime: "19:00:00",
    kind: "regular",
  });
  const yesterday = shift(today, -1);
  await write(page, `/classes/${classroom.id}/sessions`, {
    date: yesterday,
    startTime: "18:00:00",
    endTime: "19:00:00",
    kind: "regular",
  });
  await page.reload();

  const sessions = page
    .getByRole("list", { name: "Buổi học trong ngày" })
    .getByRole("listitem");
  const calendar = page.getByRole("region", { name: "Lịch tuần và tháng" });
  await expect(calendar).toBeVisible();
  const todayCell = calendar.locator(`[data-date="${today}"]`);
  await expect(todayCell).toHaveAttribute("aria-pressed", "true");
  await expect(todayCell).toHaveAttribute("aria-current", "date");
  await expect(todayCell).toHaveAccessibleName(/chưa tạo buổi/);
  await expect(calendar.getByText(`Chưa tạo buổi ngày`)).toBeVisible();
  await expect(calendar.getByText(`06:00–07:00 · ${name}`)).toBeVisible();

  // Month view: yesterday's planned session is overdue; the alert jumps to it.
  await calendar.getByRole("button", { name: "Xem tháng" }).click();
  await expect(
    calendar.getByRole("group", { name: "Lịch tháng" }),
  ).toBeVisible();
  await expect(calendar.getByRole("status")).toContainText("quá hạn");
  // The oldest day needing attention may belong to other classes of the account; it is never later than yesterday.
  await calendar.getByRole("button", { name: "Tới ngày cũ nhất" }).click();
  const jumped = await calendar
    .locator('[aria-pressed="true"]')
    .getAttribute("data-date");
  expect(jumped! <= yesterday).toBe(true);
  await calendar.locator(`[data-date="${yesterday}"]`).click();
  await expect(sessions.filter({ hasText: name })).toHaveCount(1);

  // Back to the week: the picked day survives the view change.
  const picked = calendar.locator('[aria-pressed="true"]');
  const pickedDate = await picked.getAttribute("data-date");
  await calendar.getByRole("button", { name: "Thu về tuần" }).click();
  await expect(
    calendar.getByRole("group", { name: "Lịch tuần" }),
  ).toBeVisible();
  await expect(calendar.locator('[aria-pressed="true"]')).toHaveAttribute(
    "data-date",
    pickedDate!,
  );

  // Arrow keys move the picked day and the session list follows.
  await calendar.locator('[aria-pressed="true"]').focus();
  await page.keyboard.press("ArrowRight");
  await expect(calendar.locator('[aria-pressed="true"]')).toHaveAttribute(
    "data-date",
    shift(pickedDate!, 1),
  );
  await calendar.getByRole("button", { name: "Về hôm nay" }).click();
  await expect(todayCell).toHaveAttribute("aria-pressed", "true");
  await expect(sessions.filter({ hasText: name })).toContainText("18:00");
  // The existing day buttons keep their accessible names.
  await expect(page.getByRole("button", { name: /Hôm trước/ })).toBeVisible();
  await expect(page.getByRole("button", { name: /Hôm sau/ })).toBeVisible();

  const overflow = await page.evaluate(
    () =>
      document.documentElement.scrollWidth -
      document.documentElement.clientWidth,
  );
  expect(overflow).toBeLessThanOrEqual(0);
});

test("bulk create previews every class, creates on confirm and reports per class", async ({
  page,
}) => {
  await signIn(page);
  const today = vietnamToday();
  const name = `Hàng loạt ${Date.now()}`;
  const classroom = await write(page, "/classes", { name, subject: "Toán" });
  await write(
    page,
    `/classes/${classroom.id}/schedule`,
    {
      version: 0,
      effectiveFrom: "2020-01-01",
      slots: [
        {
          dayOfWeek: weekday(today),
          startTime: "05:00:00",
          endTime: "05:45:00",
        },
      ],
    },
    "PUT",
  );
  await page.reload();
  const calendar = page.getByRole("region", { name: "Lịch tuần và tháng" });
  await calendar
    .getByRole("button", { name: /Tạo \d+ buổi chưa có trong tuần này/ })
    .click();
  const dialog = page.getByRole("dialog");
  await expect(dialog).toContainText(`${name}: 1 buổi`);
  await dialog.getByRole("button", { name: /Xác nhận tạo/ }).click();
  await expect(
    dialog.getByRole("listitem").filter({ hasText: name }),
  ).toContainText("Đã tạo 1 buổi.");
  await dialog.locator("button.primary", { hasText: "Đóng" }).click();
  const sessions = page
    .getByRole("list", { name: "Buổi học trong ngày" })
    .getByRole("listitem");
  await expect(sessions.filter({ hasText: name })).toContainText("05:00");
  // Nothing left to create for that class: it no longer appears among the unplanned slots.
  await expect(calendar.getByText(`05:00–05:45 · ${name}`)).toHaveCount(0);
});
