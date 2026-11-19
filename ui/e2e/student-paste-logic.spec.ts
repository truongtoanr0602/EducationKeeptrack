import { test, expect } from "@playwright/test";
import { MAX_ROWS, foldName, parsePaste } from "../src/student-paste";

const names = (text: string) => parsePaste(text).rows.map((r) => r.name);

test.describe("pasted student list: parsing (no browser, no server)", () => {
  test("one student per line, with an optional phone after a comma", () => {
    const { rows } = parsePaste(
      "Nguyễn An\nTrần Bình, 0901234567\nLê Chi,0912 345 678",
    );
    expect(rows.map((r) => [r.name, r.phone, r.error])).toEqual([
      ["Nguyễn An", "", null],
      ["Trần Bình", "0901234567", null],
      ["Lê Chi", "0912 345 678", null],
    ]);
  });

  test("a spreadsheet paste separates columns with Tab", () => {
    const { rows } = parsePaste("Nguyễn An\t0901234567\nTrần Bình\t");
    expect(rows.map((r) => [r.name, r.phone])).toEqual([
      ["Nguyễn An", "0901234567"],
      ["Trần Bình", ""],
    ]);
  });

  test("list numbers and bullets in front of a name are dropped", () => {
    expect(
      names(
        "1. Nguyễn An\n2) Trần Bình\n3 Lê Chi\n- Phạm Dũng\n• Hoàng Em\n10.Vũ Phúc",
      ),
    ).toEqual([
      "Nguyễn An",
      "Trần Bình",
      "Lê Chi",
      "Phạm Dũng",
      "Hoàng Em",
      "Vũ Phúc",
    ]);
  });

  test("a phone number alone on a line is not mistaken for a list number", () => {
    const [row] = parsePaste("0901234567").rows;
    expect(row.error).toBe("Dòng này chưa có tên học sinh.");
  });

  test("blank lines are skipped but line numbers still match what the teacher sees", () => {
    const { rows } = parsePaste("An Nguyễn\n\n   \nBình Trần");
    expect(rows.map((r) => r.line)).toEqual([1, 4]);
  });

  test("windows line endings, non-breaking spaces and zero-width characters from chat apps are cleaned", () => {
    const { rows } = parsePaste("Nguyễn An\r\nTrần​ Bình​\r\nLê   Chi");
    expect(rows.map((r) => r.name)).toEqual([
      "Nguyễn An",
      "Trần Bình",
      "Lê Chi",
    ]);
  });

  test("a line without a name, with a name that is too long, or with a bad phone is an error on that line only", () => {
    const { rows } = parsePaste(
      `, 0901234567\n${"A".repeat(121)}\nLê Chi, abc\nPhạm Dũng, 0901234567`,
    );
    expect(rows[0].error).toBe("Dòng này chưa có tên học sinh.");
    expect(rows[1].error).toBe("Tên học sinh tối đa 120 ký tự.");
    expect(rows[2].error).toBe("Số điện thoại không hợp lệ.");
    expect(rows[3].error).toBeNull();
  });

  test("phone numbers the server accepts are not rejected: brackets, plus sign and spaces", () => {
    const phones = [
      "(028) 3822 1234",
      "+84 901 234 567",
      "090.123.4567",
      "090-123-4567",
    ];
    const { rows } = parsePaste(
      phones.map((p, i) => `HS ${i}, ${p}`).join("\n"),
    );
    expect(rows.map((r) => r.error)).toEqual([null, null, null, null]);
  });

  test("more than a name and a phone on a line is refused with an explanation", () => {
    const [row] = parsePaste("Nguyễn An, 0901234567, lớp 9A").rows;
    expect(row.error).toBe(
      "Mỗi dòng gồm tên và (tùy chọn) số điện thoại, ngăn cách bằng dấu phẩy hoặc Tab.",
    );
  });

  test("the same name twice in one paste is a warning on the later line, never an error", () => {
    const { rows } = parsePaste("Nguyễn An\nTrần Bình\nnguyen an");
    expect(rows[2].error).toBeNull();
    expect(rows[2].warnings).toEqual([
      "Trùng tên với dòng 1 trong danh sách vừa dán.",
    ]);
    expect(rows[0].warnings).toEqual([]);
  });

  test("a name that already exists is a warning, and one already in this class is a stronger one", () => {
    const { rows } = parsePaste("Nguyễn An\nTrần Bình\nLê Chi", [
      { name: "nguyễn an", enrolled: false },
      { name: "Trần Bình", enrolled: true },
    ]);
    expect(rows[0].warnings).toEqual([
      "Đã có học sinh tên này trong danh sách của bạn (có thể đang học lớp khác).",
    ]);
    expect(rows[1].warnings).toEqual([
      "Học sinh tên này đã học trong lớp này.",
    ]);
    expect(rows[2].warnings).toEqual([]);
  });

  test("names are compared without accents and case", () => {
    expect(foldName("  Nguyễn  Đức   An ")).toBe(foldName("nguyen duc an"));
  });

  test("more rows than the limit is reported", () => {
    const many = Array.from(
      { length: MAX_ROWS + 1 },
      (_, i) => `Học sinh ${String.fromCharCode(65 + (i % 26))}${i}`,
    ).join("\n");
    expect(parsePaste(many).overLimit).toBe(true);
    expect(
      parsePaste(many.split("\n").slice(0, MAX_ROWS).join("\n")).overLimit,
    ).toBe(false);
  });
});
