import { test, expect } from "@playwright/test";

// PDF storage (R2) is not needed here: the reporting API and the file download are stubbed so the
// test checks what the screen does with them: which export is chosen, that it is downloaded, and that Zalo opens
// on the parent's chat. Nothing goes through the Web Share sheet (no nearby sharing).
test("Hóa đơn downloads or sends via Zalo the up-to-date monthly report PDF of the student", async ({
  page,
}) => {
  await page.goto("/");
  await page.getByLabel("Email", { exact: true }).fill(process.env.DEMO_EMAIL!);
  await page
    .getByLabel("Mật khẩu", { exact: true })
    .fill(process.env.DEMO_PASSWORD!);
  await page.getByRole("button", { name: "Đăng nhập", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Hôm nay" })).toBeVisible();

  async function write(path: string, data: unknown, method = "POST") {
    const csrf = await (await page.request.get("/api/auth/csrf")).json();
    const response = await page.request.fetch(`/api${path}`, {
      method,
      data,
      headers: { "X-CSRF-TOKEN": csrf.token },
    });
    expect(response.ok()).toBeTruthy();
    return response.json();
  }
  const stamp = Date.now();
  const studentName = `Bình chia sẻ ${stamp}`;
  const classroom = await write("/classes", {
    name: `Chia sẻ ${stamp}`,
    subject: "Toán",
  });
  const student = await write("/students", {
    name: studentName,
    phone: "0901 234 567",
  });
  await write(`/classes/${classroom.id}/enrollments`, {
    studentId: student.id,
    startDate: "2026-10-01",
  });
  await write(`/billing/classes/${classroom.id}/rates`, {
    effectiveFrom: "2026-10-01",
    unitPriceVnd: "500000",
    version: 0,
  });
  const session = await write(`/classes/${classroom.id}/sessions`, {
    date: "2026-10-02",
    startTime: "18:00:00",
    endTime: "19:00:00",
    kind: "regular",
  });
  await write(
    `/classes/${classroom.id}/sessions/${session.id}`,
    { status: "held", version: 1 },
    "PUT",
  );
  await write(
    `/classes/${classroom.id}/sessions/${session.id}/attendance/${student.id}`,
    { status: "present", version: 0 },
    "PUT",
  );
  await expect
    .poll(async () => {
      const report = await (
        await page.request.get(
          `/api/billing/classes/${classroom.id}/payments?month=2026-10`,
        )
      ).json();
      return report.status;
    })
    .toBe("ready");

  const calls: string[] = [];
  const path = `/api/reporting/classes/${classroom.id}/students/${student.id}/months/2026-10`;
  await page.route(`**${path}/exports`, (route) => {
    calls.push(`${route.request().method()} exports`);
    return route.fulfill({
      json: [
        {
          id: "old",
          state: "ready",
          createdAt: "2026-10-03T00:00:00Z",
          byteLength: 9,
          sha256: null,
          draftVersion: 1,
          sourceStamp: "x",
          warnings: [],
          freshness: { state: "outdated", changes: [] },
        },
        {
          id: "fresh",
          state: "ready",
          createdAt: "2026-10-04T00:00:00Z",
          byteLength: 9,
          sha256: null,
          draftVersion: 1,
          sourceStamp: "y",
          warnings: [],
          freshness: { state: "current", changes: [] },
        },
      ],
    });
  });
  await page.route(`**${path}/exports/*/download-link`, (route) => {
    calls.push(`link ${route.request().url().split("/exports/")[1]}`);
    return route.fulfill({ json: { url: "/stub-report.pdf" } });
  });
  await page.route("**/stub-report.pdf", (route) =>
    route.fulfill({ contentType: "application/pdf", body: "%PDF-1.4 stub" }),
  );

  await page.evaluate(() => (window.location.hash = "#/billing"));
  await page.reload();
  await page.getByRole("combobox", { name: /^Lớp/ }).selectOption(classroom.id);
  await page.getByLabel("Tháng").fill("2026-10");
  const card = page.getByRole("listitem").filter({ hasText: studentName });
  // Download: the current export is chosen, the outdated one is skipped.
  const download = page.waitForEvent("download");
  await card.getByRole("button", { name: "Tải báo cáo PDF" }).click();
  expect((await download).suggestedFilename()).toBe(
    `bao-cao-2026-10-${studentName.replace(/\s+/g, "-")}.pdf`,
  );
  await expect(card.getByRole("status")).toContainText("Đã tải PDF về máy");
  expect(calls).toContain("link fresh/download-link");
  expect(calls.some((c) => c.includes("old"))).toBe(false);

  // Zalo: the PDF is downloaded and the chat of the parent's phone number opens in a new tab.
  // Routed on the context: the new tab is not the page.
  await page
    .context()
    .route("https://zalo.me/**", (route) =>
      route.fulfill({ contentType: "text/html", body: "zalo" }),
    );
  const zaloTab = page.waitForEvent("popup");
  const zaloDownload = page.waitForEvent("download");
  await card.getByRole("button", { name: "Gửi qua Zalo" }).click();
  await zaloDownload;
  const tab = await zaloTab;
  await expect.poll(() => tab.url()).toBe("https://zalo.me/0901234567");
  await expect(card.getByRole("status")).toContainText("Đính kèm file này");

  // The preview lives on the monthly report screen, reached from the row.
  await expect(
    card.getByRole("link", { name: "Xem trước PDF" }),
  ).toHaveAttribute(
    "href",
    new RegExp(`reportStudent=${student.id}&reportMonth=2026-10`),
  );
});
