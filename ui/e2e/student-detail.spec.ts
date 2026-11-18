import { test, expect } from "@playwright/test";

test("teacher finds a student across classes and opens their detail", async ({
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
    expect(response.ok()).toBeTruthy();
    return response.json();
  }
  const stamp = Date.now();
  const studentName = `Dũng đa lớp ${stamp}`;
  const first = await write("/classes", {
    name: `Toán ${stamp}`,
    subject: "Toán",
  });
  const second = await write("/classes", {
    name: `Lý ${stamp}`,
    subject: "Lý",
  });
  const student = await write("/students", {
    name: studentName,
    parentName: "Cô Lan",
    phone: "0900000001",
  });
  for (const classroom of [first, second])
    await write(`/classes/${classroom.id}/enrollments`, {
      studentId: student.id,
      startDate: "2026-10-01",
    });

  await page.evaluate(() => (window.location.hash = "#/classes"));
  await page.reload();
  const search = page.getByLabel("Tìm học sinh");
  await search.fill(`đa lớp ${stamp}`);
  await page.getByRole("link", { name: new RegExp(studentName) }).click();

  await expect(page.getByRole("heading", { name: studentName })).toBeVisible();
  await expect(page.getByLabel("Liên hệ")).toContainText("Cô Lan");
  await expect(page.getByRole("link", { name: "0900000001" })).toHaveAttribute(
    "href",
    "tel:0900000001",
  );
  const classes = page.getByRole("list", { name: "Lớp của học sinh" });
  await expect(classes.getByRole("listitem")).toHaveCount(2);
  await expect(classes).toContainText(`Toán ${stamp}`);
  await expect(classes).toContainText(`Lý ${stamp}`);

  await classes
    .getByRole("listitem")
    .filter({ hasText: `Toán ${stamp}` })
    .getByRole("link", { name: "Học phí" })
    .click();
  await expect(page).toHaveURL(new RegExp(`/classes/${first.id}/tuition`));
});
