import { test, expect } from "@playwright/test";

test("teacher independently grades both levels, confirms and reloads history", async ({
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
  const classroom = await post("/classes", {
    name: `Chấm bài ${Date.now()}`,
    subject: "Toán",
  });
  const student = await post("/students", { name: "An chấm bài" });
  await post(`/classes/${classroom.id}/enrollments`, {
    studentId: student.id,
    startDate: "2026-10-01",
  });
  await page.goto(`/#${classroom.id}`);
  // The class was created via the API; refresh the app's class list.
  await page.reload();
  const learning = page.getByRole("region", { name: "Đánh giá học tập" });
  await learning
    .getByLabel("Tên bài đánh giá", { exact: true })
    .fill("Phân số");
  await learning
    .getByLabel("Ngày đánh giá", { exact: true })
    .fill("2026-10-05");
  await expect(learning.getByLabel("Cách chấm mặc định")).toHaveValue("manual");
  await expect(
    learning.getByText("AI chưa khả dụng", { exact: true }).first(),
  ).toBeVisible();
  await learning
    .getByRole("button", { name: "Tạo bài đánh giá", exact: true })
    .click();
  const submission = learning.getByRole("article", {
    name: "Bài của An chấm bài",
  });
  await submission.getByLabel("Trạng thái đánh giá").selectOption("assessed");
  const completion = submission.getByRole("group", {
    name: "Lượng bài hoàn thành",
    exact: true,
  });
  const correctness = submission.getByRole("group", {
    name: "Kết quả làm đúng",
    exact: true,
  });
  await expect(completion.getByRole("button")).toHaveCount(4);
  await expect(correctness.getByRole("button")).toHaveCount(4);
  for (const name of ["25%", "50%", "75%", "100%"]) {
    await expect(
      completion.getByRole("button", { name, exact: true }),
    ).toBeVisible();
    await expect(
      correctness.getByRole("button", { name, exact: true }),
    ).toBeVisible();
  }
  await expect(
    submission.getByRole("button", { name: "Xác nhận kết quả" }),
  ).toBeDisabled();
  await completion.getByRole("button", { name: "25%", exact: true }).click();
  await correctness.getByRole("button", { name: "100%", exact: true }).click();
  await expect(
    completion.getByRole("button", { name: "25%", exact: true }),
  ).toHaveAttribute("aria-pressed", "true");
  await expect(
    correctness.getByRole("button", { name: "100%", exact: true }),
  ).toHaveAttribute("aria-pressed", "true");
  await submission.getByRole("button", { name: "Lưu bản nháp" }).click();
  await expect(
    submission.getByText("Đã lưu bản nháp.", { exact: true }),
  ).toBeVisible();
  await expect(
    submission.getByRole("status", { name: "Kết quả cuối" }),
  ).toContainText("Chưa xác nhận");
  await submission.getByRole("button", { name: "Xác nhận kết quả" }).click();
  await expect(
    submission.getByRole("status", { name: "Kết quả cuối" }),
  ).toContainText("Hoàn thành 25% · Làm đúng 100%");
  await page.reload();
  await expect(
    submission.getByRole("status", { name: "Kết quả cuối" }),
  ).toContainText("Hoàn thành 25% · Làm đúng 100%");
  await submission.getByLabel("Trạng thái đánh giá").selectOption("not_done");
  await submission.getByRole("button", { name: "Xác nhận kết quả" }).click();
  await expect(
    submission.getByRole("status", { name: "Kết quả cuối" }),
  ).toContainText("Hoàn thành 0% · Làm đúng 0%");
  await submission.getByText("Lịch sử chỉnh sửa").click();
  await expect(
    submission
      .getByRole("list", { name: "Lịch sử kết quả" })
      .getByRole("listitem"),
  ).toHaveCount(3);
  await submission.getByLabel("Trạng thái đánh giá").selectOption("no_correct");
  await completion.getByRole("button", { name: "75%", exact: true }).click();
  await submission.getByRole("button", { name: "Xác nhận kết quả" }).click();
  await expect(
    submission.getByRole("status", { name: "Kết quả cuối" }),
  ).toContainText("Hoàn thành 75% · Làm đúng 0%");
  await submission.getByLabel("Trạng thái đánh giá").selectOption("unassigned");
  await submission.getByRole("button", { name: "Xác nhận kết quả" }).click();
  await expect(
    submission.getByRole("status", { name: "Kết quả cuối" }),
  ).toContainText("Không giao bài · Không tính vào thống kê");
  await expect(page.locator("body")).toHaveJSProperty(
    "scrollWidth",
    await page.locator("body").evaluate((el) => el.clientWidth),
  );
});
