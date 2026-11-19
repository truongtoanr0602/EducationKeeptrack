import { test, expect } from "@playwright/test";
import {
  accountErrors,
  digitsOnly,
  findBank,
  normalizeAccountName,
  searchBanks,
} from "../src/banks";
import { banks } from "../src/bank-list";

test.describe("bank picker logic (no browser, no server)", () => {
  test("every listed BIN satisfies the server rule and is unique", () => {
    expect(banks.length).toBeGreaterThan(30);
    for (const bank of banks) expect(bank.bin).toMatch(/^970[0-9]{3}$/);
    expect(new Set(banks.map((b) => b.bin)).size).toBe(banks.length);
  });

  test("well-known banks have the BIN VietQR documents", () => {
    expect(findBank("970436")?.shortName).toBe("Vietcombank");
    expect(findBank("970415")?.shortName).toBe("VietinBank");
    expect(findBank("970422")?.shortName).toBe("MBBank");
    expect(findBank("970418")?.shortName).toBe("BIDV");
    expect(findBank("970408")).toBeUndefined(); // saved but not listed: the form must still show it
  });

  test("search ignores accents and case and looks at name, short name and code", () => {
    expect(searchBanks("vietin")[0].shortName).toBe("VietinBank");
    expect(searchBanks("VCB")[0].shortName).toBe("Vietcombank");
    expect(searchBanks("ngoai thuong")[0].shortName).toBe("Vietcombank");
    expect(searchBanks("Ngoại Thương")[0].shortName).toBe("Vietcombank");
    expect(searchBanks("quan doi")[0].shortName).toBe("MBBank");
  });

  test("an exact code or name outranks banks that merely contain the text", () => {
    const mb = searchBanks("mb").map((b) => b.shortName);
    expect(mb[0]).toBe("MBBank");
    expect(searchBanks("acb")[0].shortName).toBe("ACB");
  });

  test("an empty query lists every bank and nonsense lists none", () => {
    expect(searchBanks("")).toHaveLength(banks.length);
    expect(searchBanks("   ")).toHaveLength(banks.length);
    expect(searchBanks("zzzzqq")).toEqual([]);
  });

  test("the account name becomes upper case without accents", () => {
    expect(normalizeAccountName("Nguyễn Văn Đạt")).toBe("NGUYEN VAN DAT");
    expect(normalizeAccountName("Lương Thị Ánh Tuyết")).toBe(
      "LUONG THI ANH TUYET",
    );
    expect(normalizeAccountName("Đỗ Hữu Ơn Ưu")).toBe("DO HUU ON UU");
  });

  test("typing keeps one trailing space so a second word can be typed, but never two spaces", () => {
    expect(normalizeAccountName("NGUYEN ")).toBe("NGUYEN ");
    expect(normalizeAccountName("NGUYEN   VAN")).toBe("NGUYEN VAN");
    expect(normalizeAccountName("  an")).toBe("AN");
  });

  test("characters the bank would reject are dropped and the length is capped at 50", () => {
    expect(normalizeAccountName("An 123 B-C.")).toBe("AN BC");
    expect(normalizeAccountName("A".repeat(80))).toHaveLength(50);
  });

  test("account numbers keep digits only, at most 19", () => {
    expect(digitsOnly("1234 5678-90")).toBe("1234567890");
    expect(digitsOnly("9".repeat(25))).toHaveLength(19);
  });

  test("client validation mirrors the server messages", () => {
    const ok = {
      bankBin: "970415",
      accountNo: "123456789",
      accountName: "NGUYEN VAN AN ",
    };
    expect(accountErrors(ok)).toEqual({});
    expect(accountErrors({ ...ok, bankBin: "" }).bankBin).toBe(
      "Chọn ngân hàng từ danh sách.",
    );
    expect(accountErrors({ ...ok, bankBin: "971005" }).bankBin).toBe(
      "Mã BIN ngân hàng phải gồm 6 chữ số, bắt đầu bằng 970.",
    );
    expect(accountErrors({ ...ok, accountNo: "12345" }).accountNo).toBe(
      "Số tài khoản phải gồm 6–19 chữ số.",
    );
    expect(accountErrors({ ...ok, accountName: "AN" }).accountName).toBe(
      "Tên người nhận gồm 5–50 chữ cái viết hoa không dấu và khoảng trắng.",
    );
  });
});
