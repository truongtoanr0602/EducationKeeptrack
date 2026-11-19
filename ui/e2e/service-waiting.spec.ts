import { test, expect, type Page } from "@playwright/test";

// The page a teacher sees before being approved (#58). The demo teacher is active, so the service profile is replaced
// by a waiting one while a test says so; every other request is real.

type Kind = "unverified" | "pending" | "waiting" | "rejected" | "active";
const profile = (real: Record<string, unknown>, kind: Kind) => {
  if (kind === "active") return real;
  const status = kind === "waiting" ? "pending" : kind;
  const request = (state: string, reason: string | null) => ({
    id: "00000000-0000-0000-0000-000000000001",
    transactionCode: "FT-WAIT-1",
    note: "",
    submittedAt: new Date().toISOString(),
    status: state,
    decisionReason: reason,
    decidedAt: null,
  });
  return {
    ...real,
    status,
    access: { canRead: false, canWrite: false, canUseAi: false, status },
    periods: [],
    ai: { ...(real.ai as object), canUseAi: false },
    requests:
      kind === "waiting"
        ? [request("submitted", null)]
        : kind === "rejected"
          ? [request("rejected", "Không thấy khoản chuyển")]
          : [],
  };
};

async function stubService(page: Page, initial: Kind) {
  const state = { kind: initial, calls: 0 };
  await page.route("**/api/service", async (route) => {
    if (route.request().method() !== "GET") return route.continue();
    state.calls++;
    const response = await route.fetch();
    return route.fulfill({
      response,
      json: profile(await response.json(), state.kind),
    });
  });
  return state;
}

async function signIn(page: Page) {
  await page.goto("/", { waitUntil: "domcontentloaded" });
  await page.getByLabel("Email", { exact: true }).fill(process.env.DEMO_EMAIL!);
  await page
    .getByLabel("Mật khẩu", { exact: true })
    .fill(process.env.DEMO_PASSWORD!);
  await page.getByRole("button", { name: "Đăng nhập", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Hôm nay" })).toBeVisible();
}
const open = (page: Page) =>
  page.evaluate(() => (window.location.hash = "#/account"));
const steps = (page: Page) => page.getByLabel("Tiến trình kích hoạt");

test.describe("waiting for approval", () => {
  const cases: [Kind, string, string][] = [
    ["unverified", "Xác minh email", "Gửi lại email xác minh"],
    ["pending", "Chuyển phí dịch vụ và gửi mã giao dịch", "Nhập mã giao dịch"],
    ["waiting", "Chờ Admin duyệt", "Kiểm tra lại"],
    [
      "rejected",
      "Chuyển phí dịch vụ và gửi mã giao dịch",
      "Gửi lại thông tin đối chiếu",
    ],
  ];
  for (const [kind, current, action] of cases) {
    test(`${kind}: the bar marks "${current}" and offers "${action}"`, async ({
      page,
    }) => {
      await stubService(page, kind);
      await signIn(page);
      await open(page);
      const bar = steps(page);
      await expect(bar.getByRole("listitem")).toHaveCount(3);
      await expect(bar.locator("[aria-current=step]")).toContainText(current);
      await expect(bar.locator("[aria-current=step]")).toHaveCount(1);
      await expect(bar.getByRole("button")).toHaveCount(1);
      await expect(bar.getByRole("button", { name: action })).toBeVisible();
      // Steps before the current one are marked done.
      const doneCount = kind === "unverified" ? 0 : kind === "waiting" ? 2 : 1;
      await expect(bar.locator("li.done")).toHaveCount(doneCount);
    });
  }

  test("the code field takes focus from the step action", async ({ page }) => {
    await stubService(page, "pending");
    await signIn(page);
    await open(page);
    await steps(page)
      .getByRole("button", { name: "Nhập mã giao dịch" })
      .click();
    await expect(page.getByLabel("Mã giao dịch chuyển khoản")).toBeFocused();
  });

  test("an active teacher has no progress bar", async ({ page }) => {
    await signIn(page);
    await open(page);
    await expect(
      page.getByRole("heading", { name: "Quyền sử dụng" }),
    ).toBeVisible();
    await expect(steps(page)).toHaveCount(0);
  });

  test("after 30 seconds the page notices the approval by itself and goes to Hôm nay", async ({
    page,
  }) => {
    await page.clock.install();
    const state = await stubService(page, "waiting");
    await signIn(page);
    await open(page);
    await expect(steps(page).locator("[aria-current=step]")).toContainText(
      "Chờ Admin duyệt",
    );
    const before = state.calls;
    state.kind = "active";
    await page.clock.fastForward(31_000);
    await expect(page).toHaveURL(/#\/today$/);
    expect(state.calls).toBeGreaterThan(before);
  });

  test("a brand-new teacher is taken into the first setup when approved", async ({
    page,
  }) => {
    await page.route("**/api/classes", (route) =>
      route.request().method() === "GET"
        ? route.fulfill({ json: [] })
        : route.continue(),
    );
    const state = await stubService(page, "waiting");
    await page.goto("/", { waitUntil: "domcontentloaded" });
    await page
      .getByLabel("Email", { exact: true })
      .fill(process.env.DEMO_EMAIL!);
    await page
      .getByLabel("Mật khẩu", { exact: true })
      .fill(process.env.DEMO_PASSWORD!);
    await page.getByRole("button", { name: "Đăng nhập", exact: true }).click();
    await expect(page).toHaveURL(/#\/setup\/1$/);
    await open(page);
    await expect(steps(page).locator("[aria-current=step]")).toContainText(
      "Chờ Admin duyệt",
    );
    state.kind = "active";
    // Coming back to the tab checks at once, without waiting for the timer.
    await page.evaluate(() =>
      document.dispatchEvent(new Event("visibilitychange")),
    );
    await expect(page).toHaveURL(/#\/setup\/1$/);
    await expect(page.getByRole("progressbar")).toHaveAttribute(
      "aria-valuetext",
      "Bước 1/3",
    );
  });

  test("a failed background check shows no error and the next one still works", async ({
    page,
  }) => {
    await page.clock.install();
    await stubService(page, "waiting");
    await signIn(page);
    await open(page);
    await expect(steps(page)).toBeVisible();
    await page.route("**/api/service", (route) =>
      route.abort("connectionreset"),
    );
    await page.clock.fastForward(31_000);
    await expect(page.getByRole("alert")).toHaveCount(0);
    await expect(steps(page)).toBeVisible();
    await page.unroute("**/api/service");
    await stubService(page, "active");
    await page.clock.fastForward(31_000);
    await expect(page).toHaveURL(/#\/today$/);
  });
});
