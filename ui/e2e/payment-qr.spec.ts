import { test, expect } from "@playwright/test";

test("recipient preview and QR modes retain manual instructions without recording money", async ({
  page,
}) => {
  await page.goto("/");
  await page.getByLabel("Email", { exact: true }).fill(process.env.DEMO_EMAIL!);
  await page
    .getByLabel("Mật khẩu", { exact: true })
    .fill(process.env.DEMO_PASSWORD!);
  await page.getByRole("button", { name: "Đăng nhập", exact: true }).click();
  await page.getByRole("button", { name: "Tạo lớp mới", exact: true }).click();
  const name = `Lớp QR ${Date.now()}`;
  await page.getByLabel("Tên lớp", { exact: true }).fill(name);
  await page.getByLabel("Môn học", { exact: true }).fill("Toán");
  await page.getByRole("button", { name: "Lưu lớp", exact: true }).click();
  await page
    .getByRole("article")
    .filter({ has: page.getByRole("heading", { name, exact: true }) })
    .getByRole("button", { name: "Danh sách học sinh" })
    .click();
  await page
    .getByRole("button", { name: "Thêm học sinh mới", exact: true })
    .click();
  await page.getByLabel("Tên học sinh", { exact: true }).fill("An QR");
  await page.getByLabel("Ngày bắt đầu", { exact: true }).fill("2026-10-01");
  await page.getByRole("button", { name: "Lưu học sinh", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "An QR", exact: true }),
  ).toBeVisible();
  const settings = page.getByRole("region", {
    name: "Tài khoản nhận tiền",
    exact: true,
  });
  await expect(settings.getByLabel("Mã BIN ngân hàng")).toBeEnabled();
  await settings.getByLabel("Mã BIN ngân hàng").fill("970415");
  await settings.getByLabel("Ngân hàng", { exact: true }).fill("VietinBank");
  await settings.getByLabel("Số tài khoản", { exact: true }).fill("123456789");
  await settings
    .getByLabel("Tên người nhận (viết hoa không dấu)")
    .fill("NGUYEN VAN AN");
  await expect(settings).toContainText(
    "Xem trước người nhận: VietinBank · 123456789 · NGUYEN VAN AN",
  );
  await settings
    .getByRole("button", { name: "Lưu tài khoản nhận tiền", exact: true })
    .click();
  await expect(
    settings.getByRole("button", {
      name: "Lưu tài khoản nhận tiền",
      exact: true,
    }),
  ).toBeEnabled();
  const classId = new URL(page.url()).hash.slice(1);
  const students = await (
    await page.request.get(`/api/classes/${classId}/enrollments`)
  ).json();
  const studentId = students[0].studentId;
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
  await write(`/billing/classes/${classId}/rates`, {
    effectiveFrom: "2026-10-01",
    unitPriceVnd: "700000",
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
    `/classes/${classId}/sessions/${session.id}/attendance/${studentId}`,
    { status: "present", version: 0 },
    "PUT",
  );
  const payments = page.getByRole("region", {
    name: "Thanh toán và công nợ",
    exact: true,
  });
  await payments
    .getByRole("button", { name: "Tải lại công nợ", exact: true })
    .click();
  const student = payments.getByRole("article", { name: "Công nợ An QR" });
  const request = student.getByRole("region", {
    name: "Yêu cầu chuyển khoản",
    exact: true,
  });
  await expect(student).toContainText("Tổng cần thu: 700.000");
  const report = await (
    await page.request.get(
      `/api/billing/classes/${classId}/payments?month=2026-10`,
    )
  ).json();
  const recipient = await (
    await page.request.get("/api/billing/recipient")
  ).json();
  let kind = "vietqr";
  let brokenImage = false;
  await page.route("**/transfer-request", (route) =>
    route.fulfill({
      json: {
        ...recipient,
        recipientVersion: recipient.version,
        version: report.version,
        amountVnd: "700000",
        content: "HP202610AN",
        qrKind: kind,
        staticQrDataUrl:
          "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aZWQAAAAASUVORK5CYII=",
        qrDataUrl: brokenImage
          ? "data:image/png;base64,AAAA"
          : kind === "manual"
            ? null
            : "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aZWQAAAAASUVORK5CYII=",
      },
    }),
  );
  await request
    .getByRole("button", { name: "Tạo yêu cầu chuyển khoản" })
    .click();
  await expect(request).toContainText("VietQR có số tiền và nội dung");
  await expect(
    request.getByRole("img", { name: "VietQR chuyển khoản" }),
  ).toBeVisible();
  for (const fallback of ["static", "manual"]) {
    kind = fallback;
    await request
      .getByRole("button", { name: "Tạo yêu cầu chuyển khoản" })
      .click();
    await expect(request).toContainText(
      fallback === "static"
        ? "QR tĩnh — tự nhập số tiền và nội dung"
        : "Chuyển khoản thủ công — tự nhập đầy đủ thông tin",
    );
    await expect(request.getByRole("alert")).toContainText(
      "tự nhập đúng số tiền",
    );
    await expect(request).toContainText("Số tiền chuyển: 700.000");
    await expect(request).toContainText("Người nhận: NGUYEN VAN AN");
    await expect(request).toContainText("Nội dung: HP202610AN");
  }
  await expect(student).toContainText("Chưa ghi nhận tiền.");
  kind = "vietqr";
  brokenImage = true;
  await request
    .getByRole("button", { name: "Tạo yêu cầu chuyển khoản" })
    .click();
  await expect(request).toContainText("QR tĩnh — tự nhập số tiền và nội dung");
  await expect(
    request.getByRole("img", { name: "QR ngân hàng tĩnh" }),
  ).toBeVisible();
  await payments
    .getByRole("button", { name: "Tải lại công nợ", exact: true })
    .click();
  await expect(request.getByText("Nội dung: HP202610AN")).toHaveCount(0);
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth,
    ),
  ).toBeTruthy();
});
