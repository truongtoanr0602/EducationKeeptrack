import { test, expect, type Page } from "@playwright/test";

// Other classes' schedules on the week grid, and the overlap warning (#59), against the real backend. The teacher's
// classes are shared by all tests, so each test names its own classes and looks only for them.

async function login(page: Page) {
  await page.goto("/", { waitUntil: "domcontentloaded" });
  await page.getByLabel("Email", { exact: true }).fill(process.env.DEMO_EMAIL!);
  await page
    .getByLabel("Mật khẩu", { exact: true })
    .fill(process.env.DEMO_PASSWORD!);
  await page.getByRole("button", { name: "Đăng nhập", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Hôm nay" })).toBeVisible();
}

async function write(page: Page, path: string, data: unknown, method = "POST") {
  const csrf = await (await page.request.get("/api/auth/csrf")).json();
  const response = await page.request.fetch(`/api${path}`, {
    method,
    data,
    headers: { "X-CSRF-TOKEN": csrf.token },
  });
  expect(response.ok(), await response.text()).toBeTruthy();
  return response.json();
}

const token = () =>
  Math.random()
    .toString(36)
    .replace(/[^a-z]/g, "")
    .padEnd(5, "x")
    .slice(0, 5);
const ymd = (offset: number) => {
  const d = new Date(Date.now() + 7 * 3600_000 + offset * 86_400_000);
  return d.toISOString().slice(0, 10);
};

// Class A teaches on Saturday 05:15-06:15, an hour nobody else uses; class B is the one being planned.
async function twoClasses(page: Page) {
  const a = `Lớp gốc ${token()}`;
  const b = `Lớp mới ${token()}`;
  const first = await write(page, "/classes", { name: a, subject: "Toán" });
  const second = await write(page, "/classes", { name: b, subject: "Toán" });
  await write(
    page,
    `/classes/${first.id}/schedule`,
    {
      version: 0,
      effectiveFrom: ymd(-1),
      slots: [{ dayOfWeek: 6, startTime: "05:15", endTime: "06:15" }],
    },
    "PUT",
  );
  return { a, b, bId: second.id as string };
}

async function openEditor(page: Page, classId: string) {
  await page.evaluate(
    (id) => (window.location.hash = `#/classes/${id}/sessions`),
    classId,
  );
  await page
    .getByRole("region", { name: "Lịch và điểm danh" })
    .getByRole("button", { name: "Thiết lập lịch tuần" })
    .click();
}

const slotList = (page: Page) =>
  page.getByRole("list", { name: "Khung giờ trong lịch tuần" });
async function enter(page: Page, day: string, start: string, end: string) {
  await page.getByRole("button", { name: day, exact: true }).click();
  await page.getByLabel("Giờ bắt đầu", { exact: true }).fill(start);
  await page.getByLabel("Giờ kết thúc", { exact: true }).fill(end);
}
const add = (page: Page) =>
  page.getByRole("button", { name: "Thêm vào lịch", exact: true }).click();

test.describe("overlap between classes", () => {
  test("planning class B shows class A grey on the grid and in the text, and warns about the overlap", async ({
    page,
  }) => {
    await login(page);
    const { a, b, bId } = await twoClasses(page);
    await openEditor(page, bId);

    // The other class: a grey block named after it, and the same facts as text.
    await expect(page.locator(".week-block.other", { hasText: a })).toHaveCount(
      1,
    );
    await expect(
      page.locator(".other-schedules li", { hasText: a }),
    ).toContainText("T7 05:15–06:15");

    // The slot being typed is checked before it is added.
    await enter(page, "Thứ Bảy", "05:45", "06:45");
    const clash = `Trùng với lớp ${a} (T7 05:15–06:15)`;
    await expect(
      page.getByRole("status").filter({ hasText: clash }),
    ).toBeVisible();
    await add(page);

    // Added: marked in the list (text, not only colour) and on the grid.
    const item = slotList(page).getByRole("listitem");
    await expect(item).toHaveCount(1);
    await expect(item).toContainText(clash);
    await expect(item).toHaveClass(/clash/);
    await expect(page.locator(".week-block.clash")).toHaveCount(1);

    // The warning never blocks: the overlapping schedule is saved, with the same result as any other save.
    await page
      .getByRole("button", { name: "Lưu lịch tuần", exact: true })
      .click();
    await expect(
      page.getByRole("button", { name: "Thiết lập lịch tuần" }),
    ).toBeVisible();
    const saved = await (
      await page.request.get(`/api/classes/${bId}/schedule`)
    ).json();
    expect(saved.version).toBe(1);
    expect(saved.slots).toHaveLength(1);
    expect(saved.slots[0]).toMatchObject({
      dayOfWeek: 6,
      startTime: "05:45:00",
    });
    expect(b).toBeTruthy();
  });

  test("slots that only touch, or fall on another day, are not warned about", async ({
    page,
  }) => {
    await login(page);
    const { a, bId } = await twoClasses(page);
    await openEditor(page, bId);
    await enter(page, "Thứ Bảy", "06:15", "07:00");
    await add(page);
    await enter(page, "Chủ nhật", "05:15", "06:15");
    await add(page);
    await expect(slotList(page).getByRole("listitem")).toHaveCount(2);
    // Class A is not named as a clash (classes left by earlier runs may legitimately overlap elsewhere).
    await expect(slotList(page)).not.toContainText(a);
    // Class A is still shown, only not as a clash.
    await expect(page.locator(".week-block.other", { hasText: a })).toHaveCount(
      1,
    );
  });

  test("a class never sees itself as another class", async ({ page }) => {
    await login(page);
    const { a, b, bId } = await twoClasses(page);
    // B gets a schedule of its own; opening B's editor again must show A grey but not B.
    await write(
      page,
      `/classes/${bId}/schedule`,
      {
        version: 0,
        effectiveFrom: ymd(-1),
        slots: [{ dayOfWeek: 6, startTime: "08:00", endTime: "09:00" }],
      },
      "PUT",
    );
    await openEditor(page, bId);
    await expect(slotList(page).getByRole("listitem")).toHaveCount(1);
    await expect(slotList(page)).not.toContainText(b);
    await expect(page.locator(".week-block.other", { hasText: a })).toHaveCount(
      1,
    );
    await expect(page.locator(".week-block.other", { hasText: b })).toHaveCount(
      0,
    );
  });

  test("the class creation dialog warns too, before the class exists", async ({
    page,
  }) => {
    await login(page);
    const { a } = await twoClasses(page);
    await page.evaluate(() => (window.location.hash = "#/classes"));
    await page
      .getByRole("button", { name: "Tạo lớp mới", exact: true })
      .click();
    const dialog = page.getByRole("dialog", { name: "Tạo lớp mới" });
    await dialog
      .getByLabel("Tên lớp", { exact: true })
      .fill(`Chưa tạo ${token()}`);
    await dialog.getByLabel("Môn học", { exact: true }).fill("Toán");
    await dialog.getByRole("button", { name: "Tiếp theo: lịch học" }).click();
    await expect(
      dialog.locator(".week-block.other", { hasText: a }),
    ).toHaveCount(1);
    await dialog.getByRole("button", { name: "Thứ Bảy", exact: true }).click();
    await dialog.getByLabel("Giờ bắt đầu", { exact: true }).fill("05:30");
    await dialog.getByLabel("Giờ kết thúc", { exact: true }).fill("06:00");
    await expect(
      dialog.getByRole("status").filter({
        hasText: `Trùng với lớp ${a} (T7 05:15–06:15)`,
      }),
    ).toBeVisible();
    await dialog
      .getByRole("button", { name: "Thêm vào lịch", exact: true })
      .click();
    await expect(dialog.locator(".slot-list li.clash")).toHaveCount(1);
    // Nothing was saved: closing leaves no class behind.
    await dialog.getByRole("button", { name: "Hủy" }).click();
    await expect(dialog).toHaveCount(0);
  });
});
