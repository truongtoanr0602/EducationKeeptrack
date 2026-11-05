import { test, expect } from "@playwright/test";

test("teacher tracks two learners across dates and explicitly approves a plan", async ({
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
  async function post(path: string, data: unknown) {
    const csrf = await (await page.request.get("/api/auth/csrf")).json();
    const response = await page.request.post(`/api${path}`, {
      headers: { "X-CSRF-TOKEN": csrf.token },
      data,
    });
    expect(response.ok()).toBeTruthy();
    return response.json();
  }
  const cls = await post("/classes", {
    name: `Cá nhân hóa ${Date.now()}`,
    subject: "Toán",
  });
  for (const name of ["An demo", "Bình demo"]) {
    const student = await post("/students", { name });
    await post(`/classes/${cls.id}/enrollments`, {
      studentId: student.id,
      startDate: "2026-10-01",
    });
  }
  await page.goto(`/#${cls.id}`);
  await page.reload();
  const panel = page.getByRole("region", {
    name: "Cá nhân hóa học tập",
    exact: true,
  });
  await expect(panel).toBeVisible();
  await panel.getByLabel("Bộ sách của lớp", { exact: true }).fill("Sách demo");
  await panel.getByLabel("Tên kỹ năng 1", { exact: true }).fill("Cộng qua 10");
  await panel.getByLabel("Đã dạy kỹ năng 1", { exact: true }).check();
  await panel
    .getByRole("button", { name: "Lưu cấu hình kỹ năng", exact: true })
    .click();
  await expect(
    panel.getByText("Đã lưu cấu hình.", { exact: true }),
  ).toBeVisible();
  await panel.getByLabel("Ngày theo dõi", { exact: true }).fill("2026-10-05");
  const an = panel.getByRole("article", {
    name: "An demo — Cộng qua 10",
    exact: true,
  });
  const binh = panel.getByRole("article", {
    name: "Bình demo — Cộng qua 10",
    exact: true,
  });
  await an
    .getByRole("button", { name: "Đỏ — Cần hỗ trợ", exact: true })
    .click();
  await expect(
    an.getByText("Đỏ — Cần hỗ trợ", { exact: true }).first(),
  ).toBeVisible();
  await binh
    .getByRole("button", { name: "Xanh — Ổn trong buổi", exact: true })
    .click();
  await page.reload();
  await panel.getByLabel("Ngày theo dõi", { exact: true }).fill("2026-10-06");
  await expect(
    an.getByText("Cần kiểm tra lại ở buổi tham dự tiếp theo.", { exact: true }),
  ).toBeVisible();
  await expect(
    binh.getByText("Cần kiểm tra lại ở buổi tham dự tiếp theo.", {
      exact: true,
    }),
  ).toHaveCount(0);
  await expect(
    an.getByText("Chưa đủ bằng chứng", { exact: true }),
  ).toBeVisible();
  await an
    .getByText("Nhập kết quả bài kiểm tra chung hoặc riêng", { exact: true })
    .click();
  await an
    .getByLabel("Tên bài kiểm tra", { exact: true })
    .fill("Kiểm tra thử lại khi mất mạng");
  await an.getByLabel("Số đúng", { exact: true }).fill("5");
  await an
    .getByRole("checkbox", {
      name: "Xác nhận đây là kết quả độc lập để đánh giá kỹ năng",
      exact: true,
    })
    .check();
  const requestIds: string[] = [];
  let abortFirst = true;
  await page.route(
    `**/api/learning/classes/${cls.id}/evidence`,
    async (route) => {
      await expect(
        panel.getByLabel("Ngày theo dõi", { exact: true }),
      ).toBeDisabled();
      requestIds.push(route.request().postDataJSON().requestId);
      const response = await route.fetch();
      if (abortFirst) {
        abortFirst = false;
        await route.abort();
      } else await route.fulfill({ response });
    },
  );
  await an
    .getByRole("button", { name: "Lưu kết quả kỹ năng", exact: true })
    .click();
  await expect(panel.getByRole("alert")).toBeVisible();
  await an
    .getByRole("button", { name: "Lưu kết quả kỹ năng", exact: true })
    .click();
  await expect(
    panel.getByText("Đã lưu ghi nhận.", { exact: true }),
  ).toBeVisible();
  expect(requestIds).toHaveLength(2);
  expect(requestIds[0]).toBe(requestIds[1]);
  await expect(
    an.getByRole("button", { name: "Lưu kết quả kỹ năng", exact: true }),
  ).toBeDisabled();
  await expect(
    an.getByText("Chưa đủ bằng chứng", { exact: true }),
  ).toBeVisible();
  await panel.getByLabel("Tên kế hoạch", { exact: true }).fill("Ôn tập demo");
  await panel
    .getByLabel("Nội dung kế hoạch", { exact: true })
    .fill("Ôn chung, luyện riêng và kiểm tra lại.");
  await panel
    .getByRole("button", { name: "Lưu dự thảo kế hoạch", exact: true })
    .click();
  await expect(
    panel.getByText("Dự thảo", { exact: true }).first(),
  ).toBeVisible();
  await panel
    .getByRole("button", { name: "Duyệt kế hoạch này", exact: true })
    .first()
    .click();
  await expect(
    panel.getByText("Đã duyệt", { exact: true }).first(),
  ).toBeVisible();
});
