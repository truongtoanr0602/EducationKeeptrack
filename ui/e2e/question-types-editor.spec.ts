import { test, expect, type Page } from "@playwright/test";

const png = Buffer.from(
  "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==",
  "base64",
);
// Only the first bytes of an mp3 are checked by the server (ID3 tag), so a small buffer will do.
const mp3 = Buffer.concat([
  Buffer.from("ID3\u0003\u0000\u0000", "latin1"),
  Buffer.alloc(4096, 7),
]);

async function signIn(page: Page) {
  await page.goto("/");
  await page.getByLabel("Email", { exact: true }).fill(process.env.DEMO_EMAIL!);
  await page
    .getByLabel("Mật khẩu", { exact: true })
    .fill(process.env.DEMO_PASSWORD!);
  await page.getByRole("button", { name: "Đăng nhập", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Hôm nay" })).toBeVisible();
}

/** A class with the sample curriculum, so it has skills to attach questions to. */
async function classWithSkills(page: Page) {
  const csrf = (await (await page.request.get("/api/auth/csrf")).json()).token;
  const created = await page.request.post("/api/classes", {
    data: { name: `Soạn câu ${Date.now()}`, subject: "Toán" },
    headers: { "X-CSRF-TOKEN": csrf },
  });
  const classroom = await created.json();
  const template = await page.request.get(
    `/api/learning/classes/${classroom.id}/import-template`,
  );
  const imported = await page.request.post(
    `/api/learning/classes/${classroom.id}/imports?commit=true`,
    {
      headers: { "X-CSRF-TOKEN": csrf },
      multipart: {
        file: {
          name: "mau.xlsx",
          mimeType:
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
          buffer: await template.body(),
        },
      },
    },
  );
  expect(imported.ok(), await imported.text()).toBeTruthy();
  await page.evaluate(
    (id) =>
      (window.location.hash = `#/classes/${id}/learning/program/questions`),
    classroom.id,
  );
  await expect(
    page.getByRole("region", { name: "Ngân hàng câu hỏi" }),
  ).toBeVisible();
  return classroom.id as string;
}

test("a multiple-choice question with two right options and 1,5 points is saved and listed", async ({
  page,
}) => {
  await signIn(page);
  await classWithSkills(page);
  await page.getByRole("button", { name: "Thêm câu hỏi", exact: true }).click();
  const form = page.getByRole("form", { name: "Thêm câu hỏi" });
  await form.getByLabel("Mã câu").fill("NHIEU-1");
  await form.getByLabel("Dạng").selectOption("multi_choice");
  await form.getByLabel(/^Điểm/).fill("1,5");
  await form.getByLabel("Đề bài").fill("Chọn các số chẵn");
  for (const [key, text] of [
    ["A", "2"],
    ["B", "3"],
    ["C", "4"],
    ["D", "5"],
    ["E", "6"],
  ])
    await form.getByLabel(key, { exact: true }).fill(text);
  await form.getByLabel("Phương án A đúng").check();
  await form.getByLabel("Phương án C đúng").check();
  await form.getByLabel("Phương án E đúng").check();
  await form.getByRole("button", { name: "Thêm vào ngân hàng" }).click();
  const row = page.getByRole("listitem").filter({ hasText: "NHIEU-1" });
  await expect(row).toContainText("Nhiều đáp án");
  await expect(row).toContainText("1,5 điểm");
  await expect(row).toContainText("Đáp án: A, C, E");
});

test("one right option is refused for a multiple-choice question and the message sits by the answer", async ({
  page,
}) => {
  await signIn(page);
  await classWithSkills(page);
  await page.getByRole("button", { name: "Thêm câu hỏi", exact: true }).click();
  const form = page.getByRole("form", { name: "Thêm câu hỏi" });
  await form.getByLabel("Mã câu").fill("NHIEU-2");
  await form.getByLabel("Dạng").selectOption("multi_choice");
  await form.getByLabel("Đề bài").fill("x");
  for (const [key, text] of [
    ["A", "1"],
    ["B", "2"],
    ["C", "3"],
  ])
    await form.getByLabel(key, { exact: true }).fill(text);
  await form.getByLabel("Phương án A đúng").check();
  await form.getByRole("button", { name: "Thêm vào ngân hàng" }).click();
  await expect(
    form.getByText("Đáp án là từ hai ký hiệu phương án trở lên"),
  ).toBeVisible();
});

test("an essay keeps its sample answer and points", async ({ page }) => {
  await signIn(page);
  await classWithSkills(page);
  await page.getByRole("button", { name: "Thêm câu hỏi", exact: true }).click();
  const form = page.getByRole("form", { name: "Thêm câu hỏi" });
  await form.getByLabel("Mã câu").fill("LUAN-1");
  await form.getByLabel("Dạng").selectOption("essay");
  await form.getByLabel(/^Điểm/).fill("3");
  await form.getByLabel("Đề bài").fill("Giải thích vì sao 4 là số chẵn");
  await form.getByLabel(/Đáp án mẫu/).fill("Vì 4 chia hết cho 2");
  await form.getByRole("button", { name: "Thêm vào ngân hàng" }).click();
  const row = page.getByRole("listitem").filter({ hasText: "LUAN-1" });
  await expect(row).toContainText("Tự luận");
  await expect(row).toContainText("3 điểm");
  await expect(row).toContainText("Vì 4 chia hết cho 2");
});

/** Images and audio are stored in the file store (S3); without it the upload answers 5xx and the test that needs it is skipped. */
async function fileStoreWorks(page: Page, classId: string) {
  const csrf = (await (await page.request.get("/api/auth/csrf")).json()).token;
  const probe = await page.request.post(
    `/api/learning/classes/${classId}/question-images`,
    {
      headers: { "X-CSRF-TOKEN": csrf },
      multipart: {
        file: { name: "probe.png", mimeType: "image/png", buffer: png },
      },
    },
  );
  return probe.status() < 500;
}

test("a typed fill-in is written in the rich editor with formatting, a blank and an answer, and comes back when edited", async ({
  page,
}) => {
  await signIn(page);
  await classWithSkills(page);
  await page.getByRole("button", { name: "Thêm câu hỏi", exact: true }).click();
  const form = page.getByRole("form", { name: "Thêm câu hỏi" });
  await form.getByLabel("Mã câu").fill("DIEN-1");
  await form.getByLabel("Dạng").selectOption("fill_blanks");
  await form.getByLabel("Học sinh gõ chữ vào chỗ trống").check();

  const editor = form.getByRole("textbox", { name: "Đề bài" });
  await expect(editor).toBeVisible();
  await editor.click();
  await page.keyboard.type("Thủ đô của Việt Nam là ");
  await form.getByRole("button", { name: "Chèn chỗ trống" }).click();
  await page.keyboard.type(" và đây là chữ đậm");
  await page.keyboard.press("Control+A");
  await form.getByRole("button", { name: "In đậm" }).click();
  await form.getByLabel("Font chữ").selectOption("ff-serif");
  await form.getByLabel("Cỡ chữ").selectOption("fs-l");
  await form.getByLabel("Màu chữ").selectOption("fc-3");
  await expect(editor.locator("strong")).toHaveCount(1);
  await expect(editor.locator("span.blank")).toHaveCount(1);
  await expect(form.getByText("hiện có 1 chỗ")).toBeVisible();
  await form.getByLabel(/^Chỗ trống 1/).fill("Hà Nội | Ha Noi");
  await form.getByLabel(/Bỏ qua dấu tiếng Việt/).check();
  await form.getByRole("button", { name: "Thêm vào ngân hàng" }).click();

  const row = page.getByRole("listitem").filter({ hasText: "DIEN-1" });
  await expect(row).toContainText("Điền ô");
  await expect(row.locator(".rich-stem strong")).toHaveCount(1);
  await expect(row.locator(".rich-stem .blank")).toHaveCount(1);
  await expect(
    row.locator(".rich-stem .ff-serif.fs-l.fc-3").first(),
  ).toBeVisible();
  await expect(row).toContainText("Hà Nội / Ha Noi");

  // Opened again for editing, the stem, the answer and the toggle come back.
  await row.getByRole("button", { name: "Sửa" }).click();
  const again = page.getByRole("form", { name: "Sửa câu hỏi" });
  await expect(again.getByRole("textbox", { name: "Đề bài" })).toContainText(
    "Thủ đô của Việt Nam là",
  );
  await expect(again.getByLabel(/^Chỗ trống 1/)).toHaveValue("Hà Nội | Ha Noi");
  await expect(again.getByLabel(/Bỏ qua dấu tiếng Việt/)).toBeChecked();
});

test("a typed fill-in takes an image from the editor and an audio file, both shown after saving", async ({
  page,
}) => {
  await signIn(page);
  const classId = await classWithSkills(page);
  test.skip(
    !(await fileStoreWorks(page, classId)),
    "Kho tệp (S3) chưa chạy: không tải được ảnh và âm thanh.",
  );
  await page.getByRole("button", { name: "Thêm câu hỏi", exact: true }).click();
  const form = page.getByRole("form", { name: "Thêm câu hỏi" });
  await form.getByLabel("Mã câu").fill("DIEN-2");
  await form.getByLabel("Dạng").selectOption("fill_blanks");
  await form.getByLabel("Học sinh gõ chữ vào chỗ trống").check();
  const editor = form.getByRole("textbox", { name: "Đề bài" });
  await editor.click();
  await page.keyboard.type("Đồng hồ chỉ ");
  await form.getByRole("button", { name: "Chèn chỗ trống" }).click();
  await form.getByRole("button", { name: "Chèn ảnh" }).click();
  await form
    .getByLabel("Tải ảnh mới")
    .setInputFiles({ name: "dongho.png", mimeType: "image/png", buffer: png });
  await expect(editor.locator("img[data-file='dongho.png']")).toBeVisible();
  await form.getByLabel(/^Chỗ trống 1/).fill("3 giờ");
  await form
    .getByLabel("Hoặc tải tệp mới")
    .setInputFiles({ name: "nghe.mp3", mimeType: "audio/mpeg", buffer: mp3 });
  await expect(form.getByLabel("Chọn tệp đã tải lên")).toHaveValue("nghe.mp3");
  await form.getByRole("button", { name: "Thêm vào ngân hàng" }).click();
  const row = page.getByRole("listitem").filter({ hasText: "DIEN-2" });
  await expect(row.locator(".rich-stem img")).toBeVisible();
  await expect(row.locator("audio")).toHaveCount(1);
});
