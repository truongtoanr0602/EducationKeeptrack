import { test, expect, type Page } from "@playwright/test";

// The weekly schedule editor (#53). Every test works on its own class, so they are independent.

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

async function newClass(page: Page) {
  const made = await write(page, "/classes", {
    name: `Lịch tuần ${Date.now()}-${Math.random().toString(36).slice(2, 6)}`,
    subject: "Toán",
  });
  return made.id as string;
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

const chip = (page: Page, day: string) =>
  page.getByRole("button", { name: day, exact: true });
const slotList = (page: Page) =>
  page.getByRole("list", { name: "Khung giờ trong lịch tuần" });
const add = (page: Page) =>
  page.getByRole("button", { name: "Thêm vào lịch", exact: true }).click();

async function times(page: Page, start: string, end: string) {
  await page.getByLabel("Giờ bắt đầu", { exact: true }).fill(start);
  await page.getByLabel("Giờ kết thúc", { exact: true }).fill(end);
}

test.describe("weekly schedule editor", () => {
  test("T2-T4-T6 at 18:00 for 90 minutes shows up in the list, the grid and the month preview", async ({
    page,
  }) => {
    await login(page);
    await openEditor(page, await newClass(page));
    for (const day of ["Thứ Hai", "Thứ Tư", "Thứ Sáu"])
      await chip(page, day).click();
    await page.getByRole("button", { name: "90 phút" }).click();
    await expect(page.getByLabel("Giờ kết thúc", { exact: true })).toHaveValue(
      "19:30",
    );
    await add(page);

    const items = slotList(page).getByRole("listitem");
    await expect(items).toHaveCount(3);
    await expect(items.nth(0)).toContainText("Thứ Hai · 18:00–19:30");
    await expect(items.nth(1)).toContainText("Thứ Tư · 18:00–19:30");
    await expect(items.nth(2)).toContainText("Thứ Sáu · 18:00–19:30");
    await expect(page.locator(".week-block[data-slot]")).toHaveCount(3);
    // Five weeks always contain exactly five of each weekday.
    await expect(page.locator(".month-preview td[data-sessions]")).toHaveCount(
      15,
    );
    // The chosen days are cleared so the next entry starts fresh.
    await expect(chip(page, "Thứ Hai")).toHaveAttribute(
      "aria-pressed",
      "false",
    );
  });

  test("a clashing slot on the same day is refused, an adjacent one is accepted", async ({
    page,
  }) => {
    await login(page);
    await openEditor(page, await newClass(page));
    await chip(page, "Thứ Hai").click();
    await times(page, "18:00", "19:30");
    await add(page);

    await chip(page, "Thứ Hai").click();
    await times(page, "19:00", "20:00");
    await add(page);
    await expect(page.getByRole("alert")).toContainText(
      "Trùng giờ với Thứ Hai · 18:00–19:30.",
    );
    await expect(slotList(page).getByRole("listitem")).toHaveCount(1);

    await times(page, "19:30", "20:30");
    await add(page);
    await expect(slotList(page).getByRole("listitem")).toHaveCount(2);
  });

  test("deleting a slot updates the list, the grid and the preview", async ({
    page,
  }) => {
    await login(page);
    await openEditor(page, await newClass(page));
    await chip(page, "Thứ Hai").click();
    await chip(page, "Thứ Tư").click();
    await times(page, "18:00", "19:30");
    await add(page);
    await expect(page.locator(".month-preview td[data-sessions]")).toHaveCount(
      10,
    );

    await page
      .getByRole("button", { name: "Xóa Thứ Hai · 18:00–19:30" })
      .click();
    await expect(slotList(page).getByRole("listitem")).toHaveCount(1);
    await expect(page.locator(".week-block[data-slot]")).toHaveCount(1);
    await expect(page.locator(".month-preview td[data-sessions]")).toHaveCount(
      5,
    );
  });

  test("editing a slot from the list or by tapping its block changes it in place", async ({
    page,
  }) => {
    await login(page);
    await openEditor(page, await newClass(page));
    await chip(page, "Thứ Hai").click();
    await times(page, "18:00", "19:30");
    await add(page);

    await page
      .getByRole("button", { name: "Sửa Thứ Hai · 18:00–19:30" })
      .click();
    await expect(
      page.getByText("Sửa khung giờ", { exact: true }),
    ).toBeVisible();
    await page.getByLabel("Giờ kết thúc", { exact: true }).fill("20:00");
    await page
      .getByRole("button", { name: "Cập nhật khung giờ", exact: true })
      .click();
    await expect(slotList(page).getByRole("listitem")).toHaveCount(1);
    await expect(slotList(page)).toContainText("Thứ Hai · 18:00–20:00");

    // Tapping the block in the grid opens the same edit state.
    await page.locator(".week-block[data-slot='0']").click();
    await expect(
      page.getByText("Sửa khung giờ", { exact: true }),
    ).toBeVisible();
    await expect(page.getByLabel("Giờ bắt đầu", { exact: true })).toHaveValue(
      "18:00",
    );
    await page.getByRole("button", { name: "Hủy sửa" }).click();
    await expect(
      page.getByText("Thêm khung giờ", { exact: true }),
    ).toBeVisible();
  });

  test("a stored slot with odd times is shown as saved and keeps its times when saved again", async ({
    page,
  }) => {
    await login(page);
    const id = await newClass(page);
    await write(
      page,
      `/classes/${id}/schedule`,
      {
        version: 0,
        effectiveFrom: "2026-10-01",
        slots: [{ dayOfWeek: 1, startTime: "18:10:00", endTime: "18:55:00" }],
      },
      "PUT",
    );
    await openEditor(page, id);
    await expect(slotList(page)).toContainText("Thứ Hai · 18:10–18:55");
    await page
      .getByRole("button", { name: "Lưu lịch tuần", exact: true })
      .click();
    await expect(
      page
        .getByRole("list", { name: "Lịch tuần đã lưu" })
        .getByText("Thứ Hai · 18:10–18:55", { exact: true }),
    ).toBeVisible();
  });

  test("an entry that was typed but not added blocks saving with an explanation", async ({
    page,
  }) => {
    await login(page);
    await openEditor(page, await newClass(page));
    await chip(page, "Thứ Hai").click();
    await page
      .getByRole("button", { name: "Lưu lịch tuần", exact: true })
      .click();
    await expect(page.getByRole("alert")).toContainText("chưa thêm vào lịch");
  });

  test("removing every slot and saving stops the weekly schedule", async ({
    page,
  }) => {
    await login(page);
    const id = await newClass(page);
    await write(
      page,
      `/classes/${id}/schedule`,
      {
        version: 0,
        effectiveFrom: "2026-10-01",
        slots: [{ dayOfWeek: 2, startTime: "17:00:00", endTime: "18:00:00" }],
      },
      "PUT",
    );
    await openEditor(page, id);
    await page
      .getByRole("button", { name: "Xóa Thứ Ba · 17:00–18:00" })
      .click();
    await expect(page.getByText("Chưa có khung giờ nào")).toBeVisible();
    await page
      .getByRole("button", { name: "Lưu lịch tuần", exact: true })
      .click();
    await expect(
      page.getByRole("button", { name: "Thiết lập lịch tuần" }),
    ).toBeEnabled();
    await expect(
      page
        .getByRole("list", { name: "Lịch tuần đã lưu" })
        .getByRole("listitem"),
    ).toHaveCount(0);
  });

  test("an evening class is visible in the grid without scrolling it", async ({
    page,
  }) => {
    await login(page);
    await openEditor(page, await newClass(page));
    await chip(page, "Thứ Hai").click();
    await times(page, "18:00", "19:30");
    await add(page);
    await chip(page, "Thứ Ba").click();
    await times(page, "21:00", "22:00");
    await add(page);
    const hidden = await page
      .locator(".week-block[data-slot]")
      .evaluateAll((blocks) => {
        const frame = document
          .querySelector(".week-calendar")!
          .getBoundingClientRect();
        return blocks.filter((block) => {
          const box = block.getBoundingClientRect();
          return box.top < frame.top || box.bottom > frame.bottom + 1;
        }).length;
      });
    expect(hidden).toBe(0);
  });

  test("dragging on a day column draws a slot (desktop pointer only)", async ({
    page,
  }, testInfo) => {
    test.skip(testInfo.project.name !== "desktop", "needs a mouse pointer");
    await login(page);
    await openEditor(page, await newClass(page));
    const column = page.locator(".week-col[data-day='1']");
    // Put the grid at the top of the viewport so the whole 18:00-19:30 stretch is on screen.
    await page.locator(".week-calendar").evaluate((el) => {
      el.scrollTop = 0;
      el.scrollIntoView({ block: "start" });
    });
    const box = (await column.boundingBox())!;
    // The grid is 22px an hour from its first hour label (06:00 by default; other classes' earlier slots widen it).
    const firstHour = Number(
      await page.locator(".week-hours span").first().textContent(),
    );
    const at = (hour: number) => (hour - firstHour) * 22;
    const x = box.x + box.width / 2;
    await page.mouse.move(x, box.y + at(18));
    await page.mouse.down();
    await page.mouse.move(x, box.y + at(18) + 16, { steps: 4 });
    await page.mouse.move(x, box.y + at(19.5), { steps: 4 });
    await page.mouse.up();
    await expect(slotList(page)).toContainText("Thứ Hai · 18:00–19:30");
  });
});
