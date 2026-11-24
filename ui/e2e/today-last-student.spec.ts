import { test, expect } from "@playwright/test";

const vietnamToday = () =>
  new Intl.DateTimeFormat("en-CA", { timeZone: "Asia/Ho_Chi_Minh" }).format(
    new Date(),
  );

// The sticky "Lưu điểm danh" bar must never sit on top of a student row, or that student cannot be tapped.
test("the last student of a long roster can be tapped while the save bar floats", async ({
  page,
}) => {
  await page.goto("/");
  await page.getByLabel("Email", { exact: true }).fill(process.env.DEMO_EMAIL!);
  await page
    .getByLabel("Mật khẩu", { exact: true })
    .fill(process.env.DEMO_PASSWORD!);
  await page.getByRole("button", { name: "Đăng nhập", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Hôm nay" })).toBeVisible();
  async function write(path: string, data: unknown) {
    const csrf = await (await page.request.get("/api/auth/csrf")).json();
    const response = await page.request.fetch(`/api${path}`, {
      method: "POST",
      data,
      headers: { "X-CSRF-TOKEN": csrf.token },
    });
    expect(response.ok(), await response.text()).toBeTruthy();
    return response.json();
  }
  const name = `Dài ${Date.now()}`;
  const classroom = await write("/classes", { name, subject: "Toán" });
  for (let i = 1; i <= 9; i++) {
    const student = await write("/students", { name: `Học sinh dài ${i}` });
    await write(`/classes/${classroom.id}/enrollments`, {
      studentId: student.id,
      startDate: "2020-01-01",
    });
  }
  await write(`/classes/${classroom.id}/sessions`, {
    date: vietnamToday(),
    startTime: "18:00:00",
    endTime: "19:00:00",
    kind: "regular",
  });
  await page.reload();
  const card = page
    .getByRole("list", { name: "Buổi học trong ngày" })
    .getByRole("listitem")
    .filter({ hasText: name });
  await card.getByRole("button", { expanded: false }).first().click();
  const rows = card
    .getByRole("list", { name: `Điểm danh ${name}` })
    .getByRole("button");
  await expect(rows).toHaveCount(9);
  const last = rows.nth(8);
  await last.scrollIntoViewIfNeeded();
  const covered = await last.evaluate((el) => {
    const r = el.getBoundingClientRect();
    const hit = document.elementFromPoint(
      r.x + r.width / 2,
      r.y + r.height / 2,
    );
    return !(hit === el || el.contains(hit));
  });
  expect(covered).toBe(false);
  // And a real tap toggles it.
  await last.click();
  await expect(last).toContainText("Vắng");
});
