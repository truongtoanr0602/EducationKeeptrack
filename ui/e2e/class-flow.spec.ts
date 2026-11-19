import {
  test,
  expect,
  type Locator,
  type Page,
  type Route,
} from "@playwright/test";

// Creating a class in one flow (#56), against the real backend. Every class gets a unique name and each test counts
// only its own classes, because the teacher's classes are shared by all tests.

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
const ymd = (d: Date) =>
  new Intl.DateTimeFormat("en-CA", {
    timeZone: "Asia/Ho_Chi_Minh",
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  }).format(d);
const todayYmd = () => ymd(new Date());
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
const addDays = (date: string, days: number) => {
  const d = new Date(`${date}T00:00:00Z`);
  d.setUTCDate(d.getUTCDate() + days);
  return d.toISOString().slice(0, 10);
};

async function login(page: Page) {
  await page.goto("/", { waitUntil: "domcontentloaded" });
  await page.getByLabel("Email", { exact: true }).fill(process.env.DEMO_EMAIL!);
  await page
    .getByLabel("Mật khẩu", { exact: true })
    .fill(process.env.DEMO_PASSWORD!);
  await page.getByRole("button", { name: "Đăng nhập", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Hôm nay" })).toBeVisible();
}

async function openDialog(page: Page) {
  await page.evaluate(() => (window.location.hash = "#/classes"));
  await page.getByRole("button", { name: "Tạo lớp mới", exact: true }).click();
  return page.getByRole("dialog", { name: "Tạo lớp mới" });
}

const classesNamed = async (page: Page, name: string) =>
  (
    (await (await page.request.get("/api/classes")).json()) as {
      id: string;
      name: string;
    }[]
  ).filter((c) => c.name === name);
const get = async (page: Page, path: string) =>
  (await page.request.get(`/api${path}`)).json();

async function fillInfo(dialog: Locator, name: string) {
  await dialog.getByLabel("Tên lớp", { exact: true }).fill(name);
  await dialog.getByLabel("Môn học", { exact: true }).fill("Toán");
}
const chip = (dialog: Locator, day: string) =>
  dialog.getByRole("button", { name: day, exact: true });
const next = (dialog: Locator) =>
  dialog.getByRole("button", { name: "Tiếp theo: lịch học" });
const add = (dialog: Locator) =>
  dialog.getByRole("button", { name: "Thêm vào lịch", exact: true });
const create = (dialog: Locator) =>
  dialog.getByRole("button", { name: "Tạo lớp", exact: true });
const retry = (dialog: Locator) =>
  dialog.getByRole("button", { name: "Thử lại", exact: true });

async function threeDays(dialog: Locator) {
  const start = DAYS.indexOf(todayWeekday());
  const days = [0, 2, 4].map((n) => DAYS[(start + n) % 7]);
  for (const day of days) await chip(dialog, day).click();
  await add(dialog).click();
  return days;
}

test.describe("create a class in one flow", () => {
  test("a class with T2-T4-T6 style schedule, a fee and four weeks of sessions, in under 12 taps, shows up on Hôm nay", async ({
    page,
  }) => {
    await login(page);
    const name = `Lớp một luồng ${token()}`;
    let taps = 0;
    const tap = async (locator: Locator) => (taps++, locator.click());
    const type = async (locator: Locator, text: string) => (
      taps++,
      locator.fill(text)
    );

    await page.evaluate(() => (window.location.hash = "#/classes"));
    await tap(page.getByRole("button", { name: "Tạo lớp mới", exact: true }));
    const dialog = page.getByRole("dialog", { name: "Tạo lớp mới" });
    await type(dialog.getByLabel("Tên lớp", { exact: true }), name);
    await type(dialog.getByLabel("Môn học", { exact: true }), "Toán");
    await tap(next(dialog));
    const start = DAYS.indexOf(todayWeekday());
    const days = [0, 2, 4].map((n) => DAYS[(start + n) % 7]);
    for (const day of days) await tap(chip(dialog, day));
    await tap(add(dialog));
    await type(
      dialog.getByLabel("Học phí mỗi buổi", { exact: false }),
      "150000",
    );
    // The preview shows exactly the days that will be created: 3 slots over 4 weeks.
    await expect(dialog.getByText("Xem trước 4 tuần tới")).toBeVisible();
    await expect(
      dialog.locator(".month-preview td[data-sessions]"),
    ).toHaveCount(12);
    await tap(create(dialog));
    await expect(dialog).toHaveCount(0, { timeout: 60_000 });
    expect(taps, `taps used: ${taps}`).toBeLessThan(12);

    const [made] = await classesNamed(page, name);
    expect(made).toBeTruthy();
    const schedule = await get(page, `/classes/${made.id}/schedule`);
    expect(schedule.version).toBe(1);
    expect(schedule.slots).toHaveLength(3);
    expect(
      schedule.slots.map(
        (s: { startTime: string; endTime: string }) =>
          `${s.startTime}-${s.endTime}`,
      ),
    ).toEqual(Array(3).fill("18:00:00-19:30:00"));
    const rates = await get(page, `/billing/classes/${made.id}/rates`);
    expect(
      rates.rates.map((r: { unitPriceVnd: string }) => Number(r.unitPriceVnd)),
    ).toEqual([150000]);
    const from = todayYmd();
    const sessions = await get(
      page,
      `/classes/${made.id}/sessions?from=${from}&to=${addDays(from, 27)}`,
    );
    expect(sessions).toHaveLength(12);
    expect(
      await get(
        page,
        `/classes/${made.id}/sessions?from=${addDays(from, 28)}&to=${addDays(from, 60)}`,
      ),
    ).toHaveLength(0);

    // The class has a session today, and the Today screen (a cached read) shows it.
    await page.evaluate(() => (window.location.hash = "#/today"));
    await expect(page.getByText(name).first()).toBeVisible({ timeout: 30_000 });
  });

  test("the 'Lưu lớp' quick path still creates only the class, as before", async ({
    page,
  }) => {
    await login(page);
    const name = `Lớp nhanh ${token()}`;
    const dialog = await openDialog(page);
    await fillInfo(dialog, name);
    await dialog.getByRole("button", { name: "Lưu lớp", exact: true }).click();
    await expect(dialog).toHaveCount(0, { timeout: 30_000 });
    await expect(
      page.getByRole("heading", { name, exact: true }),
    ).toBeVisible();
    const [made] = await classesNamed(page, name);
    expect((await get(page, `/classes/${made.id}/schedule`)).version).toBe(0);
    expect(await get(page, `/billing/classes/${made.id}/rates`)).toMatchObject({
      rates: [],
    });
  });

  test("without a schedule or a fee the flow creates just the class and skips the other steps", async ({
    page,
  }) => {
    await login(page);
    const name = `Lớp trống ${token()}`;
    const dialog = await openDialog(page);
    await fillInfo(dialog, name);
    await next(dialog).click();
    await expect(dialog.getByText("Chưa có khung giờ nào")).toBeVisible();
    await create(dialog).click();
    await expect(dialog).toHaveCount(0, { timeout: 30_000 });
    const [made] = await classesNamed(page, name);
    expect((await get(page, `/classes/${made.id}/schedule`)).version).toBe(0);
  });

  test("changing the weeks changes the preview and the sessions that are made", async ({
    page,
  }) => {
    await login(page);
    const name = `Lớp tám tuần ${token()}`;
    const dialog = await openDialog(page);
    await fillInfo(dialog, name);
    await next(dialog).click();
    await threeDays(dialog);
    await dialog.getByLabel("Số tuần tạo buổi học").selectOption("8");
    await expect(
      dialog.locator(".month-preview td[data-sessions]"),
    ).toHaveCount(24);
    await create(dialog).click();
    await expect(dialog).toHaveCount(0, { timeout: 60_000 });
    const [made] = await classesNamed(page, name);
    const from = todayYmd();
    expect(
      await get(
        page,
        `/classes/${made.id}/sessions?from=${from}&to=${addDays(from, 55)}`,
      ),
    ).toHaveLength(24);
  });

  test("a typed but un-added slot blocks creating, and nothing is created", async ({
    page,
  }) => {
    await login(page);
    const name = `Lớp dở ${token()}`;
    const dialog = await openDialog(page);
    await fillInfo(dialog, name);
    await next(dialog).click();
    await chip(dialog, "Thứ Hai").click();
    await create(dialog).click();
    await expect(dialog.getByRole("alert")).toContainText("chưa thêm vào lịch");
    expect(await classesNamed(page, name)).toHaveLength(0);
  });

  test("a missing class name sends the teacher back to step 1 with the message", async ({
    page,
  }) => {
    await login(page);
    const dialog = await openDialog(page);
    await dialog.getByLabel("Môn học", { exact: true }).fill("Toán");
    await next(dialog).click();
    await expect(dialog.getByText("Vui lòng nhập tên lớp.")).toBeVisible();
    await expect(dialog.getByText("Bước 1/2 · Thông tin lớp")).toBeVisible();
  });

  test("a failure after the class exists: the class is listed, 'Thử lại' finishes without a second class", async ({
    page,
  }) => {
    await login(page);
    const name = `Lớp lỗi ${token()}`;
    let failed = false;
    await page.route("**/api/classes/*/schedule", async (route: Route) => {
      if (route.request().method() !== "PUT" || failed) return route.continue();
      failed = true;
      return route.fulfill({
        status: 500,
        contentType: "application/json",
        body: "{}",
      });
    });
    const dialog = await openDialog(page);
    await fillInfo(dialog, name);
    await next(dialog).click();
    await threeDays(dialog);
    await create(dialog).click();
    await expect(dialog.getByRole("alert")).toContainText("Lớp đã được tạo");
    await expect(dialog.getByText("Lưu lịch tuần")).toBeVisible();
    expect(await classesNamed(page, name)).toHaveLength(1);

    await retry(dialog).click();
    await expect(dialog).toHaveCount(0, { timeout: 60_000 });
    const made = await classesNamed(page, name);
    expect(made).toHaveLength(1);
    expect((await get(page, `/classes/${made[0].id}/schedule`)).version).toBe(
      1,
    );
    const from = todayYmd();
    expect(
      await get(
        page,
        `/classes/${made[0].id}/sessions?from=${from}&to=${addDays(from, 27)}`,
      ),
    ).toHaveLength(12);
  });

  test("closing the dialog half-way leaves the class in the list", async ({
    page,
  }) => {
    await login(page);
    const name = `Lớp đóng ${token()}`;
    let failed = false;
    await page.route("**/api/classes/*/schedule", async (route: Route) => {
      if (route.request().method() !== "PUT" || failed) return route.continue();
      failed = true;
      return route.fulfill({
        status: 500,
        contentType: "application/json",
        body: "{}",
      });
    });
    const dialog = await openDialog(page);
    await fillInfo(dialog, name);
    await next(dialog).click();
    await threeDays(dialog);
    await create(dialog).click();
    await expect(dialog.getByRole("alert")).toBeVisible();
    await dialog.getByRole("button", { name: "Đóng, làm tiếp sau" }).click();
    await expect(dialog).toHaveCount(0);
    await expect(
      page.getByRole("heading", { name, exact: true }),
    ).toBeVisible();
  });

  test("an answer lost after the schedule was saved: the retry does not write it again (no conflict)", async ({
    page,
  }) => {
    await login(page);
    const name = `Lớp mất lịch ${token()}`;
    let lost = false;
    await page.route("**/api/classes/*/schedule", async (route: Route) => {
      if (route.request().method() !== "PUT" || lost) return route.continue();
      lost = true;
      await route.fetch(); // the server saves it, the browser never hears back
      return route.abort("connectionreset");
    });
    const dialog = await openDialog(page);
    await fillInfo(dialog, name);
    await next(dialog).click();
    await threeDays(dialog);
    await create(dialog).click();
    await expect(dialog.getByRole("alert")).toContainText("Mất kết nối");
    await retry(dialog).click();
    await expect(dialog).toHaveCount(0, { timeout: 60_000 });
    const [made] = await classesNamed(page, name);
    expect((await get(page, `/classes/${made.id}/schedule`)).version).toBe(1);
    const from = todayYmd();
    expect(
      await get(
        page,
        `/classes/${made.id}/sessions?from=${from}&to=${addDays(from, 27)}`,
      ),
    ).toHaveLength(12);
  });

  test("an answer lost after the class was created: the retry adopts it, so there is one class, not two", async ({
    page,
  }) => {
    await login(page);
    const name = `Lớp mất lớp ${token()}`;
    let lost = false;
    await page.route("**/api/classes", async (route: Route) => {
      if (route.request().method() !== "POST" || lost) return route.continue();
      lost = true;
      await route.fetch();
      return route.abort("connectionreset");
    });
    const dialog = await openDialog(page);
    await fillInfo(dialog, name);
    await next(dialog).click();
    await threeDays(dialog);
    await create(dialog).click();
    await expect(dialog.getByRole("alert")).toContainText("Mất kết nối");
    expect(await classesNamed(page, name)).toHaveLength(1); // the server did create it
    await retry(dialog).click();
    await expect(dialog).toHaveCount(0, { timeout: 60_000 });
    const made = await classesNamed(page, name);
    expect(made).toHaveLength(1);
    expect((await get(page, `/classes/${made[0].id}/schedule`)).version).toBe(
      1,
    );
  });

  test("the session ending half-way keeps the draft: after signing in again the dialog resumes without a second class", async ({
    page,
  }) => {
    await login(page);
    const name = `Lớp hết phiên ${token()}`;
    let expired = false;
    await page.route("**/api/classes/*/schedule", async (route: Route) => {
      if (route.request().method() !== "PUT" || expired)
        return route.continue();
      expired = true;
      return route.fulfill({
        status: 401,
        contentType: "application/json",
        body: JSON.stringify({ message: "Phiên đã hết hạn." }),
      });
    });
    const dialog = await openDialog(page);
    await fillInfo(dialog, name);
    await next(dialog).click();
    await threeDays(dialog);
    await create(dialog).click();
    await expect(
      page.getByText("Phiên đã hết hạn. Đăng nhập lại để tiếp tục"),
    ).toBeVisible({ timeout: 30_000 });
    expect(await classesNamed(page, name)).toHaveLength(1);

    await page
      .getByLabel("Email", { exact: true })
      .fill(process.env.DEMO_EMAIL!);
    await page
      .getByLabel("Mật khẩu", { exact: true })
      .fill(process.env.DEMO_PASSWORD!);
    await page.getByRole("button", { name: "Đăng nhập", exact: true }).click();
    // Signing in again reopens the dialog by itself, with what was typed and how far it got.
    const resumed = page.getByRole("dialog", { name: "Tạo lớp mới" });
    await expect(resumed).toBeVisible({ timeout: 30_000 });
    await expect(resumed.getByLabel("Tên lớp", { exact: true })).toHaveValue(
      name,
    );
    await expect(
      resumed.getByRole("list", { name: "Tiến trình tạo lớp" }),
    ).toContainText("Tạo lớp");
    await expect(
      resumed
        .getByRole("list", { name: "Tiến trình tạo lớp" })
        .getByRole("listitem")
        .filter({ hasText: "Tạo lớp" })
        .first(),
    ).toContainText("Xong");
    await retry(resumed).click();
    await expect(resumed).toHaveCount(0, { timeout: 60_000 });
    const made = await classesNamed(page, name);
    expect(made).toHaveLength(1);
    expect((await get(page, `/classes/${made[0].id}/schedule`)).version).toBe(
      1,
    );
  });
});
