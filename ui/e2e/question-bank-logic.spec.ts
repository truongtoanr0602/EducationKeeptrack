import { test, expect } from "@playwright/test";
import { answerText, levelLabel, skillBand, stemParts } from "../src/bank-api";

test.describe("question bank helpers (no browser, no server)", () => {
  test("a fill-in stem splits at every [ ] blank, spaces inside the brackets allowed", () => {
    expect(stemParts("8 + 5 = [ ]")).toEqual(["8 + 5 = ", ""]);
    expect(stemParts("[] chục [  ] đơn vị")).toEqual(["", " chục ", " đơn vị"]);
    expect(stemParts("Không có ô")).toEqual(["Không có ô"]);
  });

  test("the answer key reads as the option text or the blanks in order", () => {
    const options = [{ key: "A", text: "46" }, { key: "B", text: "64" }];
    expect(answerText({ type: "single_choice", answer: [["B"]], options })).toBe("B. 64");
    expect(answerText({ type: "fill_blanks", answer: [["6"], ["4", "bốn"]], options: [] })).toBe("6 | 4 / bốn");
  });

  test("skill bands: strong from 80%, weak below 50%", () => {
    expect(skillBand(4, 5)).toBe("strong");
    expect(skillBand(1, 2)).toBe("mid");
    expect(skillBand(0, 2)).toBe("weak");
    expect(skillBand(0, 0)).toBe("mid");
    expect(levelLabel("van_dung_cao")).toBe("Vận dụng cao");
  });
});
