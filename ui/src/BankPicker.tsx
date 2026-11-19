import { useId, useState, type KeyboardEvent } from "react";
import { findBank, searchBanks, type Bank } from "./banks";

type Props = {
  /** BIN of the chosen bank, "" while none is chosen. */
  bin: string;
  disabled?: boolean;
  invalid?: boolean;
  describedBy?: string;
  onPick: (bank: Bank) => void;
  /** The text no longer names the chosen bank, so the choice is cleared. */
  onClear: () => void;
  /** "Ngân hàng khác": the teacher types a BIN and a name by hand. */
  onOther: () => void;
};

const OTHER = "other";

/** A bank is chosen from the list by typing part of its name, never by typing a BIN. */
export function BankPicker({
  bin,
  disabled,
  invalid,
  describedBy,
  onPick,
  onClear,
  onOther,
}: Props) {
  const id = useId();
  const chosen = findBank(bin);
  const [text, setText] = useState<string | null>(null);
  const [open, setOpen] = useState(false);
  const [active, setActive] = useState(0);
  // While the teacher is typing the field shows their text; otherwise it shows the chosen bank.
  const shown = text ?? chosen?.shortName ?? "";
  const results = searchBanks(text ?? "");
  const options = [...results.map((bank) => bank.bin), OTHER];
  const optionId = (key: string) => `${id}-${key}`;

  function choose(key: string) {
    setOpen(false);
    setText(null);
    if (key === OTHER) onOther();
    else {
      const bank = findBank(key);
      if (bank) onPick(bank);
    }
  }
  function onKey(event: KeyboardEvent<HTMLInputElement>) {
    if (event.key === "ArrowDown" || event.key === "ArrowUp") {
      event.preventDefault();
      setOpen(true);
      const step = event.key === "ArrowDown" ? 1 : -1;
      setActive(
        (current) => (current + step + options.length) % options.length,
      );
    } else if (event.key === "Enter" && open) {
      // Pick the highlighted bank instead of submitting the form.
      event.preventDefault();
      choose(options[Math.min(active, options.length - 1)]);
    } else if (event.key === "Escape" && open) {
      event.preventDefault();
      setOpen(false);
      setText(null);
    }
  }

  return (
    <div className="bank-picker">
      <label htmlFor={`${id}-input`}>Ngân hàng</label>
      <input
        id={`${id}-input`}
        role="combobox"
        aria-expanded={open}
        aria-controls={`${id}-list`}
        aria-autocomplete="list"
        aria-activedescendant={
          open ? optionId(options[active] ?? OTHER) : undefined
        }
        aria-invalid={invalid || undefined}
        aria-describedby={describedBy}
        autoComplete="off"
        placeholder="Gõ tên hoặc mã ngân hàng, ví dụ Vietin, VCB"
        disabled={disabled}
        value={shown}
        onFocus={() => setOpen(true)}
        onBlur={() => {
          setOpen(false);
          setText(null);
        }}
        onChange={(event) => {
          setText(event.target.value);
          setActive(0);
          setOpen(true);
          onClear();
        }}
        onKeyDown={onKey}
      />
      {open && (
        <ul id={`${id}-list`} role="listbox" aria-label="Danh sách ngân hàng">
          {results.map((bank, index) => (
            <li
              key={bank.bin}
              id={optionId(bank.bin)}
              role="option"
              aria-selected={index === active}
              className={index === active ? "active" : undefined}
              // mousedown, not click: the input's blur would close the list before a click lands.
              onMouseDown={(event) => {
                event.preventDefault();
                choose(bank.bin);
              }}
              onMouseEnter={() => setActive(index)}
            >
              <strong>{bank.shortName}</strong>
              <span>{bank.name}</span>
            </li>
          ))}
          <li
            id={optionId(OTHER)}
            role="option"
            aria-selected={active === results.length}
            className={active === results.length ? "active other" : "other"}
            onMouseDown={(event) => {
              event.preventDefault();
              choose(OTHER);
            }}
            onMouseEnter={() => setActive(results.length)}
          >
            <strong>Ngân hàng khác</strong>
            <span>Không có trong danh sách: nhập mã BIN bằng tay</span>
          </li>
        </ul>
      )}
    </div>
  );
}
