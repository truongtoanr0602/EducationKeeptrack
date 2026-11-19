import { test, expect, type Locator, type Page } from "@playwright/test";

// First-run setup (#57), against the real backend. The demo teacher already has classes, students and a receiving
// account, so a brand-new teacher is simulated by hiding them from three reads: the class list, the student
// profiles and the receiving account show only what this test has made itself. Every write is real.

const token = () =>
  Math.random()
    .toString(36)
    .replace(/[^a-z]/g, "")
    .padEnd(6, "x")
    .slice(0, 6);
const DAYS = [
  "Chủ nhật",
  "Thứ Hai",
  "Thứ Ba",
  "Thứ Tư",
  "Thứ Năm",
  "Thứ Sáu",
  "Thứ Bảy",
];
const todayWeekday = () =>
  DAYS[
    [
      "Sunday",
      "Monday",
      "Tuesday",
      "Wednesday",
      "Thursday",
      "Friday",
      "Saturday",
    ].indexOf(
      new Intl.DateTimeFormat("en-US", {
        timeZone: "Asia/Ho_Chi_Minh",
        weekday: "long",
      }).format(new Date()),
    )
  ];

type Fresh = {
  name: string;
  classMade: boolean;
  recipientSaved: boolean;
  /** The receiving account was added on another device. */
  otherDevice: boolean;
  profiles: Set<string>;
  failSchedule: boolean;
};

async function freshTeacher(page: Page): Promise<Fresh> {
  const s: Fresh = {
    name: `Lớp thiết lập ${token()}`,
    classMade: false,
    recipientSaved: false,
    otherDevice: false,
    profiles: new Set(),
    failSchedule: false,
  };
  await page.route("**/api/classes", async (route) => {
    if (route.request().method() === "POST") {
      const response = await route.fetch();
      if (response.ok()) s.classMade = true;
      return route.fulfill({ response });
    }
    if (!s.classMade) return route.fulfill({ json: [] });
    const response = await route.fetch();
    const all = (await response.json()) as { name: string }[];
    return route.fulfill({
      response,
      json: all.filter((c) => c.name === s.name),
    });
  });
  await page.route("**/api/students", async (route) => {
    if (route.request().method() === "POST") {
      const response = await route.fetch();
      if (response.ok())
        s.profiles.add(((await response.json()) as { id: string }).id);
      return route.fulfill({ response });
    }
    const response = await route.fetch();
    const all = (await response.json()) as { id: string }[];
    return route.fulfill({
      response,
      json: all.filter((p) => s.profiles.has(p.id)),
    });
  });
  await page.route("**/api/billing/recipient", async (route) => {
    if (route.request().method() === "POST") {
      // The demo teacher really has an account already (version n), which a new teacher would not.
      const real = await page.request.get("/api/billing/recipient");
      const body = route.request().postDataJSON();
      const response = await route.fetch({
        postData: real.ok()
          ? {
              ...body,
              version: ((await real.json()) as { version: number }).version,
            }
          : body,
      });
      if (response.ok()) s.recipientSaved = true;
      return route.fulfill({ response });
    }
    if (s.recipientSaved) return route.continue();
    if (s.otherDevice)
      return route.fulfill({
        json: {
          bankBin: "970415",
          bankName: "VietinBank",
          accountNo: "0123456789",
          accountName: "NGUYEN VAN A",
          hasStaticQr: false,
          version: 1,
        },
      });
    return route.fulfill({ status: 404, json: { title: "Không có" } });
  });
  await page.route("**/api/classes/*/schedule", (route) => {
    if (route.request().method() === "PUT" && s.failSchedule)
      return route.fulfill({ status: 500, json: { title: "Lỗi" } });
    return route.continue();
  });
  return s;
}

async function signIn(page: Page) {
  await page.goto("/", { waitUntil: "domcontentloaded" });
  await page.getByLabel("Email", { exact: true }).fill(process.env.DEMO_EMAIL!);
  await page
    .getByLabel("Mật khẩu", { exact: true })
    .fill(process.env.DEMO_PASSWORD!);
  await page.getByRole("button", { name: "Đăng nhập", exact: true }).click();
}

