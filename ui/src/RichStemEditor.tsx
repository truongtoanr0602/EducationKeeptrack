import { useRef, useState } from "react";
import { Extension, Mark, Node, mergeAttributes } from "@tiptap/core";
import { EditorContent, useEditor } from "@tiptap/react";
import StarterKit from "@tiptap/starter-kit";
import { blankNumbers, colors, fonts, sizes, stemText } from "./rich-stem";
import "./rich-stem.css";

export type StemImage = { fileName: string; url: string };

// Which uploaded image has which signed link, so an image block can show itself. Keyed by lower-case file name.
const imageLinks = new Map<string, string>();

const classOf = (el: HTMLElement, ids: readonly { id: string }[]) =>
  ids.find((x) => el.classList.contains(x.id))?.id ?? null;

/** Font, size and colour of a run of text, written as classes from the fixed set instead of inline styles. */
const StemStyle = Mark.create({
  name: "stemStyle",
  addAttributes() {
    return {
      ff: { default: null },
      fs: { default: null },
      fc: { default: null },
    };
  },
  parseHTML() {
    return [
      {
        tag: "span",
        getAttrs: (el) => {
          const e = el as HTMLElement;
          const ff = classOf(e, fonts),
            fs = classOf(e, sizes),
            fc = classOf(e, colors);
          return ff || fs || fc ? { ff, fs, fc } : false;
        },
      },
    ];
  },
  renderHTML({ HTMLAttributes }) {
    const names = [HTMLAttributes.ff, HTMLAttributes.fs, HTMLAttributes.fc]
      .filter(Boolean)
      .join(" ");
    return ["span", { class: names }, 0];
  },
});

/** Alignment of a block, as ta-left / ta-center / ta-right. */
const StemAlign = Extension.create({
  name: "stemAlign",
  addGlobalAttributes() {
    return [
      {
        types: ["paragraph", "heading", "listItem"],
        attributes: {
          align: {
            default: null,
            parseHTML: (el: HTMLElement) =>
              ["ta-left", "ta-center", "ta-right"].find((c) =>
                el.classList.contains(c),
              ) ?? null,
            renderHTML: (attrs: Record<string, unknown>) =>
              attrs.align ? { class: String(attrs.align) } : {},
          },
        },
      },
    ];
  },
});

/** A numbered blank, stored as <span class="blank" data-blank="1">{{1}}</span>: the text keeps the marker, the answers live apart. */
const Blank = Node.create({
  name: "blank",
  group: "inline",
  inline: true,
  atom: true,
  addAttributes() {
    return { n: { default: 1 } };
  },
  parseHTML() {
    return [
      {
        tag: "span[data-blank]",
        getAttrs: (el) => ({
          n: Number((el as HTMLElement).getAttribute("data-blank")) || 1,
        }),
      },
    ];
  },
  renderHTML({ node }) {
    return [
      "span",
      mergeAttributes({ class: "blank", "data-blank": String(node.attrs.n) }),
      `{{${node.attrs.n}}}`,
    ];
  },
});

/** An uploaded image, stored by file name (data-file) and shown through its signed link. */
const StemImageNode = Node.create({
  name: "stemImage",
  group: "block",
  atom: true,
  draggable: true,
  addAttributes() {
    return { file: { default: "" }, alt: { default: "" } };
  },
  parseHTML() {
    return [
      {
        tag: "img[data-file]",
        getAttrs: (el) => ({
          file: (el as HTMLElement).getAttribute("data-file") ?? "",
          alt: (el as HTMLElement).getAttribute("alt") ?? "",
        }),
      },
    ];
  },
  renderHTML({ node }) {
    return ["img", { "data-file": node.attrs.file, alt: node.attrs.alt }];
  },
  addNodeView() {
    return ({ node }) => {
      const img = document.createElement("img");
      img.dataset.file = node.attrs.file;
      img.alt = node.attrs.alt || node.attrs.file;
      const link = imageLinks.get(String(node.attrs.file).toLowerCase());
      if (link) img.src = link;
      return { dom: img };
    };
  },
});

type Props = {
  value: string;
  onChange: (html: string) => void;
  images: StemImage[];
  /** Uploads a file to the class's images and returns its name and link. */
  uploadImage: (file: File) => Promise<StemImage>;
  label: string;
};

/**
 * The editor of a typed fill-in's stem (ADR 0008): bold, italic, underline, strike, four fonts, four sizes, six colours, alignment,
 * lists, headings, images and numbered blanks. It is a separate chunk, loaded only when this screen opens.
 */
