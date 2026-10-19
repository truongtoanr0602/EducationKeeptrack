import { test, expect } from "@playwright/test";

test("cashflow and period totals reconcile with details after correction and reload", async ({
  page,
}) => {
  await page.goto("/", { waitUntil: "domcontentloaded" });
  await page.getByLabel("Email", { exact: true }).fill(process.env.DEMO_EMAIL!);
  await page
    .getByLabel("Mật khẩu", { exact: true })
    .fill(process.env.DEMO_PASSWORD!);
  await page.getByRole("button", { name: "Đăng nhập", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "Lớp học của tôi" }),
  ).toBeVisible();
  async function write(path: string, data: unknown, method = "POST") {
    const csrf = await (await page.request.get("/api/auth/csrf")).json();
    const response = await page.request.fetch(`/api${path}`, {
      method,
      headers: { "X-CSRF-TOKEN": csrf.token },
      data,
    });
    expect(response.ok()).toBeTruthy();
    return response.json();
  }
  const classroom = await write("/classes", {
    name: `Sửa thu ${Date.now()}`,
    subject: "Toán",
  });
  const student = await write("/students", { name: "An sửa thu" });
  const classId = classroom.id;
  await write(`/classes/${classId}/enrollments`, {
    studentId: student.id,
    startDate: "2026-10-01",
  });
  await write(`/billing/classes/${classId}/rates`, {
    effectiveFrom: "2026-10-01",
    unitPriceVnd: "500000",
    version: 0,
  });
  const session = await write(`/classes/${classId}/sessions`, {
    date: "2026-10-02",
    startTime: "18:00:00",
    endTime: "19:00:00",
    kind: "regular",
  });
  await write(
    `/classes/${classId}/sessions/${session.id}`,
    { status: "held", version: 1 },
    "PUT",
  );
  await write(
    `/classes/${classId}/sessions/${session.id}/attendance/${student.id}`,
    { status: "present", version: 0 },
    "PUT",
  );
  await expect
    .poll(
      async () =>
        (
          await (
            await page.request.get(
              `/api/billing/classes/${classId}/payments?month=2026-11`,
            )
          ).json()
        ).status,
    )
    .toBe("ready");
  async function receive(amount: string, date: string) {
    const report = await (
      await page.request.get(
        `/api/billing/classes/${classId}/payments?month=2026-11`,
      )
    ).json();
    return write(`/billing/classes/${classId}/payments`, {
      studentId: student.id,
      amountVnd: amount,
      receivedOn: date,
      version: report.version,
      idempotencyKey: crypto.randomUUID(),
    });
  }
  const receipt = await receive("500000", "2026-11-05");
  await receive("100000", "2026-11-06");
  await page.goto(`/?tuitionMonth=2026-10#${classId}`, {
    waitUntil: "domcontentloaded",
  });
  const stats = page.getByRole("region", {
    name: "Thống kê thu tiền và công nợ",
    exact: true,
  });
  await expect(stats).toBeVisible();
  await stats.getByLabel("Từ ngày thực nhận").fill("2026-11-01");
  await stats.getByLabel("Đến ngày thực nhận").fill("2026-11-30");
  await stats
    .getByRole("button", { name: "Tải thống kê", exact: true })
    .click();
  const total = stats.getByRole("status", {
    name: "Tổng thực thu",
    exact: true,
  });
  await expect(total).toContainText("600.000");
  const table = stats.getByRole("table", {
    name: "Chi tiết thực thu",
    exact: true,
  });
  await expect(table.locator("tbody tr")).toHaveCount(2);
  const amounts = await table
    .locator("tbody tr")
    .evaluateAll((rows) =>
      rows.map((row) => row.children[2].textContent ?? ""),
    );
  const sum = amounts.reduce(
    (value, amount) => value + BigInt(amount.replace(/[^0-9]/g, "")),
    0n,
  );
  expect(sum).toBe(600000n);
  expect(BigInt((await total.innerText()).replace(/[^0-9]/g, ""))).toBe(sum);
  await table
    .getByRole("button", {
      name: "Đối chiếu khoản nhận 2026-11-05",
      exact: true,
    })
    .click();
  await expect(
    stats.getByRole("region", { name: "Đối chiếu giao dịch", exact: true }),
  ).toContainText(receipt.id);
  await expect(
    stats.getByRole("region", { name: "Đối chiếu giao dịch", exact: true }),
  ).toContainText("2026-10: 500.000");
  await stats
    .getByRole("button", { name: "Mở lịch sử khoản nhận tiền", exact: true })
    .click();
  expect(new URL(page.url()).hash).toBe(`#${classId}`);
  await stats
    .getByRole("button", { name: "Công nợ theo kỳ học", exact: true })
    .click();
  await stats.getByLabel("Kỳ nghĩa vụ").fill("2026-10");
  await stats
    .getByRole("button", { name: "Tải thống kê", exact: true })
    .click();
  await expect(
    stats.getByRole("status", { name: "Tổng công nợ kỳ", exact: true }),
  ).toContainText("0");
  await expect(
    stats.getByRole("status", { name: "Tổng đã thanh toán kỳ", exact: true }),
  ).toContainText("500.000");
  const periods = stats.getByRole("table", {
    name: "Chi tiết công nợ kỳ",
    exact: true,
  });
  const paid = await periods
    .locator("tbody tr")
    .evaluateAll((rows) =>
      rows.map((row) => row.children[2].textContent ?? ""),
    );
  expect(
    paid.reduce(
      (sum, amount) => sum + BigInt(amount.replace(/[^0-9]/g, "")),
      0n,
    ),
  ).toBe(500000n);
  await periods
    .getByRole("button", { name: "Đối chiếu kỳ An sửa thu", exact: true })
    .click();
  await expect(
    stats.getByRole("region", { name: "Đối chiếu kỳ", exact: true }),
  ).toContainText("2026-11-05");
  const current = await (
    await page.request.get(
      `/api/billing/classes/${classId}/payments?month=2026-11`,
    )
  ).json();
  await write(
    `/billing/classes/${classId}/payments/${receipt.id}/corrections`,
    {
      action: "cancel",
      reason: "Nhập trùng",
      idempotencyKey: crypto.randomUUID(),
      version: current.version,
    },
  );
  await stats
    .getByRole("button", { name: "Tải thống kê", exact: true })
    .click();
  await expect(
    stats.getByRole("status", { name: "Tổng công nợ kỳ", exact: true }),
  ).toContainText("500.000");
  await stats
    .getByRole("button", { name: "Thực thu theo ngày nhận", exact: true })
    .click();
  await expect(total).toContainText("100.000");
  await page.reload({ waitUntil: "domcontentloaded" });
  await expect(total).toContainText("100.000");
  await expect(table.locator("tbody tr")).toHaveCount(1);
  await page.route("**/api/billing/classes/*/cashflow?*", (route) =>
    route.fulfill({
      status: 503,
      contentType: "application/json",
      body: JSON.stringify({ message: "Tạm thời không tải được thống kê." }),
    }),
  );
  await stats
    .getByRole("button", { name: "Tải thống kê", exact: true })
    .click();
  await expect(stats.getByRole("alert")).toContainText("Tạm thời");
  await expect(total).toHaveCount(0);
  await expect(stats.getByLabel("Từ ngày thực nhận")).toHaveValue("2026-11-01");
  await page.unroute("**/api/billing/classes/*/cashflow?*");
  await stats
    .getByRole("button", { name: "Tải thống kê", exact: true })
    .click();
  await expect(total).toContainText("100.000");
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth,
    ),
  ).toBeTruthy();
});