const progress = (page: Page, n: number) =>
  expect(
    page.getByRole("progressbar", { name: "Tiến độ thiết lập" }),
  ).toHaveAttribute("aria-valuetext", `Bước ${n}/3`);
const tabs = (page: Page) =>
  page.getByRole("navigation", { name: "Điều hướng nhanh" });
const classDialog = (page: Page) =>
  page.getByRole("dialog", { name: "Tạo lớp mới" });

async function fillClass(dialog: Locator, name: string) {
  await dialog.getByLabel("Tên lớp", { exact: true }).fill(name);
  await dialog.getByLabel("Môn học", { exact: true }).fill("Toán");
  await dialog.getByRole("button", { name: "Tiếp theo: lịch học" }).click();
  await dialog
    .getByRole("button", { name: todayWeekday(), exact: true })
    .click();
  await dialog
    .getByRole("button", { name: "Thêm vào lịch", exact: true })
    .click();
  await dialog.getByLabel("Học phí mỗi buổi", { exact: false }).fill("150000");
}

async function createClass(page: Page, s: Fresh) {
  const dialog = classDialog(page);
  await fillClass(dialog, s.name);
  await dialog.getByRole("button", { name: "Tạo lớp", exact: true }).click();
  await expect(dialog).toHaveCount(0, { timeout: 60_000 });
}

const made = async (page: Page, name: string) =>
  (
    (await (await page.request.get("/api/classes")).json()) as {
      name: string;
    }[]
  ).filter((c) => c.name === name);

