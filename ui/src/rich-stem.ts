// The rich stem of a typed fill-in (ADR 0008). The server already cleans it; the browser cleans it again before showing it, with the
// same short allow-list (src/Modules/Learning/Domain/RichStem.cs), so a stem is never trusted just because it came from the API.

const TAGS = new Set([
  "P",
  "BR",
  "STRONG",
  "EM",
  "U",
  "S",
  "UL",
  "OL",
  "LI",
  "SPAN",
  "H2",
  "H3",
  "H4",
  "IMG",
]);
export const fonts = [
  { id: "ff-sans", label: "Không chân" },
  { id: "ff-serif", label: "Có chân" },
  { id: "ff-hand", label: "Chữ viết tay" },
  { id: "ff-mono", label: "Chữ đều" },
] as const;
export const sizes = [
  { id: "fs-s", label: "Nhỏ" },
  { id: "fs-m", label: "Vừa" },
  { id: "fs-l", label: "Lớn" },
  { id: "fs-xl", label: "Rất lớn" },
] as const;
export const colors = [
  { id: "fc-1", label: "Đen", value: "#111827" },
  { id: "fc-2", label: "Đỏ", value: "#b91c1c" },
  { id: "fc-3", label: "Xanh dương", value: "#1d4ed8" },
  { id: "fc-4", label: "Xanh lá", value: "#15803d" },
  { id: "fc-5", label: "Tím", value: "#7e22ce" },
  { id: "fc-6", label: "Cam", value: "#c2410c" },
] as const;
const CLASSES = new Set([
  ...fonts.map((x) => x.id),
  ...sizes.map((x) => x.id),
  ...colors.map((x) => x.id),
  "ta-left",
  "ta-center",
  "ta-right",
  "blank",
]);
const IMAGE_LINK = "/api/learning/question-images/";

/** The stem reduced to the allowed tags, classes and attributes; an image keeps its src only when it is a signed question-image link. */
export function sanitizeStem(html: string | null | undefined): string {
  if (!html) return "";
  const doc = new DOMParser().parseFromString(
    `<body>${html}</body>`,
    "text/html",
  );
  const walk = (parent: Element) => {
    for (const node of [...parent.childNodes]) {
      if (node.nodeType === Node.TEXT_NODE) continue;
      if (node.nodeType !== Node.ELEMENT_NODE) {
        node.remove();
        continue;
      }
      const el = node as Element;
      if (!TAGS.has(el.tagName)) {
        el.remove();
        continue;
      }
      for (const attr of [...el.attributes]) {
        const name = attr.name.toLowerCase();
        if (name === "class") {
          const kept = [...el.classList].filter((c) => CLASSES.has(c));
          if (kept.length > 0) el.setAttribute("class", kept.join(" "));
          else el.removeAttribute("class");
        } else if (name === "data-blank" && /^\d{1,2}$/.test(attr.value))
          continue;
        else if (name === "data-file" || name === "alt") continue;
        else if (
          name === "src" &&
          el.tagName === "IMG" &&
          attr.value.startsWith(IMAGE_LINK)
        )
          continue;
        else el.removeAttribute(attr.name);
      }
      if (
        el.tagName === "IMG" &&
        !el.getAttribute("src") &&
        !el.getAttribute("data-file")
      )
        el.remove();
      else walk(el);
    }
  };
  walk(doc.body);
  return doc.body.innerHTML;
}

/** Numbers of the blanks ({{1}}, {{2}}…) written in a stem's text, in the order they appear. */
export function blankNumbers(text: string): number[] {
  return [...text.matchAll(/\{\{(\d+)\}\}/g)].map((m) => Number(m[1]));
}

/** The text of a stem with blank markers kept, for counting blanks and for a plain preview. */
export function stemText(html: string): string {
  const doc = new DOMParser().parseFromString(
    `<body>${html}</body>`,
    "text/html",
  );
  return doc.body.textContent ?? "";
}

/** Replaces each blank marker of a stem with an element made by `make(n)`; a stem rendered this way gets real inputs in place of {{n}}. */
export function fillBlanks(
  root: HTMLElement,
  make: (n: number) => HTMLElement,
) {
  for (const el of root.querySelectorAll<HTMLElement>("span[data-blank]"))
    el.replaceWith(make(Number(el.dataset.blank)));
  // Text that still holds {{n}} (a stem typed as plain text) is split around the marker.
  const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
  const texts: Text[] = [];
  for (let n = walker.nextNode(); n; n = walker.nextNode())
    if (/\{\{\d+\}\}/.test(n.textContent ?? "")) texts.push(n as Text);
  for (const text of texts) {
    const fragment = document.createDocumentFragment();
    let last = 0;
    for (const m of (text.textContent ?? "").matchAll(/\{\{(\d+)\}\}/g)) {
      fragment.append((text.textContent ?? "").slice(last, m.index));
      fragment.append(make(Number(m[1])));
      last = (m.index ?? 0) + m[0].length;
    }
    fragment.append((text.textContent ?? "").slice(last));
    text.replaceWith(fragment);
  }
}

/** Adds a signed link to each image that names a file (data-file) but has no src yet; an image whose file has no link is dropped. */
export function withImageLinks(
  html: string | null | undefined,
  links: ReadonlyMap<string, string>,
): string {
  if (!html) return "";
  const doc = new DOMParser().parseFromString(
    `<body>${html}</body>`,
    "text/html",
  );
  for (const img of doc.querySelectorAll("img[data-file]")) {
    if (img.getAttribute("src")) continue;
    const link = links.get((img.getAttribute("data-file") ?? "").toLowerCase());
    if (link) img.setAttribute("src", link);
    else img.remove();
  }
  return doc.body.innerHTML;
}
