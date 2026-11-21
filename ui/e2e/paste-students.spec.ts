import { test, expect, type Page, type Route } from "@playwright/test";

// Pasting a list of students into a class (#55), against the real backend. The teacher's student profiles are shared
// by all classes, so every name carries a random token of letters and each test counts only its own names.

const token = () =>
  Math.random()
    .toString(36)
    .replace(/[^a-z]/g, "")
    .padEnd(6, "x")
    .slice(0, 6);
const letters = (i: number) =>
  String.fromCharCode(65 + (i % 26)) + String.fromCharCode(97 + ((i * 7) % 26));

async function login(page: Page) {
  await page.goto("/", { waitUntil: "domcontentloaded" });
  await page.getByLabel("Email", { exact: true }).fill(process.env.DEMO_EMAIL!);
  await page
    .getByLabel("Mật khẩu", { exact: true })
    .fill(process.env.DEMO_PASSWORD!);
  await page.getByRole("button", { name: "Đăng nhập", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Hôm nay" })).toBeVisible();
}

async function write(page: Page, path: string, data: unknown) {
  const csrf = await (await page.request.get("/api/auth/csrf")).json();
  const response = await page.request.fetch(`/api${path}`, {
    method: "POST",
    data,
    headers: { "X-CSRF-TOKEN": csrf.token },
  });
  expect(response.ok(), await response.text()).toBeTruthy();
  return response.json();
}

async function openPaste(page: Page) {
  const className = `Lớp dán ${Date.now()}-${token()}`;
  const made = await write(page, "/classes", {
    name: className,
    subject: "Toán",
  });
  await page.evaluate(
    (id) => (window.location.hash = `#/classes/${id}/students`),
    made.id,
  );
  await page
    .getByRole("button", { name: "Dán danh sách", exact: true })
    .click();
  const panel = page.getByRole("region", { name: "Dán danh sách học sinh" });
  await expect(panel).toBeVisible();
  return { id: made.id as string, panel };
}

const enrolled = async (page: Page, classId: string) =>
  (await (
    await page.request.get(`/api/classes/${classId}/enrollments`)
  ).json()) as {
    student: { name: string };
  }[];
const profiles = async (page: Page) =>
  (await (await page.request.get("/api/students")).json()) as {
    id: string;
    name: string;
  }[];
const countOf = (all: { name: string }[], tag: string) =>
  all.filter((s) => s.name.endsWith(tag)).length;
const saveButton = (panel: ReturnType<Page["getByRole"]>) =>
  panel.getByRole("button", { name: /^Thêm \d+ học sinh$/ });

test.describe("paste a list of students", () => {
  test("15 pasted lines, with list numbers and a phone, are all saved into the class", async ({
    page,
  }) => {
    await login(page);
    const tag = token();
    const names = Array.from({ length: 15 }, (_, i) => `${letters(i)} ${tag}`);
    const { id, panel } = await openPaste(page);
    // Numbering from a chat list, one line with a phone, one pasted the way a spreadsheet separates columns.
    const lines = names.map((n, i) =>
      i === 1
        ? `${i + 1}. ${n}, 0901234567`
        : i === 2
          ? `${n}\t0912 345 678`
          : `${i + 1}. ${n}`,
    );
    await panel
      .getByLabel("Danh sách học sinh", { exact: true })
      .fill(lines.join("\n"));
    await expect(panel.getByText("15 học sinh sẽ được thêm.")).toBeVisible();
    await expect(
      panel
        .getByRole("table", { name: "Xem trước danh sách học sinh" })
        .getByRole("row"),
    ).toHaveCount(16);

    await saveButton(panel).click();
    await expect(panel.getByText("Đã thêm 15 học sinh vào lớp.")).toBeVisible({
      timeout: 60_000,
    });
    const roster = await enrolled(page, id);
    expect(roster.map((r) => r.student.name).sort()).toEqual([...names].sort());
    // The roster behind the panel was refreshed (hidden from role queries while the panel is open).
    await expect(page.locator("h3", { hasText: names[0] })).toBeVisible();
    await panel.getByRole("button", { name: "Đóng" }).click();
    await expect(panel).toHaveCount(0);
  });

  test("a bad line is flagged and stays in the box, the others are saved", async ({
    page,
  }) => {
    await login(page);
    const tag = token();
    const { id, panel } = await openPaste(page);
    await panel
      .getByLabel("Danh sách học sinh", { exact: true })
      .fill(`An ${tag}\nBình ${tag}, abc\nChi ${tag}, 0901234567`);
    await expect(
      panel.getByText("2 học sinh sẽ được thêm, 1 dòng cần sửa."),
    ).toBeVisible();
    await expect(panel.getByText("Số điện thoại không hợp lệ.")).toBeVisible();

    await saveButton(panel).click();
    await expect(panel.getByText("Đã thêm 2 học sinh vào lớp.")).toBeVisible({
      timeout: 30_000,
    });
    await expect(
      panel.getByLabel("Danh sách học sinh", { exact: true }),
    ).toHaveValue(`Bình ${tag}, abc`);
    expect(
      (await enrolled(page, id)).map((r) => r.student.name).sort(),
    ).toEqual([`An ${tag}`, `Chi ${tag}`]);
    // Fixing the line and saving adds it.
    await panel
      .getByLabel("Danh sách học sinh", { exact: true })
      .fill(`Bình ${tag}, 0987654321`);
    await saveButton(panel).click();
    await expect(panel.getByText("Đã thêm 3 học sinh vào lớp.")).toBeVisible({
      timeout: 30_000,
    });
    expect(await enrolled(page, id)).toHaveLength(3);
  });

  test("the same name twice, or a name that already exists, is a warning that does not block", async ({
    page,
  }) => {
    await login(page);
    const tag = token();
    await write(page, "/students", { name: `Có sẵn ${tag}` });
    const { panel } = await openPaste(page);
    await panel
      .getByLabel("Danh sách học sinh", { exact: true })
      .fill(`Trùng ${tag}\nTrùng ${tag.toUpperCase()}\nCó sẵn ${tag}`);
    await expect(
      panel.getByText("Trùng tên với dòng 1 trong danh sách vừa dán."),
    ).toBeVisible();
    await expect(
      panel.getByText(
        "Đã có học sinh tên này trong danh sách của bạn (có thể đang học lớp khác).",
      ),
    ).toBeVisible();
    await expect(saveButton(panel)).toBeEnabled();
    await expect(saveButton(panel)).toHaveText("Thêm 3 học sinh");
  });

  test("an answer lost after a profile was created: the retry does not create it twice", async ({
    page,
  }) => {
    await login(page);
    const tag = token();
    const names = ["A", "B", "C"].map((l) => `${l}${l} ${tag}`);
    const { id, panel } = await openPaste(page);
    let creates = 0;
    await page.route("**/api/students", async (route: Route) => {
      if (route.request().method() !== "POST") return route.continue();
      creates++;
      if (creates === 2) {
        // The server really creates the profile, but the browser never hears back.
        await route.fetch();
        return route.abort("connectionreset");
      }
      return route.continue();
    });
    await panel
      .getByLabel("Danh sách học sinh", { exact: true })
      .fill(names.join("\n"));
    await saveButton(panel).click();
    await expect(panel.getByRole("alert")).toContainText("Mất kết nối");
    await expect(
      panel.getByText("chưa chắc đã lưu, sẽ kiểm tra khi thử lại"),
    ).toBeVisible();
    // The server already holds the profile the browser never heard about.
    expect(countOf(await profiles(page), tag)).toBe(2);

    await panel.getByRole("button", { name: "Thử lại", exact: true }).click();
    await expect(panel.getByText("Đã thêm 3 học sinh vào lớp.")).toBeVisible({
      timeout: 30_000,
    });
    const all = await profiles(page);
    for (const name of names)
      expect(all.filter((s) => s.name === name)).toHaveLength(1);
    expect(
      (await enrolled(page, id)).map((r) => r.student.name).sort(),
    ).toEqual([...names].sort());
  });

  test("a failed enrollment stops the run, and the retry adds no profile", async ({
    page,
  }) => {
    await login(page);
    const tag = token();
    const names = ["A", "B", "C"].map((l) => `${l}${l} ${tag}`);
    const { id, panel } = await openPaste(page);
    let failed = false;
    await page.route("**/api/classes/*/enrollments", async (route: Route) => {
      if (route.request().method() !== "POST" || failed)
        return route.continue();
      failed = true;
      return route.fulfill({
        status: 500,
        contentType: "application/json",
        body: "{}",
      });
    });
    await panel
      .getByLabel("Danh sách học sinh", { exact: true })
      .fill(names.join("\n"));
    await saveButton(panel).click();
    await expect(panel.getByRole("alert")).toBeVisible();
    // It stopped at the first student: only that profile exists, the other two were never touched.
    expect(countOf(await profiles(page), tag)).toBe(1);
    expect(await enrolled(page, id)).toHaveLength(0);

    await panel.getByRole("button", { name: "Thử lại", exact: true }).click();
    await expect(panel.getByText("Đã thêm 3 học sinh vào lớp.")).toBeVisible({
      timeout: 30_000,
    });
    const all = await profiles(page);
    for (const name of names)
      expect(all.filter((s) => s.name === name)).toHaveLength(1);
    expect(await enrolled(page, id)).toHaveLength(3);
  });

  test("a list longer than the limit cannot be saved", async ({ page }) => {
    await login(page);
    const { panel } = await openPaste(page);
    const lines = Array.from(
      { length: 101 },
      (_, i) => `Em ${letters(i)}${letters(i + 3)} ${i}`,
    );
    await panel
      .getByLabel("Danh sách học sinh", { exact: true })
      .fill(lines.join("\n"));
    await expect(
      panel.getByText("Tối đa 100 học sinh mỗi lần dán; bạn đã dán 101."),
    ).toBeVisible();
    await expect(saveButton(panel)).toBeDisabled();
  });
});
