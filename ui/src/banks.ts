import { banks, type Bank } from "./bank-list";

export type { Bank };

// Accents that NFD splits off a letter (the range is written as an escaped string so no tool rewrites it).
const COMBINING_MARKS = new RegExp("[\\u0300-\\u036f]", "g");

// Pure helpers for the receiving-account form. The server accepts only a 970xxx BIN, an account number of 6-19
// digits and a name of 5-50 capital letters and spaces; the same rules run here so mistakes show while typing.

/** Lower case without accents, so "ngoai thuong" finds "Ngoại Thương". */
export const fold = (text: string) =>
  text
    .normalize("NFD")
    .replace(COMBINING_MARKS, "")
    .replace(/[đĐ]/g, "d")
    .toLowerCase();

export const findBank = (bin: string): Bank | undefined =>
  banks.find((bank) => bank.bin === bin);

/**
 * Banks matching the text, best first: exact code or name, then a name that starts with it, then a short name or
 * code that contains it, then the full name. Ties go to the earlier match, then to alphabetical order, so
 * "ngoai thuong" lists Vietcombank before CBBank (whose long name only mentions it near the end).
 */
export function searchBanks(query: string): Bank[] {
  const q = fold(query).trim();
  if (!q) return banks;
  const rank = (bank: Bank): [number, number] => {
    const short = fold(bank.shortName);
    const code = fold(bank.code);
    const name = fold(bank.name);
    if (short === q || code === q) return [0, 0];
    if (short.startsWith(q) || code.startsWith(q)) return [1, 0];
    if (short.includes(q) || code.includes(q)) return [2, 0];
    const at = name.indexOf(q);
    return at >= 0 ? [3, at] : [-1, 0];
  };
  return banks
    .map((bank) => ({ bank, rank: rank(bank) }))
    .filter((item) => item.rank[0] >= 0)
    .sort(
      (a, b) =>
        a.rank[0] - b.rank[0] ||
        a.rank[1] - b.rank[1] ||
        a.bank.shortName.localeCompare(b.bank.shortName),
    )
    .map((item) => item.bank);
}

/**
 * Upper case without accents, letters and single spaces only, at most 50 characters. One trailing space is kept so
 * a second word can still be typed; the server trims the saved value.
 */
export const normalizeAccountName = (raw: string) =>
  raw
    .normalize("NFD")
    .replace(COMBINING_MARKS, "")
    .replace(/[đĐ]/g, "D")
    .toUpperCase()
    .replace(/[^A-Z ]/g, "")
    .replace(/ {2,}/g, " ")
    .replace(/^ /, "")
    .slice(0, 50);

export const digitsOnly = (raw: string) => raw.replace(/\D/g, "").slice(0, 19);

export function accountErrors(account: {
  bankBin: string;
  accountNo: string;
  accountName: string;
}): Record<string, string> {
  const errors: Record<string, string> = {};
  if (!account.bankBin) errors.bankBin = "Chọn ngân hàng từ danh sách.";
  else if (!/^970[0-9]{3}$/.test(account.bankBin))
    errors.bankBin = "Mã BIN ngân hàng phải gồm 6 chữ số, bắt đầu bằng 970.";
  if (!/^[0-9]{6,19}$/.test(account.accountNo))
    errors.accountNo = "Số tài khoản phải gồm 6–19 chữ số.";
  if (!/^[A-Z ]{5,50}$/.test(account.accountName.trim()))
    errors.accountName =
      "Tên người nhận gồm 5–50 chữ cái viết hoa không dấu và khoảng trắng.";
  return errors;
}
