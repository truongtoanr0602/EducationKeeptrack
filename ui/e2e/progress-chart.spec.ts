import { test, expect } from "@playwright/test";

test("teacher sees both indicators over time, gaps stay gaps and sparse data says so", async ({
  page,
}) => {
  await page.goto("/");
  await page.getByLabel("Email", { exact: true }).fill(process.env.DEMO_EMAIL!);
  await page
    .getByLabel("Mật khẩu", { exact: true })
    .fill(process.env.DEMO_PASSWORD!);
  await page.getByRole("button", { name: "Đăng nhập", exact: true }).click();
  await expect(
    page.getByRole("button", { name: "Tạo lớp mới", exact: true }),
  ).toBeVisible();
  async function send(method: "post" | "put", path: string, data: unknown) {
    const csrf = await (await page.request.get("/api/auth/csrf")).json();
    const response = await page.request[method](`/api${path}`, {
      headers: { "X-CSRF-TOKEN": csrf.token },
      data,
    });
    expect(response.ok()).toBeTruthy();
    return response.json();
  }
  const classroom = await send("post", "/classes", {
    name: `Tiến độ ${Date.now()}`,
    subject: "Toán",
  });
  const student = await send("post", "/students", { name: "Bình tiến độ" });
  await send("post", `/classes/${classroom.id}/enrollments`, {
    studentId: student.id,
    startDate: "2026-01-01",
  });
  const base = `/learning/classes/${classroom.id}/assessments`;
  const today = new Date();
  const day = (offset: number) => {
    const d = new Date(today);
    d.setDate(d.getDate() - offset);
    return d.toISOString().slice(0, 10);
  };
  async function assess(
    offset: number,
    title: string,
    result?: { status: string; completion: number | null; correctness: number | null },
  ) {
    const made = await send("post", base, {
      title,
      date: day(offset),
      defaultMethod: "manual",
    });
    if (result)
      await send("put", `${base}/${made.id}/submissions/${student.id}`, {
        ...result,
        version: 1,
        confirm: true,
      });
  }
  await assess(30, "Bài một", { status: "assessed", completion: 100, correctness: 75 });
  await assess(20, "Bài hai", { status: "not_done", completion: 0, correctness: 0 });
  await assess(10, "Bài ba", { status: "unassigned", completion: null, correctness: null });
  await page.goto(`/#/classes/${classroom.id}/assessments`);
  await page.reload();
  const panel = page.getByRole("region", { name: "Tiến độ học sinh" });
  await expect(
    panel.getByRole("img", { name: /Bình tiến độ/ }),
  ).toBeVisible();
  // Two plotted points (a real 0 counts), one gap; both are explained, none is drawn as a trend of decline from missing data.
  await expect(panel.getByTestId("progress-completeness")).toContainText(
    "Đã vẽ 2/3 bài · 1 bài không giao",
  );
  await expect(panel.getByTestId("progress-insufficient")).toHaveCount(0);
  const rows = panel.getByRole("table").getByRole("row");
  await expect(rows).toHaveCount(4);
  await expect(rows.nth(1)).toContainText("100%");
  await expect(rows.nth(1)).toContainText("75%");
  await expect(rows.nth(2)).toContainText("Không làm bài");
  await expect(rows.nth(2)).toContainText("0%");
  await expect(rows.nth(3)).toContainText("Không giao bài");
  await expect(rows.nth(3)).toContainText("—");
  // Narrowing the range to a single assessment says the data is not enough for a trend.
  await panel.getByLabel("Từ ngày").fill(day(31));
  await panel.getByLabel("Đến ngày").fill(day(29));
  await expect(panel.getByTestId("progress-insufficient")).toBeVisible();
  await expect(panel.getByTestId("progress-completeness")).toContainText(
    "Đã vẽ 1/1 bài",
  );
});
