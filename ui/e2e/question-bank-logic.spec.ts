import { test, expect } from "@playwright/test";
import { answerText, levelLabel, skillBand, stemParts } from "../src/bank-api";
import { href, parse } from "../src/router";

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

  test("program screens have their own always-shown section and old personalisation links follow them", () => {
    const id = "0b6c0a3e-2f5d-4c1e-9a77-3f2d1c0b9e11";
    expect(parse(`#/classes/${id}/program/exams`)).toEqual({ name: "class", classId: id, section: "program", tab: "exams" });
    expect(parse(`#/classes/${id}/program`)).toEqual({ name: "class", classId: id, section: "program", tab: undefined });
    expect(parse(`#/classes/${id}/personalization/curriculum`)).toEqual({ name: "class", classId: id, section: "program", tab: "curriculum" });
    expect(parse(`#/classes/${id}/personalization/competency`).section).toBe("program");
    expect(parse(`#/classes/${id}/personalization/plans`)).toEqual({ name: "class", classId: id, section: "personalization", tab: "plans" });
    expect(parse(`#/classes/${id}/personalization`).tab).toBe("materials");
    expect(href({ name: "class", classId: id, section: "program", tab: "questions" })).toBe(`#/classes/${id}/program/questions`);
  });
});
