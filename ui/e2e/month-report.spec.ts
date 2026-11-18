import { test, expect } from "@playwright/test";

const month = "2031-03";

test("Báo cáo shows the month as cards that move with the data", async ({
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
  const read = async (path: string) =>
    (await page.request.get(`/api${path}`)).json();
  // Earlier runs leave data in the same month, so compare against the figures before this test.
  const before = {
    attendance: await read(`/reports/attendance?month=${month}`),
    money: await read(`/billing/summary?month=${month}`),
  };

  const stamp = Date.now();
  const classroom = await write("/classes", {
    name: `Báo cáo ${stamp}`,
    subject: "Toán",
  });
  const student = await write("/students", { name: `An báo cáo ${stamp}` });
  await write(`/classes/${classroom.id}/enrollments`, {
    studentId: student.id,
    startDate: `${month}-01`,
  });
  await write(`/billing/classes/${classroom.id}/rates`, {
    effectiveFrom: `${month}-01`,
    unitPriceVnd: "500000",
    version: 0,
  });
  const session = await write(`/classes/${classroom.id}/sessions`, {
    date: `${month}-04`,
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
  let version = 0;
  await expect
    .poll(async () => {
      const report = await read(
        `/billing/classes/${classroom.id}/payments?month=${month}`,
      );
      version = report.version;
      return report.status;
    })
    .toBe("ready");
  await write(`/billing/classes/${classroom.id}/payments`, {
    studentId: student.id,
    amountVnd: "200000",
    receivedOn: `${month}-10`,
    note: "",
    idempotencyKey: crypto.randomUUID(),
    version,
  });

  const after = {
    attendance: await read(`/reports/attendance?month=${month}`),
    money: await read(`/billing/summary?month=${month}`),
  };
  expect(after.attendance.held - before.attendance.held).toBe(1);
  expect(after.attendance.present - before.attendance.present).toBe(1);
  expect(
    BigInt(after.money.receivedVnd) - BigInt(before.money.receivedVnd),
  ).toBe(200000n);
  expect(BigInt(after.money.debtVnd) - BigInt(before.money.debtVnd)).toBe(
    300000n,
  );

  await page.evaluate(() => (window.location.hash = "#/reports"));
  await page.reload();
  await expect(
    page.getByRole("heading", { name: "Báo cáo", exact: true }),
  ).toBeVisible();
  await page.getByLabel("Tháng").fill(month);
  const cards = page.getByRole("list", { name: "Số liệu tháng" });
  await expect(
    cards.getByRole("listitem").filter({ hasText: "Buổi đã dạy" }),
  ).toContainText(String(after.attendance.held));
  const percent = `${Math.round(after.attendance.attendanceRate * 100)}%`;
  await expect(
    cards.getByRole("listitem").filter({ hasText: "Chuyên cần" }),
  ).toContainText(percent);
  const received = BigInt(after.money.receivedVnd).toLocaleString("vi-VN");
  await expect(
    cards.getByRole("listitem").filter({ hasText: "Đã thu trong tháng" }),
  ).toContainText(received);
  await expect(
    page.getByRole("link", { name: /Báo cáo tháng →/ }).first(),
  ).toBeVisible();
});
