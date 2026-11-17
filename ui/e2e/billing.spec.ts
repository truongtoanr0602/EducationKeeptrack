import { test, expect } from "@playwright/test";

test("teacher sees debts across classes, records a partial receipt and the debt drops", async ({
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
  const stamp = Date.now();
  const studentName = `An nợ ${stamp}`;
  const className = `Hóa đơn ${stamp}`;
  const classroom = await write("/classes", {
    name: className,
    subject: "Toán",
  });
  const student = await write("/students", { name: studentName });
  await write(`/classes/${classroom.id}/enrollments`, {
    studentId: student.id,
    startDate: "2026-10-01",
  });
  await write(`/billing/classes/${classroom.id}/rates`, {
    effectiveFrom: "2026-10-01",
    unitPriceVnd: "500000",
    version: 0,
  });
  const session = await write(`/classes/${classroom.id}/sessions`, {
    date: "2026-10-02",
    startTime: "18:00:00",
    endTime: "19:00:00",
    kind: "regular",
  });
  await write(
    `/classes/${classroom.id}/sessions/${session.id}`,
    { status: "held", version: 1 },
    "PUT",
  );
  await write(
    `/classes/${classroom.id}/sessions/${session.id}/attendance/${student.id}`,
    { status: "present", version: 0 },
    "PUT",
  );
  await expect
    .poll(async () => {
      const report = await (
        await page.request.get(
          `/api/billing/classes/${classroom.id}/payments?month=2026-10`,
        )
      ).json();
      return report.status;
    })
    .toBe("ready");

  await page.evaluate(() => (window.location.hash = "#/billing"));
  // The class list is loaded at sign-in, so reload to see the class created through the API.
  await page.reload();
  await expect(page.getByRole("heading", { name: "Hóa đơn" })).toBeVisible();
  await page.getByRole("combobox", { name: /^Lớp/ }).selectOption(classroom.id);
  const card = page.getByRole("listitem").filter({ hasText: studentName });
  await expect(card).toContainText(className);
  await expect(card).toContainText("500.000");

  await card.getByRole("button", { name: "Ghi nhận đã thu" }).click();
  const form = card.getByRole("form", { name: "Ghi nhận tiền" });
  await expect(form.getByLabel("Số tiền nhận (đồng)")).toHaveValue("500000");
  await form.getByLabel("Số tiền nhận (đồng)").fill("200000");
  await form.getByRole("button", { name: /Ghi nhận|Lưu/ }).click();

  await expect(card).toContainText("300.000");
  await page.reload();
  await page.getByRole("combobox", { name: /^Lớp/ }).selectOption(classroom.id);
  await expect(
    page.getByRole("listitem").filter({ hasText: studentName }),
  ).toContainText("300.000");
});
