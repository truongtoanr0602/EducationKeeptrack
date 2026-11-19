import { test, expect, type Page } from "@playwright/test";

// The receiving-account form (#54). The recipient belongs to the teacher, not to a class, so these tests share one
// row; run them in order with a single worker (`--workers=1`) and reset the account through the API in each test.
test.describe.configure({ mode: "serial" });

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
  return page.request.fetch(`/api${path}`, {
    method: "POST",
    data,
    headers: { "X-CSRF-TOKEN": csrf.token },
  });
}

async function saveViaApi(
  page: Page,
  account: { bankBin: string; bankName: string; accountNo?: string },
) {
  const current = await page.request.get("/api/billing/recipient");
  const version = current.ok() ? (await current.json()).version : 0;
  const response = await write(page, "/billing/recipient", {
    accountNo: "0123456789",
    accountName: "NGUYEN VAN AN",
    staticQrBase64: null,
    version,
    ...account,
  });
  expect(response.ok(), await response.text()).toBeTruthy();
}

async function openSettings(page: Page) {
  const made = await (
    await write(page, "/classes", {
      name: `Lớp ngân hàng ${Date.now()}-${Math.random().toString(36).slice(2, 6)}`,
      subject: "Toán",
    })
  ).json();
  await page.evaluate(
    (id) => (window.location.hash = `#/classes/${id}/tuition`),
    made.id,
  );
  const settings = page.getByRole("region", {
    name: "Tài khoản nhận tiền",
    exact: true,
  });
  await expect(
    settings.getByRole("button", { name: "Lưu tài khoản nhận tiền" }),
  ).toBeEnabled();
  return settings;
}

const saved = async (page: Page) =>
  (await page.request.get("/api/billing/recipient")).json();

