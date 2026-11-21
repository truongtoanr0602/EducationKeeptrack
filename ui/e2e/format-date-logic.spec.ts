import { test, expect } from "@playwright/test";
import { formatDate, formatMonth } from "../src/format-date";

test("a calendar date reads day/month/year", () => {
  expect(formatDate("2026-11-10")).toBe("10/11/2026");
  expect(formatDate("2026-01-05")).toBe("05/01/2026");
});

test("a month reads month/year", () => {
  expect(formatMonth("2026-11")).toBe("11/2026");
});

test("anything else comes back unchanged, and a missing value is empty", () => {
  expect(formatDate("2026-11-10T08:00:00Z")).toBe("2026-11-10T08:00:00Z");
  expect(formatDate("Hôm nay")).toBe("Hôm nay");
  expect(formatMonth("2026-11-10")).toBe("2026-11-10");
  expect(formatDate(null)).toBe("");
  expect(formatDate(undefined)).toBe("");
  expect(formatMonth("")).toBe("");
});
