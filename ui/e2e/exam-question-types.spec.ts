import { test, expect, type Page } from "@playwright/test";

// The student's page for the kinds of ADR 0008: multiple choice (ticked, with the number to tick), typed blanks in a formatted
// text, an essay box, and the score with the essay still waiting. On a phone, as a child would use it.

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

test("a child answers multiple choice, typed blanks, an essay and a single choice, and sees the score without the essay", async ({
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
    name: `Đề đủ dạng ${Date.now()}`,
    subject: "Toán",
  });
  const student = await write(page, "/students", {
    name: `Phạm Bé ${Math.random().toString(36).slice(2, 7)}`,
  });
  await write(page, `/classes/${classroom.id}/enrollments`, {
    studentId: student.id,
    startDate: "2026-01-01",
  });
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
  };
  const option = (key: string, text: string) => ({ key, text, image: null });
  const multi = await write(page, `${learning}/questions`, {
    ...base,
    code: "DD1",
    type: "multi_choice",
    stem: "Chọn các số chẵn",
    points: 1,
    options: [
      option("A", "2"),
      option("B", "3"),
      option("C", "4"),
      option("D", "5"),
    ],
    answer: [["A", "C"]],
  });
  const typed = await write(page, `${learning}/questions`, {
    ...base,
    code: "DD2",
    type: "fill_blanks",
    fillMode: "type",
    points: 2,
    options: [],
    answer: [["Việt Nam"], ["Hà Nội", "Ha Noi"]],
    stem: "x",
    stemHtml:
      '<p class="ff-serif">Thủ đô của <strong>Việt Nam</strong> là <span class="blank" data-blank="1">{{1}}</span>, thủ đô là <span class="blank" data-blank="2">{{2}}</span>.</p>',
  });
  const essay = await write(page, `${learning}/questions`, {
    ...base,
    code: "DD3",
    type: "essay",
    stem: "Giải thích vì sao 4 là số chẵn",
    points: 3,
    options: [],
    answer: [],
    sampleAnswer: "Vì 4 chia hết cho 2",
  });
  const single = await write(page, `${learning}/questions`, {
    ...base,
    code: "DD4",
    type: "single_choice",
    stem: "1 + 1 = ?",
    points: 2,
    options: [option("A", "1"), option("B", "2"), option("C", "3")],
    answer: [["B"]],
  });
  let exam = await write(page, `${learning}/exams`, {
    title: "Đề đủ dạng",
    counts: [4, 0, 0, 0],
    skillIds: [skill],
    durationMinutes: 30,
    questionIds: [multi.id, typed.id, essay.id, single.id],
  });
  exam = await write(page, `${learning}/exams/${exam.id}/open`, {
    version: exam.version,
  });
  const code = exam.seats.find(
    (x: { studentId: string }) => x.studentId === student.id,
  ).code as string;

  const phone = await browser.newContext({
    viewport: { width: 390, height: 844 },
  });
  const kid = await phone.newPage();
  await kid.goto(`/#/lam-bai/${exam.publicToken}`);
  await kid
    .getByRole("button", {
      name: new RegExp(`^${student.name.split(" ").at(-1)}`),
    })
    .click();
  await kid.getByLabel("Mã vào bài", { exact: true }).fill(code);
  await kid.getByRole("button", { name: "Vào làm bài" }).click();

  let sawMulti = false;
  for (let i = 0; i < 4; i++) {
    await expect(kid.locator(".take-number")).toContainText(`Câu ${i + 1} `);
    if (await kid.getByRole("checkbox").count()) {
      sawMulti = true;
      await expect(kid.getByText("Chọn 2 đáp án.")).toBeVisible();
      await kid.getByRole("checkbox", { name: "2", exact: true }).click();
      await kid.getByRole("checkbox", { name: "4", exact: true }).click();
      await expect(
        kid.getByRole("checkbox", { name: "2", exact: true }),
      ).toHaveAttribute("aria-checked", "true");
    } else if (await kid.getByLabel("Chỗ trống 1").count()) {
      await expect(kid.locator(".take-stem strong")).toHaveText("Việt Nam");
      await kid.getByLabel("Chỗ trống 1").fill("việt nam");
      await kid.getByLabel("Chỗ trống 2").fill("ha noi");
    } else if (await kid.getByLabel("Bài làm của em").count()) {
      await kid
        .getByLabel("Bài làm của em")
        .fill("Vì 4 chia hết cho 2 nên 4 là số chẵn.");
    } else {
      await kid.getByRole("radio", { name: "2", exact: true }).click();
    }
    await expect(kid.getByText("Đã lưu ✓")).toBeVisible();
    if (i < 3) await kid.getByRole("button", { name: "Câu sau →" }).click();
  }
  expect(sawMulti).toBe(true);
  await kid.getByRole("button", { name: "Nộp bài" }).click();
  await kid
    .getByRole("alertdialog")
    .getByRole("button", { name: "Nộp bài" })
    .click();
  await expect(
    kid.getByRole("heading", { name: /Em đã nộp bài/ }),
  ).toBeVisible();
  await expect(kid.getByText("Đáp án")).toHaveCount(0);

  // The teacher closes the exam and releases the results; the child sees the points and that the essay is still waiting.
  exam = await write(page, `${learning}/exams/${exam.id}/close`, {
    version: (
      await (await page.request.get(`/api${learning}/exams/${exam.id}`)).json()
    ).version,
  });
  await write(
    page,
    `${learning}/exams/${exam.id}/show-results`,
    { show: true, version: exam.version },
    "PUT",
  );
  // Opened again, the page still knows the child (same device) and goes straight to the result.
  await kid.reload();
  await expect(
    kid.getByText(/Em được 5 \/ 8 điểm \(thang 10: 6,25\)/),
  ).toBeVisible();
  await expect(kid.getByText("Chưa gồm phần tự luận")).toBeVisible();
  await expect(kid.getByText("Chờ thầy cô chấm")).toBeVisible();
  const overflow = await kid.evaluate(
    () =>
      document.documentElement.scrollWidth -
      document.documentElement.clientWidth,
  );
  expect(overflow).toBeLessThanOrEqual(0);
  await phone.close();
});
