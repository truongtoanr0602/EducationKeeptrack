import { useState } from "react";
import type { Me } from "./api";
import { classHref, href } from "./router";
import { useSwr } from "./swr";
import {
  cardHidden,
  hideCard,
  loadSetupProgress,
  setupDone,
  setupTotal,
} from "./setup-progress";

type Row = { text: string; label: string; to: string };

/**
 * "Hoàn tất thiết lập (n/3)" on the Today screen. Rows come from the teacher's real data (receiving account,
 * students, unit price), so work finished on another device shows up here without anything being stored.
 */
export function SetupCard({ me }: { me: Me }) {
  const [hidden, setHidden] = useState(() => cardHidden(me.email));
  const { data } = useSwr("setup:progress", loadSetupProgress);
  if (hidden || !data || data.classes.length === 0) return null;
  const done = setupDone(data);
  if (done === setupTotal) return null;
  const first = data.classes[0];
  const rows: Row[] = [];
  if (!data.recipient)
    rows.push({
      text: "Chưa có tài khoản nhận tiền",
      label: "Thêm tài khoản",
      to: href({ name: "setup", step: 2 }),
    });
  if (!data.students)
    rows.push({
      text: "Chưa có học sinh",
      label: "Dán danh sách học sinh",
      to: classHref(first.id, "students"),
    });
  if (!data.fee)
    rows.push({
      text: "Chưa đặt học phí",
      label: "Đặt học phí",
      to: classHref(data.feeClassId ?? first.id, "tuition"),
    });
  return (
    <section className="setup-card" aria-label="Hoàn tất thiết lập">
      <div className="setup-card-head">
        <h2>
          Hoàn tất thiết lập ({done}/{setupTotal})
        </h2>
        <button
          type="button"
          className="icon-button"
          aria-label="Ẩn thẻ thiết lập"
          onClick={() => {
            hideCard(me.email, true);
            setHidden(true);
          }}
        >
          ×
        </button>
      </div>
      <ul>
        {rows.map((row) => (
          <li key={row.text}>
            <span>{row.text}</span>
            <a className="button-link" href={row.to}>
              {row.label}
            </a>
          </li>
        ))}
      </ul>
    </section>
  );
}