test.describe("first-run setup", () => {
  test("a new teacher gets class, receiving account and students in three steps, with no navigation, and ends on Hôm nay", async ({
    page,
  }) => {
    const s = await freshTeacher(page);
    await signIn(page);
    await expect(page).toHaveURL(/#\/setup\/1$/);
    await progress(page, 1);
    await expect(tabs(page)).toBeHidden();
    await expect(page.locator(".sidebar")).toBeHidden();
    // Step 1 offers no way to skip: only the class form.
    await expect(page.getByRole("button", { name: "Để sau" })).toHaveCount(0);
    await createClass(page, s);

    await progress(page, 2);
    const bank = page.getByRole("combobox", { name: "Ngân hàng" });
    await bank.fill("vietin");
    await page.getByRole("option", { name: /VietinBank/ }).click();
    await page.getByLabel("Số tài khoản", { exact: true }).fill("0123456789");
    await page.getByLabel("Tên chủ tài khoản").fill("Nguyen Van A");
    await page.getByRole("button", { name: "Lưu tài khoản nhận tiền" }).click();
    await page.getByRole("button", { name: "Tiếp theo", exact: true }).click();

    await progress(page, 3);
    const tag = token();
    await page
      .getByLabel("Danh sách học sinh", { exact: true })
      .fill(`An ${tag}\nBình ${tag}`);
    await page.getByRole("button", { name: "Thêm 2 học sinh" }).click();
    await expect(page.getByText("Đã thêm 2 học sinh vào lớp.")).toBeVisible({
      timeout: 60_000,
    });
    await page.getByRole("button", { name: "Hoàn tất", exact: true }).click();

    await expect(page).toHaveURL(/#\/today$/);
    await expect(page.getByRole("heading", { name: "Hôm nay" })).toBeVisible();
    // Navigation is back: the bottom tabs on a phone, the sidebar on a desktop.
    await expect(
      test.info().project.name === "mobile"
        ? tabs(page)
        : page.locator(".sidebar"),
    ).toBeVisible();
    // The class has a session today, and all three tasks are done, so there is no card.
    await expect(page.getByText(s.name).first()).toBeVisible({
      timeout: 30_000,
    });
    await expect(page.getByLabel("Hoàn tất thiết lập")).toHaveCount(0);
    expect(await made(page, s.name)).toHaveLength(1);
  });

  test("steps 2 and 3 can be skipped; the card shows 1/3, follows data from another device, and can be hidden", async ({
    page,
  }) => {
    const s = await freshTeacher(page);
    await signIn(page);
    await createClass(page, s);
    await progress(page, 2);
    await page.getByRole("button", { name: "Để sau", exact: true }).click();
    await progress(page, 3);
    await page.getByRole("button", { name: "Quay lại" }).click();
    await progress(page, 2);
    await page.getByRole("button", { name: "Để sau", exact: true }).click();
    await progress(page, 3);
    await page.getByRole("button", { name: "Để sau", exact: true }).click();

    await expect(page.getByRole("heading", { name: "Hôm nay" })).toBeVisible();
    const card = page.getByLabel("Hoàn tất thiết lập");
    await expect(
      card.getByRole("heading", { name: "Hoàn tất thiết lập (1/3)" }),
    ).toBeVisible();
    await expect(card.getByText("Chưa có tài khoản nhận tiền")).toBeVisible();
    await expect(card.getByText("Chưa có học sinh")).toBeVisible();
    // The fee was set in step 1, so it is not listed.
    await expect(card.getByText("Chưa đặt học phí")).toHaveCount(0);
    await expect(
      card.getByRole("link", { name: "Dán danh sách học sinh" }),
    ).toBeVisible();

    // Skipping is final for that visit: Hôm nay is not sent back to the setup.
    await page.reload();
    await expect(page.getByRole("heading", { name: "Hôm nay" })).toBeVisible();
    await expect(page).toHaveURL(/#\/today$/);

    // The receiving account is added on another device: the card follows the data.
    s.otherDevice = true;
    await page.reload();
    await expect(
      page
        .getByLabel("Hoàn tất thiết lập")
        .getByRole("heading", { name: "Hoàn tất thiết lập (2/3)" }),
    ).toBeVisible();
    await expect(page.getByText("Chưa có tài khoản nhận tiền")).toHaveCount(0);

    await page.getByRole("button", { name: "Ẩn thẻ thiết lập" }).click();
    await expect(page.getByLabel("Hoàn tất thiết lập")).toHaveCount(0);
    await page.reload();
    await expect(page.getByRole("heading", { name: "Hôm nay" })).toBeVisible();
    await expect(page.getByLabel("Hoàn tất thiết lập")).toHaveCount(0);
  });

  test("leaving in the middle of step 1 comes back to the right place and never makes a second class", async ({
    page,
  }) => {
    const s = await freshTeacher(page);
    await signIn(page);
    // Nothing exists yet: a reload simply starts step 1 again.
    await expect(page).toHaveURL(/#\/setup\/1$/);
    await expect(classDialog(page)).toBeVisible();
    await page.reload();
    await expect(page).toHaveURL(/#\/setup\/1$/);
    await expect(classDialog(page)).toBeVisible();

    // The class is made, but its schedule fails; the teacher leaves with "Đóng, làm tiếp sau".
    s.failSchedule = true;
    const dialog = classDialog(page);
    await fillClass(dialog, s.name);
    await dialog.getByRole("button", { name: "Tạo lớp", exact: true }).click();
    await expect(dialog.getByRole("alert")).toBeVisible({ timeout: 60_000 });
    await dialog.getByRole("button", { name: "Đóng, làm tiếp sau" }).click();
    await expect(dialog).toHaveCount(0);
    await expect(page.getByText(`Đã tạo lớp “${s.name}”`)).toBeVisible();

    // Opening the app again lands on step 2, and the class exists once.
    await page.goto("/#/today");
    await page.reload();
    await expect(page).toHaveURL(/#\/setup\/2$/);
    await progress(page, 2);
    expect(await made(page, s.name)).toHaveLength(1);
  });

  test("an account that cannot write is not let into the setup", async ({
    page,
  }) => {
    await page.route("**/api/auth/me", async (route) => {
      const response = await route.fetch();
      // Signed out, the profile is a 401 with no body: leave it alone.
      if (!response.ok()) return route.fulfill({ response });
      const me = await response.json();
      return route.fulfill({
        response,
        json: { ...me, access: { ...me.access, canWrite: false } },
      });
    });
    await signIn(page);
    await expect(page.getByRole("heading", { name: "Hôm nay" })).toBeVisible();
    await page.evaluate(() => (window.location.hash = "#/setup/1"));
    await expect(page.getByRole("heading", { name: "Hôm nay" })).toBeVisible();
    await expect(page).toHaveURL(/#\/today$/);
    await expect(page.getByRole("progressbar")).toHaveCount(0);
  });
});
