import { test, expect } from "@playwright/test";

test("fee adjustment shows its reason and matching debt after reload", async ({
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
    name: `Đối chiếu ${Date.now()}`,
    subject: "Toán",
  });
  const student = await write("/students", { name: "An đối chiếu" });
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
  const attendance = `/classes/${classId}/sessions/${session.id}/attendance/${student.id}`;
  await write(attendance, { status: "present", version: 0 }, "PUT");
  await page.goto(`/?tuitionMonth=2026-10#${classId}`, {
    waitUntil: "domcontentloaded",
  });
  const payments = page.getByRole("region", { name: "Thanh toán và công nợ" });
  const card = payments.getByRole("article", { name: "Công nợ An đối chiếu" });
  await expect(
    card.getByText("Tổng cần thu: 500.000", { exact: false }),
  ).toBeVisible();
  await card.getByLabel("Số tiền nhận (đồng)").fill("500000");
  await expect(
    card.getByRole("button", { name: "Ghi nhận tiền", exact: true }),
  ).toBeEnabled();
  await card
    .getByRole("button", { name: "Ghi nhận tiền", exact: true })
    .click();
  await expect(
    card.getByText("Tổng cần thu: 0", { exact: false }),
  ).toBeVisible();
  const fees = page.getByRole("article").filter({
    has: page.getByRole("heading", {
      name: "Học phí: An đối chiếu",
      exact: true,
    }),
  });
  await fees
    .getByRole("button", { name: "Chi tiết An đối chiếu", exact: true })
    .click();
  const form = fees.getByRole("form", { name: "Điều chỉnh học phí" });
  await expect(form).toBeVisible();
  await form.getByLabel("Chiều điều chỉnh").selectOption("decrease");
  await form.getByLabel("Số tiền điều chỉnh (đồng)").fill("200000");
  await form.getByLabel("Lý do điều chỉnh").fill("Hỗ trợ học sinh");
  await page.route("**/api/billing/classes/*/adjustments", async (route) => {
    const response = await route.fetch();
    expect(response.status()).toBe(201);
    await route.abort("failed");
  });
  await form
    .getByRole("button", { name: "Lưu điều chỉnh", exact: true })
    .click();
  await expect(form.getByRole("alert")).toContainText("chưa rõ");
  await page.unroute("**/api/billing/classes/*/adjustments");
  await fees
    .getByRole("button", { name: "Chi tiết An đối chiếu", exact: true })
    .click();
  await fees
    .getByRole("button", { name: "Chi tiết An đối chiếu", exact: true })
    .click();
  await form
    .getByRole("button", { name: "Thử lại điều chỉnh", exact: true })
    .click();
  await expect(
    fees.getByRole("list", { name: "Lịch sử điều chỉnh" }),
  ).toContainText("Hỗ trợ học sinh");
  await expect(
    card.getByText("Tiền giữ để đối chiếu: 200.000", { exact: false }),
  ).toBeVisible();
  await page.reload({ waitUntil: "domcontentloaded" });
  await fees
    .getByRole("button", { name: "Chi tiết An đối chiếu", exact: true })
    .click();
  await expect(
    fees
      .getByRole("list", { name: "Lịch sử điều chỉnh" })
      .getByRole("listitem"),
  ).toHaveCount(1);
  await expect(fees).toContainText("Hỗ trợ học sinh");
  await expect(fees).toContainText("Người ghi:");
  await expect(fees).toContainText("300.000");
  await expect(fees.getByRole("table")).toContainText("500.000");
  await expect(
    card.getByText("Tổng cần thu: 0", { exact: false }),
  ).toBeVisible();
  await expect(
    card.getByText("Tiền giữ để đối chiếu: 200.000", { exact: false }),
  ).toBeVisible();
});