test.describe("bank picker and receiving account", () => {
  test("a bank is chosen by typing part of its name, and the BIN is filled in for the teacher", async ({
    page,
  }) => {
    await login(page);
    await saveViaApi(page, { bankBin: "970436", bankName: "Vietcombank" });
    const settings = await openSettings(page);
    const bank = settings.getByRole("combobox", { name: "Ngân hàng" });
    await expect(bank).toHaveValue("Vietcombank");

    await bank.fill("vietin");
    await expect(settings.getByRole("option").first()).toContainText(
      "VietinBank",
    );
    await settings.getByRole("option", { name: /VietinBank/ }).click();
    await expect(bank).toHaveValue("VietinBank");
    await settings
      .getByRole("button", { name: "Lưu tài khoản nhận tiền" })
      .click();
    await expect.poll(async () => (await saved(page)).bankBin).toBe("970415");
    expect((await saved(page)).bankName).toBe("VietinBank");
  });

  test("search ignores accents and works with the bank code, and the keyboard picks without saving the form", async ({
    page,
  }) => {
    await login(page);
    await saveViaApi(page, { bankBin: "970415", bankName: "VietinBank" });
    const settings = await openSettings(page);
    const bank = settings.getByRole("combobox", { name: "Ngân hàng" });

    await bank.fill("ngoai thuong");
    await expect(settings.getByRole("option").first()).toContainText(
      "Vietcombank",
    );
    await bank.fill("vcb");
    await bank.press("ArrowDown");
    await bank.press("ArrowUp");
    await bank.press("Enter");
    await expect(bank).toHaveValue("Vietcombank");
    // Enter chose the bank; it did not submit the form.
    expect((await saved(page)).bankBin).toBe("970415");
    await expect(settings.getByRole("alert")).toHaveCount(0);
  });

  test("the account name and number are cleaned up as they are typed", async ({
    page,
  }) => {
    await login(page);
    await saveViaApi(page, { bankBin: "970415", bankName: "VietinBank" });
    const settings = await openSettings(page);
    const name = settings.getByLabel("Tên chủ tài khoản");
    await name.fill("Nguyễn Văn Đạt");
    await expect(name).toHaveValue("NGUYEN VAN DAT");
    const number = settings.getByLabel("Số tài khoản", { exact: true });
    await number.fill("0123 456-789");
    await expect(number).toHaveValue("0123456789");
    await settings
      .getByRole("button", { name: "Lưu tài khoản nhận tiền" })
      .click();
    await expect
      .poll(async () => (await saved(page)).accountName)
      .toBe("NGUYEN VAN DAT");
  });

  test("typed text that was never picked from the list does not save, and says why", async ({
    page,
  }) => {
    await login(page);
    await saveViaApi(page, { bankBin: "970415", bankName: "VietinBank" });
    const settings = await openSettings(page);
    const bank = settings.getByRole("combobox", { name: "Ngân hàng" });
    await bank.fill("vietin");
    // Leave the field with Tab, as a keyboard user would; the open list covers the fields below it.
    await bank.press("Tab");
    await expect(settings.getByRole("listbox")).toHaveCount(0);
    await expect(bank).toHaveValue("");
    await settings
      .getByRole("button", { name: "Lưu tài khoản nhận tiền" })
      .click();
    await expect(settings).toContainText("Chọn ngân hàng từ danh sách.");
    expect((await saved(page)).bankBin).toBe("970415");
  });

  test("a saved bank that is not in the list keeps showing and saves unchanged", async ({
    page,
  }) => {
    await login(page);
    // GPBank (970408) is valid for the server but is not offered by the list.
    await saveViaApi(page, { bankBin: "970408", bankName: "GPBank" });
    const settings = await openSettings(page);
    await expect(settings.getByLabel("Mã BIN ngân hàng")).toHaveValue("970408");
    await expect(settings.getByLabel("Tên ngân hàng")).toHaveValue("GPBank");
    await settings
      .getByRole("button", { name: "Lưu tài khoản nhận tiền" })
      .click();
    await expect(
      settings.getByRole("button", { name: "Lưu tài khoản nhận tiền" }),
    ).toBeEnabled();
    const after = await saved(page);
    expect([after.bankBin, after.bankName]).toEqual(["970408", "GPBank"]);
  });

  test("another bank can be typed by hand through the last option of the list", async ({
    page,
  }) => {
    await login(page);
    await saveViaApi(page, { bankBin: "970415", bankName: "VietinBank" });
    const settings = await openSettings(page);
    const bank = settings.getByRole("combobox", { name: "Ngân hàng" });
    await bank.fill("zzzzqq");
    await expect(settings.getByRole("option")).toHaveCount(1);
    await settings.getByRole("option", { name: /Ngân hàng khác/ }).click();
    await settings.getByLabel("Mã BIN ngân hàng").fill("970406");
    await settings.getByLabel("Tên ngân hàng").fill("Vikki");
    await settings
      .getByRole("button", { name: "Lưu tài khoản nhận tiền" })
      .click();
    await expect.poll(async () => (await saved(page)).bankBin).toBe("970406");
    await settings.getByLabel("Mã BIN ngân hàng").fill("971005");
    await settings
      .getByRole("button", { name: "Lưu tài khoản nhận tiền" })
      .click();
    await expect(settings).toContainText(
      "Mã BIN ngân hàng phải gồm 6 chữ số, bắt đầu bằng 970.",
    );
    expect((await saved(page)).bankBin).toBe("970406");
  });

  test("the sample QR uses the saved account and the typed amount, and is switched off while the form has unsaved edits", async ({
    page,
  }) => {
    await login(page);
    await saveViaApi(page, { bankBin: "970436", bankName: "Vietcombank" });
    const settings = await openSettings(page);
    const sample = settings.getByRole("region", { name: "QR mẫu" });
    const make = sample.getByRole("button", { name: "Tạo QR mẫu" });
    await expect(make).toBeEnabled();

    // An unsaved edit switches it off and says why.
    const number = settings.getByLabel("Số tài khoản", { exact: true });
    await number.fill("999999999");
    await expect(make).toBeDisabled();
    await expect(sample).toContainText("Bạn đã sửa thông tin chưa lưu");
    await number.fill("0123456789");
    await expect(make).toBeEnabled();

    await sample.getByLabel("Số tiền mẫu (đồng)").fill("150000");
    await make.click();
    // The real VietQR service may be unreachable here; either the image or the clear fallback message is correct.
    // The exact amount sent to the generator is asserted by the API tests.
    await expect(
      sample
        .getByRole("img", { name: /QR mẫu chuyển 150\.000/ })
        .or(sample.getByText(/Chưa tạo được QR lúc này/))
        .or(sample.getByText(/QR tĩnh bạn đã tải lên/)),
    ).toBeVisible({ timeout: 30_000 });
    await expect(sample.getByRole("alert")).toHaveCount(0);

    await sample.getByLabel("Số tiền mẫu (đồng)").fill("");
    await expect(make).toBeDisabled();
  });
});