export default function RichStemEditor({
  value,
  onChange,
  images,
  uploadImage,
  label,
}: Props) {
  const [popup, setPopup] = useState<"image" | null>(null);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const file = useRef<HTMLInputElement>(null);
  for (const x of images) imageLinks.set(x.fileName.toLowerCase(), x.url);
  const editor = useEditor({
    immediatelyRender: false,
    extensions: [
      StarterKit.configure({
        heading: { levels: [2, 3, 4] },
        blockquote: false,
        code: false,
        codeBlock: false,
        horizontalRule: false,
        link: false,
        dropcursor: false,
        gapcursor: false,
      }),
      StemStyle,
      StemAlign,
      Blank,
      StemImageNode,
    ],
    content: value,
    editorProps: {
      attributes: {
        "aria-label": label,
        role: "textbox",
        "aria-multiline": "true",
      },
    },
    onUpdate: ({ editor: e }) => onChange(e.isEmpty ? "" : e.getHTML()),
  });
  if (!editor) return <p role="status">Đang tải trình soạn thảo…</p>;

  const style = (key: "ff" | "fs" | "fc", id: string) => {
    const current = editor.getAttributes("stemStyle") as Record<
      string,
      string | null
    >;
    const next = {
      ff: current.ff ?? null,
      fs: current.fs ?? null,
      fc: current.fc ?? null,
      [key]: id || null,
    };
    if (!next.ff && !next.fs && !next.fc)
      editor.chain().focus().unsetMark("stemStyle").run();
    else editor.chain().focus().setMark("stemStyle", next).run();
  };
  const align = (value: string) =>
    editor
      .chain()
      .focus()
      .updateAttributes("paragraph", { align: value })
      .updateAttributes("heading", { align: value })
      .updateAttributes("listItem", { align: value })
      .run();
  const used = blankNumbers(stemText(editor.getHTML()));
  const insertBlank = () => {
    if (used.length >= 10) return setError("Tối đa 10 chỗ trống.");
    setError("");
    editor
      .chain()
      .focus()
      .insertContent({
        type: "blank",
        attrs: { n: (used.length ? Math.max(...used) : 0) + 1 },
      })
      .run();
  };
  const insertImage = (name: string) => {
    editor
      .chain()
      .focus()
      .insertContent({ type: "stemImage", attrs: { file: name, alt: "" } })
      .run();
    setPopup(null);
  };
  async function send(f: File | undefined) {
    if (!f) return;
    setBusy(true);
    setError("");
    try {
      const saved = await uploadImage(f);
      imageLinks.set(saved.fileName.toLowerCase(), saved.url);
      insertImage(saved.fileName);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Không tải được ảnh.");
    } finally {
      setBusy(false);
      if (file.current) file.current.value = "";
    }
  }
  const active = (name: string, attrs?: Record<string, unknown>) =>
    editor.isActive(name, attrs);
  const btn = (title: string, on: boolean, run: () => void, text: string) => (
    <button
      type="button"
      title={title}
      aria-label={title}
      aria-pressed={on}
      onClick={run}
    >
      {text}
    </button>
  );
  const current = editor.getAttributes("stemStyle") as Record<
    string,
    string | null
  >;
  return (
    <div className="rich-editor">
      <div
        className="rich-editor-bar"
        role="toolbar"
        aria-label="Định dạng đề bài"
      >
        <select
          aria-label="Font chữ"
          value={current.ff ?? ""}
          onChange={(e) => style("ff", e.target.value)}
        >
          <option value="">Font</option>
          {fonts.map((x) => (
            <option key={x.id} value={x.id}>
              {x.label}
            </option>
          ))}
        </select>
        <select
          aria-label="Cỡ chữ"
          value={current.fs ?? ""}
          onChange={(e) => style("fs", e.target.value)}
        >
          <option value="">Cỡ chữ</option>
          {sizes.map((x) => (
            <option key={x.id} value={x.id}>
              {x.label}
            </option>
          ))}
        </select>
        <select
          aria-label="Màu chữ"
          value={current.fc ?? ""}
          onChange={(e) => style("fc", e.target.value)}
        >
          <option value="">Màu</option>
          {colors.map((x) => (
            <option key={x.id} value={x.id}>
              {x.label}
            </option>
          ))}
        </select>
        <span className="sep" />
        {btn(
          "In đậm",
          active("bold"),
          () => editor.chain().focus().toggleBold().run(),
          "B",
        )}
        {btn(
          "In nghiêng",
          active("italic"),
          () => editor.chain().focus().toggleItalic().run(),
          "I",
        )}
        {btn(
          "Gạch chân",
          active("underline"),
          () => editor.chain().focus().toggleUnderline().run(),
          "U",
        )}
        {btn(
          "Gạch ngang",
          active("strike"),
          () => editor.chain().focus().toggleStrike().run(),
          "S",
        )}
        <span className="sep" />
        {btn(
          "Tiêu đề lớn",
          active("heading", { level: 2 }),
          () => editor.chain().focus().toggleHeading({ level: 2 }).run(),
          "H",
        )}
        {btn(
          "Danh sách dấu chấm",
          active("bulletList"),
          () => editor.chain().focus().toggleBulletList().run(),
          "• •",
        )}
        {btn(
          "Danh sách số",
          active("orderedList"),
          () => editor.chain().focus().toggleOrderedList().run(),
          "1.",
        )}
        <span className="sep" />
        {btn("Căn trái", false, () => align("ta-left"), "⇤")}
        {btn("Căn giữa", false, () => align("ta-center"), "↔")}
        {btn("Căn phải", false, () => align("ta-right"), "⇥")}
        <span className="sep" />
        {btn("Chèn chỗ trống", false, insertBlank, "＿ Chỗ trống")}
        {btn(
          "Chèn ảnh",
          popup === "image",
          () => setPopup(popup === "image" ? null : "image"),
          "Ảnh",
        )}
      </div>
      {popup === "image" && (
        <div className="rich-editor-pop" role="group" aria-label="Chọn ảnh">
          <label>
            Tải ảnh mới
            <input
              ref={file}
              type="file"
              accept="image/png,image/jpeg,image/webp,image/gif"
              disabled={busy}
              onChange={(e) => void send(e.target.files?.[0])}
            />
          </label>
          {images.length > 0 && (
            <ul aria-label="Ảnh đã tải lên">
              {images.map((x) => (
                <li key={x.fileName}>
                  <button type="button" onClick={() => insertImage(x.fileName)}>
                    <img src={x.url} alt="" />
                    {x.fileName}
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
      {error && (
        <p className="field-error" role="alert">
          {error}
        </p>
      )}
      <div className="rich-editor-body rich-stem">
        <EditorContent editor={editor} />
      </div>
    </div>
  );
}
