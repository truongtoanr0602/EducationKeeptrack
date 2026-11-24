import { test, expect, type Page } from "@playwright/test";

// The teacher scores the essays of an exam question by question, the totals follow, and the results can be confirmed only once
// nothing is waiting. Two children answer through the API so the test stays on the teacher's screen.

async function write(
  page: Page,
  path: string,
  data?: unknown,
  method = "POST",
) {
  const csrf = await (await page.request.get("/api/auth/csrf")).json();
  const response = await page.request.fetch(`/api${path}`, {
    method,
    data,
    headers: { "X-CSRF-TOKEN": csrf.token },
  });
  expect(response.ok(), await response.text()).toBeTruthy();
  return response.json();
}

test("the teacher scores two essays, the totals follow, and confirming waits until all are scored", async ({
  page,
  browser,
}) => {
  test.setTimeout(180_000);
  await page.goto("/");
  await page.getByLabel("Email", { exact: true }).fill(process.env.DEMO_EMAIL!);
  await page
    .getByLabel("Mật khẩu", { exact: true })
    .fill(process.env.DEMO_PASSWORD!);
  await page.getByRole("button", { name: "Đăng nhập", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Hôm nay" })).toBeVisible();

  const classroom = await write(page, "/classes", {
    name: `Chấm tay ${Date.now()}`,
    subject: "Toán",
  });
  const suffix = Math.random()
    .toString(36)
    .replace(/[^a-z]/g, "")
    .padEnd(5, "x")
    .slice(0, 5);
  const kids = [`Lê Bé A${suffix}`, `Vũ Bé B${suffix}`];
  const students = [];
  for (const name of kids) {
    const student = await write(page, "/students", { name });
    await write(page, `/classes/${classroom.id}/enrollments`, {
      studentId: student.id,
      startDate: "2026-01-01",
    });
    students.push(student);
  }
  const template = await page.request.get(
    `/api/learning/classes/${classroom.id}/import-template`,
  );
  const csrf = (await (await page.request.get("/api/auth/csrf")).json()).token;
  const imported = await page.request.post(
    `/api/learning/classes/${classroom.id}/imports?commit=true`,
    {
      headers: { "X-CSRF-TOKEN": csrf },
      multipart: {
        file: {
          name: "mau.xlsx",
          mimeType: "application/octet-stream",
          buffer: await template.body(),
        },
      },
    },
  );
  expect(imported.ok(), await imported.text()).toBeTruthy();
  const learning = `/learning/classes/${classroom.id}`;
  const skill = (
    await (await page.request.get(`/api${learning}/questions`)).json()
  ).skills[0].id as string;
  const base = {
    level: "nhan_biet",
    primarySkillId: skill,
    secondarySkillIds: [],
    version: 0,
    options: [] as unknown[],
  };
  const essay = await write(page, `${learning}/questions`, {
    ...base,
    code: "CT1",
    type: "essay",
    stem: "Giải thích vì sao 4 là số chẵn",
    points: 4,
    answer: [],
    sampleAnswer: "Vì 4 chia hết cho 2",
  });
  const single = await write(page, `${learning}/questions`, {
    ...base,
    code: "CT2",
    type: "single_choice",
    stem: "1 + 1 = ?",
    points: 1,
    options: [
      { key: "A", text: "1", image: null },
      { key: "B", text: "2", image: null },
    ],
    answer: [["B"]],
  });
  let exam = await write(page, `${learning}/exams`, {
    title: "Đề chấm tay",
    counts: [2, 0, 0, 0],
    skillIds: [skill],
    durationMinutes: 30,
    questionIds: [essay.id, single.id],
  });
  exam = await write(page, `${learning}/exams/${exam.id}/open`, {
    version: exam.version,
  });
  const order = Object.fromEntries(
    exam.items.map((x: { questionId: string; order: number }) => [
      x.questionId,
      x.order,
    ]),
  );

  // Both children answer and submit through the API, on their own devices.
  const answers = ["Vì 4 chia hết cho 2.", "Vì nó chẵn."];
  for (const [i, student] of students.entries()) {
    const device = await browser.newContext();
    const code = exam.seats.find(
      (x: { studentId: string }) => x.studentId === student.id,
    ).code;
    const token = (await (await device.request.get("/api/auth/csrf")).json())
      .token;
    const headers = { "X-CSRF-TOKEN": token };
    const join = await device.request.post(
      `/api/exam-take/${exam.publicToken}/join`,
      { headers, data: { studentId: student.id, code } },
    );
    expect(join.ok(), await join.text()).toBeTruthy();
    for (const [question, values] of [
      [essay.id, [answers[i]]],
      [single.id, ["B"]],
    ] as const) {
      const saved = await device.request.put(
        `/api/exam-take/${exam.publicToken}/attempt/answers/${order[question]}`,
        { headers, data: { values } },
      );
      expect(saved.ok(), await saved.text()).toBeTruthy();
    }
    expect(
      (
        await device.request.post(
          `/api/exam-take/${exam.publicToken}/attempt/submit`,
          { headers, data: {} },
        )
      ).ok(),
    ).toBeTruthy();
    await device.close();
  }
  const current = await (
    await page.request.get(`/api${learning}/exams/${exam.id}`)
  ).json();
  await write(page, `${learning}/exams/${exam.id}/close`, {
    version: current.version,
  });

  await page.goto(
    `/?exam=${exam.id}#/classes/${classroom.id}/learning/program/exams`,
  );
  const grading = page.getByRole("region", { name: "Chấm tự luận" });
  await expect(grading).toContainText("còn 2 bài chờ chấm");
  const confirm = page.getByRole("button", { name: "Xác nhận kết quả" });
  await expect(confirm).toBeDisabled();
  await expect(
    page.getByText("Chấm xong 2 bài tự luận rồi mới xác nhận được."),
  ).toBeVisible();

  // By question: the first answer, then on to the next one that still has no points.
  await expect(grading.getByText("Bài 1/2")).toBeVisible();
  await expect(grading.getByRole("form")).toContainText("Vì 4 chia hết cho 2.");
  await grading.getByLabel(/^Điểm/).selectOption("4");
  await grading.getByLabel(/^Nhận xét/).fill("Đúng và đủ ý");
  await grading.getByRole("button", { name: "Lưu và sang bài kế" }).click();
  await expect(grading.getByText("Bài 2/2")).toBeVisible();
  await expect(grading.getByRole("form")).toContainText("Vì nó chẵn.");
  await expect(grading).toContainText("còn 1 bài chờ chấm");
  await expect(confirm).toBeDisabled();
  await grading.getByRole("button", { name: "½" }).click();
  await expect(grading.getByLabel(/^Điểm/)).toHaveValue("2");
  await grading.getByRole("button", { name: "Lưu", exact: true }).click();
  await expect(grading.getByText("Đã lưu ✓")).toBeVisible();

  // Nothing waits any more: the board shows the totals and the exam can be confirmed.
  await expect(grading).not.toContainText("chờ chấm)");
  const board = page.getByRole("region", { name: "Học sinh và kết quả" });
  await expect(
    board.getByRole("row").filter({ hasText: kids[0] }),
  ).toContainText("5/5");
  await expect(
    board.getByRole("row").filter({ hasText: kids[1] }),
  ).toContainText("3/5");
  await expect(confirm).toBeEnabled();
  page.once("dialog", (dialog) => void dialog.accept());
  await confirm.click();
  await expect(
    page.getByText("Kết quả đã vào bài đánh giá và hồ sơ năng lực."),
  ).toBeVisible();
  await expect(page.getByRole("region", { name: "Chấm tự luận" })).toHaveCount(
    0,
  );
});
