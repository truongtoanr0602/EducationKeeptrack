// The API and the date inputs speak ISO (2026-11-10, 2026-11); a Vietnamese reader expects 10/11/2026 and 11/2026.
// Only plain calendar dates and months are rewritten: anything else comes back as it was, so a missing value or a
// date-time string is never mangled.

const date = /^(\d{4})-(\d{2})-(\d{2})$/;
const month = /^(\d{4})-(\d{2})$/;

export function formatDate(value: string | null | undefined): string {
  if (!value) return "";
  const match = date.exec(value);
  return match ? `${match[3]}/${match[2]}/${match[1]}` : value;
}

export function formatMonth(value: string | null | undefined): string {
  if (!value) return "";
  const match = month.exec(value);
  return match ? `${match[2]}/${match[1]}` : value;
}
