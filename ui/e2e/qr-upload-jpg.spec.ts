import { test, expect } from "@playwright/test";
import { openRecipientSettings } from "./helpers";

// A JPEG or WebP QR is converted to PNG in the browser; the server still stores and serves PNG only.
test("a JPG static QR is converted to PNG, saved and shown; a text file is refused", async ({
  page,
}) => {
  await page.goto("/");
  await page.getByLabel("Email", { exact: true }).fill(process.env.DEMO_EMAIL!);
  await page
    .getByLabel("Mật khẩu", { exact: true })
    .fill(process.env.DEMO_PASSWORD!);
  await page.getByRole("button", { name: "Đăng nhập", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Hôm nay" })).toBeVisible();
  const csrf = await (await page.request.get("/api/auth/csrf")).json();
  const created = await page.request.post("/api/classes", {
    data: { name: `QR jpg ${Date.now()}`, subject: "Toán" },
    headers: { "X-CSRF-TOKEN": csrf.token },
  });
  const classroom = await created.json();
  await page.evaluate(
    (id) => (window.location.hash = `#/classes/${id}/tuition`),
    classroom.id,
  );
  const settings = await openRecipientSettings(
    page.getByRole("region", { name: "Thanh toán và công nợ", exact: true }),
  );

  // A 600x600 JPEG with a flat black-and-white pattern, drawn in the page.
  const jpeg = await page.evaluate(async () => {
    const canvas = document.createElement("canvas");
    canvas.width = canvas.height = 600;
    const c = canvas.getContext("2d")!;
    c.fillStyle = "#fff";
    c.fillRect(0, 0, 600, 600);
    c.fillStyle = "#000";
    for (let y = 0; y < 10; y++)
      for (let x = 0; x < 10; x++)
        if ((x * 7 + y * 3) % 4 < 2)
          c.fillRect(60 + x * 48, 60 + y * 48, 48, 48);
    const blob = await new Promise<Blob>((r) =>
      canvas.toBlob((b) => r(b!), "image/jpeg", 0.9),
    );
    const reader = new FileReader();
    return await new Promise<string>((resolve) => {
      reader.onload = () => resolve(String(reader.result).split(",")[1]);
      reader.readAsDataURL(blob);
    });
  });
  const upload = settings.getByLabel(/QR ngân hàng tĩnh/);
  await upload.setInputFiles({
    name: "qr.jpg",
    mimeType: "image/jpeg",
    buffer: Buffer.from(jpeg, "base64"),
  });
  const preview = settings.locator("img.payment-qr");
  await expect(preview).toBeVisible();
  expect(await preview.getAttribute("src")).toMatch(/^data:image\/png;base64,/);
  const bytes = await preview.evaluate(async (img: HTMLImageElement) => {
    const response = await fetch(img.src);
    return new Uint8Array(await response.arrayBuffer()).slice(0, 8).join(",");
  });
  expect(bytes).toBe("137,80,78,71,13,10,26,10"); // PNG signature

  // Save it with the bank details and read it back from the server.
  const bank = settings.getByRole("combobox", { name: "Ngân hàng" });
  await bank.fill("vietin");
  await settings.getByRole("option", { name: /VietinBank/ }).click();
  await settings.getByLabel("Số tài khoản", { exact: true }).fill("123456789");
  await settings.getByLabel("Tên chủ tài khoản").fill("Nguyễn Văn An");
  await settings
    .getByRole("button", { name: "Lưu tài khoản nhận tiền", exact: true })
    .click();
  await expect
    .poll(
      async () =>
        (await (await page.request.get("/api/billing/recipient")).json())
          .hasStaticQr,
    )
    .toBe(true);

  // Anything that is not PNG, JPG or WebP is refused with a message.
  await upload.setInputFiles({
    name: "ghi-chu.txt",
    mimeType: "text/plain",
    buffer: Buffer.from("not an image"),
  });
  await expect(
    settings.getByText("Chọn ảnh PNG, JPG hoặc WebP."),
  ).toBeVisible();
});
