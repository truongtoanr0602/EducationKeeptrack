import { test, expect } from "@playwright/test";
import { answerText, levelLabel, skillBand, stemParts } from "../src/bank-api";
import { classHref, href, parse, sections } from "../src/router";

test.describe("question bank helpers (no browser, no server)", () => {
  test("a fill-in stem splits at every [ ] blank, spaces inside the brackets allowed", () => {
    expect(stemParts("8 + 5 = [ ]")).toEqual(["8 + 5 = ", ""]);
    expect(stemParts("[] chục [  ] đơn vị")).toEqual(["", " chục ", " đơn vị"]);
    expect(stemParts("Không có ô")).toEqual(["Không có ô"]);
  });

  test("the answer key reads as the option text or the blanks in order", () => {
    const options = [
      { key: "A", text: "46" },
      { key: "B", text: "64" },
    ];
    expect(
      answerText({ type: "single_choice", answer: [["B"]], options }),
    ).toBe("B. 64");
    expect(
      answerText({
        type: "fill_blanks",
        answer: [["6"], ["4", "bốn"]],
        options: [],
      }),
    ).toBe("6 | 4 / bốn");
  });

  test("skill bands: strong from 80%, weak below 50%", () => {
    expect(skillBand(4, 5)).toBe("strong");
    expect(skillBand(1, 2)).toBe("mid");
    expect(skillBand(0, 2)).toBe("weak");
    expect(skillBand(0, 0)).toBe("mid");
    expect(levelLabel("van_dung_cao")).toBe("Vận dụng cao");
  });

  test("class sections are five, and every old link opens the same screen under Học tập", () => {
    const id = "0b6c0a3e-2f5d-4c1e-9a77-3f2d1c0b9e11";
    const learning = (area: string, tab?: string) => ({
      name: "class",
      classId: id,
      section: "learning",
      area,
      tab,
    });
    expect(parse(`#/classes/${id}/learning/program/exams`)).toEqual(
      learning("program", "exams"),
    );
    expect(parse(`#/classes/${id}/learning`)).toEqual(
      learning("assessments", undefined),
    );
    expect(parse(`#/classes/${id}/learning/personalization`)).toEqual(
      learning("personalization", "materials"),
    );
    // Old links.
    expect(parse(`#/classes/${id}/assessments`)).toEqual(
      learning("assessments", undefined),
    );
    expect(parse(`#/classes/${id}/program/exams`)).toEqual(
      learning("program", "exams"),
    );
    expect(parse(`#/classes/${id}/program`)).toEqual(
      learning("program", undefined),
    );
    expect(parse(`#/classes/${id}/personalization/curriculum`)).toEqual(
      learning("program", "curriculum"),
    );
    expect(parse(`#/classes/${id}/personalization/competency`).area).toBe(
      "program",
    );
    expect(parse(`#/classes/${id}/personalization/plans`)).toEqual(
      learning("personalization", "plans"),
    );
    expect(parse(`#/classes/${id}/personalization`).tab).toBe("materials");
    expect(parse(`#/classes/${id}/tuition`)).toEqual({
      name: "class",
      classId: id,
      section: "tuition",
    });
    expect(href(learning("program", "questions") as never)).toBe(
      `#/classes/${id}/learning/program/questions`,
    );
    expect(classHref(id, "assessments")).toBe(
      `#/classes/${id}/learning/assessments`,
    );
    expect(classHref(id, "program", "lessons")).toBe(
      `#/classes/${id}/learning/program/lessons`,
    );
    expect(classHref(id, "tuition")).toBe(`#/classes/${id}/tuition`);
    expect(sections.map((x) => x.label)).toEqual([
      "Học sinh",
      "Lịch",
      "Học phí",
      "Học tập",
      "Báo cáo",
    ]);
  });
});
