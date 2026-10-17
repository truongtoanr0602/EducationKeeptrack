import { test, expect } from "@playwright/test";

test("teacher schedules sessions and sees saved present/absent rows with missing-attendance reminders", async ({
  page,
}) => {
  await page.goto("/", { waitUntil: "domcontentloaded" });
  await page.getByLabel("Email", { exact: true }).fill(process.env.DEMO_EMAIL!);
  await page
    .getByLabel("Mật khẩu", { exact: true })
    .fill(process.env.DEMO_PASSWORD!);
  await page.getByRole("button", { name: "Đăng nhập", exact: true }).click();
  await page.getByRole("button", { name: "Tạo lớp mới", exact: true }).click();
  const name = `Lớp điểm danh ${Date.now()}`;
  await page.getByLabel("Tên lớp", { exact: true }).fill(name);
  await page.getByLabel("Môn học", { exact: true }).fill("Toán");
  await page.getByRole("button", { name: "Lưu lớp", exact: true }).click();
  await page
    .getByRole("article")
    .filter({ has: page.getByRole("heading", { name, exact: true }) })
    .getByRole("button", { name: "Danh sách học sinh" })
    .click();
  for (const student of ["An điểm danh", "Bình điểm danh"]) {
    await page
      .getByRole("button", { name: "Thêm học sinh mới", exact: true })
      .click();
    await page.getByLabel("Tên học sinh", { exact: true }).fill(student);
    await page.getByLabel("Ngày bắt đầu", { exact: true }).fill("2026-10-01");
    await page
      .getByRole("button", { name: "Lưu học sinh", exact: true })
      .click();
    await expect(
      page.getByRole("heading", { name: student, exact: true }),
    ).toBeVisible();
  }
  const workspace = page.getByRole("region", { name: "Lịch và điểm danh" });
  await workspace.getByRole("button", { name: "Thiết lập lịch tuần" }).click();
  await page.getByLabel("Lịch áp dụng từ ngày").fill("2026-10-01");
  await page.getByLabel("Ngày trong tuần 1").selectOption("1");
  await page.getByLabel("Giờ bắt đầu 1").fill("18:00");
  await page.getByLabel("Giờ kết thúc 1").fill("19:30");
  await page.getByRole("button", { name: "Thêm khung giờ" }).click();
  await page.getByLabel("Ngày trong tuần 2").selectOption("3");
  await page
    .getByRole("button", { name: "Lưu lịch tuần", exact: true })
    .click();
  await expect(
    workspace.getByText("Thứ Hai · 18:00–19:30", { exact: true }),
  ).toBeVisible();
  await page.getByLabel("Xem từ ngày").fill("2026-10-05");
  await page.getByLabel("Xem đến ngày").fill("2026-10-11");
  await workspace.getByRole("button", { name: "Tạo buổi theo lịch" }).click();
  await workspace
    .getByRole("button", {
      name: "2026-10-05 · 18:00–19:30 · Dự kiến",
      exact: true,
    })
    .click();
  const table = workspace.getByRole("table", { name: "Điểm danh học sinh" });
  const an = table.getByRole("row").filter({ hasText: "An điểm danh" });
  const binh = table.getByRole("row").filter({ hasText: "Bình điểm danh" });
  await expect(an).toContainText("Chưa điểm danh");
  await expect(
    an.getByRole("button", { name: "Lưu điểm danh" }),
  ).toBeDisabled();
  await page.getByLabel("Trạng thái buổi").selectOption("held");
  await page.getByRole("button", { name: "Lưu trạng thái buổi" }).click();
  await expect(
    workspace.getByText("Còn 2 lượt chưa điểm danh trước tổng kết.", {
      exact: true,
    }),
  ).toBeVisible();
  await an
    .getByLabel("Điểm danh An điểm danh", { exact: true })
    .selectOption("present");
  await an.getByRole("button", { name: "Lưu điểm danh" }).click();
  await expect(an.getByRole("strong")).toHaveText("Có mặt");
  await binh
    .getByLabel("Điểm danh Bình điểm danh", { exact: true })
    .selectOption("absent");
  await binh.getByLabel("Lý do vắng Bình điểm danh").fill("Bị ốm");
  await binh.getByLabel("Nghỉ có phép Bình điểm danh").check();
  await binh.getByRole("button", { name: "Lưu điểm danh" }).click();
  await expect(
    binh.getByText("Vắng · Nghỉ có phép", { exact: true }),
  ).toBeVisible();
  await expect(
    workspace.getByText("Không còn điểm danh thiếu ở các buổi đã diễn ra.", {
      exact: true,
    }),
  ).toBeVisible();
  const presentColor = await an.evaluate(
    (el) => getComputedStyle(el).backgroundColor,
  );
  const absentColor = await binh.evaluate(
    (el) => getComputedStyle(el).backgroundColor,
  );
  expect(presentColor).not.toBe(absentColor);
  expect(absentColor).toBe("rgb(236, 238, 242)");
  for (const cell of await binh.getByRole("cell").all()) {
    expect(
      await cell.evaluate((el) => getComputedStyle(el).backgroundColor),
    ).toBe("rgba(0, 0, 0, 0)");
  }
  await page.reload({ waitUntil: "domcontentloaded" });
  await workspace
    .getByRole("button", {
      name: "2026-10-05 · 18:00–19:30 · Đã diễn ra",
      exact: true,
    })
    .click();
  await expect(
    binh.getByText("Vắng · Nghỉ có phép", { exact: true }),
  ).toBeVisible();
  await expect(binh.getByLabel("Lý do vắng Bình điểm danh")).toHaveValue(
    "Bị ốm",
  );
  await an
    .getByLabel("Điểm danh An điểm danh", { exact: true })
    .selectOption("absent");
  await an.getByLabel("Lý do vắng An điểm danh").fill("Nội dung đang sửa");
  await page.route("**/api/classes/*/sessions/*/attendance/*", (route) =>
    route.request().method() === "PUT"
      ? route.fulfill({
          status: 503,
          contentType: "application/json",
          body: JSON.stringify({
            message: "Không thể lưu điểm danh. Vui lòng thử lại.",
          }),
        })
      : route.continue(),
  );
  await an.getByRole("button", { name: "Lưu điểm danh" }).click();
  await expect(an.getByRole("alert")).toContainText("Vui lòng thử lại");
  await expect(an.getByLabel("Lý do vắng An điểm danh")).toHaveValue(
    "Nội dung đang sửa",
  );
  await page.unroute("**/api/classes/*/sessions/*/attendance/*");
  const classId = new URL(page.url()).hash.slice(1);
  const storedSessions = await (
    await page.request.get(
      `/api/classes/${classId}/sessions?from=2026-10-01&to=2026-10-31`,
    )
  ).json();
  const sessionId = storedSessions.find(
    (s: { date: string }) => s.date === "2026-10-05",
  ).id;
  const marks = await (
    await page.request.get(
      `/api/classes/${classId}/sessions/${sessionId}/attendance`,
    )
  ).json();
  const anMark = marks.find((m: { name: string }) => m.name === "An điểm danh");
  const csrf = await (await page.request.get("/api/auth/csrf")).json();
  expect(
    (
      await page.request.put(
        `/api/classes/${classId}/sessions/${sessionId}/attendance/${anMark.studentId}`,
        {
          headers: { "X-CSRF-TOKEN": csrf.token },
          data: { status: "unmarked", version: anMark.version },
        },
      )
    ).ok(),
  ).toBeTruthy();
  await an.getByRole("button", { name: "Lưu điểm danh" }).click();
  await expect(an.getByRole("alert")).toContainText("tab khác");
  await workspace.getByRole("button", { name: "Tải lại điểm danh" }).click();
  await expect(an.getByRole("strong")).toHaveText("Chưa điểm danh");
  await expect(
    an.getByLabel("Điểm danh An điểm danh", { exact: true }),
  ).toHaveValue("absent");
  await expect(an.getByLabel("Lý do vắng An điểm danh")).toHaveValue(
    "Nội dung đang sửa",
  );
  await an.getByRole("button", { name: "Lưu điểm danh" }).click();
  await expect(an.getByRole("strong")).toHaveText("Vắng");
  await expect(an.getByRole("alert")).toHaveCount(0);
  // A denied write refreshes capability flags while retaining the open row draft.
  await an.getByLabel("Lý do vắng An điểm danh").fill("Chưa lưu khi hết hạn");
  await page.route("**/api/classes/*/sessions/*/attendance/*", (route) =>
    route.fulfill({
      status: 403,
      contentType: "application/json",
      body: JSON.stringify({ message: "Quyền sử dụng đã hết hạn." }),
    }),
  );
  await page.route("**/api/auth/me", (route) =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        email: process.env.DEMO_EMAIL,
        access: {
          canRead: true,
          canWrite: false,
          canUseAi: false,
          status: "expired",
        },
      }),
    }),
  );
  await an.getByRole("button", { name: "Lưu điểm danh" }).click();
  await expect(an.getByRole("alert")).toContainText("hết hạn");
  await expect(an.getByLabel("Lý do vắng An điểm danh")).toHaveValue(
    "Chưa lưu khi hết hạn",
  );
  await expect(
    an.getByRole("button", { name: "Lưu điểm danh" }),
  ).toBeDisabled();
  await page.unroute("**/api/classes/*/sessions/*/attendance/*");
  await page.unroute("**/api/auth/me");
  await page.reload({ waitUntil: "domcontentloaded" });
  await workspace
    .getByRole("button", {
      name: "2026-10-05 · 18:00–19:30 · Đã diễn ra",
      exact: true,
    })
    .click();
  await page.getByLabel("Trạng thái buổi").selectOption("cancelled");
  await page.getByLabel("Lý do thay đổi / hủy buổi").fill("Giáo viên bận");
  await page.getByRole("button", { name: "Lưu trạng thái buổi" }).click();
  await expect(
    an.getByRole("button", { name: "Lưu điểm danh" }),
  ).toBeDisabled();
  await workspace
    .getByRole("button", { name: "Tạo học bù cho buổi này" })
    .click();
  await page.getByLabel("Ngày buổi học").fill("2026-10-06");
  await page.getByRole("button", { name: "Lưu buổi học", exact: true }).click();
  await expect(
    workspace.getByRole("button", {
      name: "2026-10-06 · 18:00–19:30 · Dự kiến",
      exact: true,
    }),
  ).toBeVisible();
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth,
    ),
  ).toBeTruthy();
});
