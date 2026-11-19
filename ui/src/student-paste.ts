// Parsing of a pasted student list: one student per line, "Nguyễn An" or "Nguyễn An, 0901234567" (a comma or a Tab
// from a spreadsheet separates the optional parent's phone). Pure functions, so every rule is tested without a page.

export const MAX_ROWS = 100;

export type PasteRow = {
  /** 1-based line in the pasted text, so messages match what the teacher sees. */
  line: number;
  /** The line as pasted, so a line that needs fixing goes back to the box unchanged. */
  raw: string;
  name: string;
  phone: string;
  /** Why the row cannot be saved, or null. */
  error: string | null;
  /** Things worth a second look that never block saving. */
  warnings: string[];
};
export type KnownStudent = { name: string; enrolled: boolean };

// Written as strings so that no formatter turns the invisible characters into raw ones.
const COMBINING = new RegExp("[\\u0300-\\u036f]", "g");
const INVISIBLE = new RegExp("[\\u200b-\\u200d\\u2060\\ufeff]", "g");
const NARROW_SPACES = new RegExp("[\\u00a0\\u2007\\u202f]", "g");
// "1.", "2)", "3 ", "10.", "- ", "• " in front of a name. Digits need a separator after them, so a phone number
// alone on a line is not read as a list number.
const MARKER = new RegExp(
  "^\\s*(?:\\d{1,3}(?:\\s*[.):\\-]\\s*|\\s+)|[-–—•*·]\\s*)",
);

const collapse = (text: string) => text.replace(/\s+/g, " ").trim();

/** Lower case, no accents, single spaces: how two names are compared. */
export const foldName = (name: string) =>
  collapse(
    name
      .normalize("NFD")
      .replace(COMBINING, "")
      .replace(/[đĐ]/g, "d")
      .toLowerCase(),
  );

/** Digits only, to compare phone numbers typed with different spacing. */
export const digits = (phone: string) => phone.replace(/\D/g, "");

// A sanity layer in front of the server's own check: brackets, plus, dots, dashes and spaces are fine, and a number
// needs at least six digits. It must never reject something the server accepts, such as "(028) 3822 1234".
const phoneOk = (phone: string) =>
  phone.length <= 30 &&
  /^[0-9 ().+-]+$/.test(phone) &&
  digits(phone).length >= 6;

export function parsePaste(
  text: string,
  known: KnownStudent[] = [],
): { rows: PasteRow[]; overLimit: boolean } {
  const lines = text
    .replace(INVISIBLE, "")
    .replace(NARROW_SPACES, " ")
    .split(/\r\n|\r|\n/);
  const firstSeen = new Map<string, number>();
  const rows: PasteRow[] = [];
  lines.forEach((raw, index) => {
    if (!raw.trim()) return;
    const body = raw.replace(MARKER, "");
    const fields = body.includes("\t") ? body.split("\t") : body.split(",");
    while (fields.length > 1 && fields[fields.length - 1].trim() === "")
      fields.pop();
    const name = collapse(fields[0] ?? "");
    const phone = (fields[1] ?? "").trim();
    let error: string | null = null;
    if (fields.length > 2)
      error =
        "Mỗi dòng gồm tên và (tùy chọn) số điện thoại, ngăn cách bằng dấu phẩy hoặc Tab.";
    else if (!/\p{L}/u.test(name)) error = "Dòng này chưa có tên học sinh.";
    else if (name.length > 120) error = "Tên học sinh tối đa 120 ký tự.";
    else if (phone && !phoneOk(phone)) error = "Số điện thoại không hợp lệ.";

    const warnings: string[] = [];
    const folded = foldName(name);
    if (!error && folded) {
      const earlier = firstSeen.get(folded);
      if (earlier === undefined) firstSeen.set(folded, index + 1);
      else
        warnings.push(`Trùng tên với dòng ${earlier} trong danh sách vừa dán.`);
      const same = known.filter((k) => foldName(k.name) === folded);
      if (same.some((k) => k.enrolled))
        warnings.push("Học sinh tên này đã học trong lớp này.");
      else if (same.length)
        warnings.push(
          "Đã có học sinh tên này trong danh sách của bạn (có thể đang học lớp khác).",
        );
    }
    rows.push({
      line: index + 1,
      raw: raw.trim(),
      name,
      phone,
      error,
      warnings,
    });
  });
  return { rows, overLimit: rows.length > MAX_ROWS };
}
