import { test, expect } from "@playwright/test";

const vietnamToday = () =>
  new Intl.DateTimeFormat("en-CA", { timeZone: "Asia/Ho_Chi_Minh" }).format(
    new Date(),
  );

test("teacher opens today's sessions, flips only the absent student, saves and undoes", async ({
  page,
}) => {
  await page.goto("/");
  await page.getByLabel("Email", { exact: true }).fill(process.env.DEMO_EMAIL!);
  await page
    .getByLabel("Mật khẩu", { exact: true })
    .fill(process.env.DEMO_PASSWORD!);
  await page.getByRole("button", { name: "Đăng nhập", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Hôm nay" })).toBeVisible();

  async function write(path: string, data: unknown, method = "POST") {
    const csrf = await (await page.request.get("/api/auth/csrf")).json();
    const response = await page.request.fetch(`/api${path}`, {
      method,
      data,
      headers: { "X-CSRF-TOKEN": csrf.token },
    });
    expect(response.ok()).toBeTruthy();
    return response.json();
  }
  const name = `Hôm nay ${Date.now()}`;
  const classroom = await write("/classes", { name, subject: "Toán" });
  for (const student of ["An hôm nay", "Bình hôm nay", "Chi hôm nay"]) {
    const created = await write("/students", { name: student });
    await write(`/classes/${classroom.id}/enrollments`, {
      studentId: created.id,
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
  const card = page.getByRole("listitem").filter({ hasText: name }).first();
  await expect(card).toContainText("0/3 đã điểm danh");
  await card.getByRole("button", { expanded: false }).first().click();

  // Everyone defaults to present; one tap marks the exception.
  const board = card.getByRole("list", { name: `Điểm danh ${name}` });
  await expect(board.getByRole("button", { name: /có mặt/i })).toHaveCount(3);
  await board.getByRole("button", { name: /Bình hôm nay/ }).click();
  await card.getByRole("button", { name: "Lưu điểm danh" }).click();
  await expect(card.getByRole("status")).toContainText("Đã lưu điểm danh (3");
  await expect(card).toContainText("3/3 đã điểm danh");

  await page.reload();
  const reloaded = page.getByRole("listitem").filter({ hasText: name }).first();
  await expect(reloaded).toContainText("Đã diễn ra");
  await expect(reloaded).toContainText("3/3 đã điểm danh");

  await reloaded.getByRole("button", { expanded: false }).first().click();
  await expect(
    reloaded.getByRole("button", { name: /Bình hôm nay: Vắng/ }),
  ).toBeVisible();
  await reloaded.getByRole("button", { name: /Bình hôm nay/ }).click();
  await reloaded.getByRole("button", { name: "Lưu điểm danh" }).click();
  await expect(reloaded.getByRole("status")).toContainText("Đã lưu điểm danh (1");
  await reloaded.getByRole("button", { name: "Hoàn tác" }).click();
  await expect(reloaded.getByRole("status")).toContainText("Đã hoàn tác");
  await expect(
    reloaded.getByRole("button", { name: /Bình hôm nay: Vắng/ }),
  ).toBeVisible();
});
