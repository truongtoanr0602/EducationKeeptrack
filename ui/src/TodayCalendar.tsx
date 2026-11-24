import { useEffect, useRef, useState, type KeyboardEvent } from "react";
import "./calendar.css";
import { BulkCreate } from "./BulkCreate";
import {
  attention,
  bulkPlan,
  classColor,
  dayLabel,
  ddmm,
  groupByDay,
  isMissing,
  isOverdue,
  loadRange,
  monthDays,
  needsAttention,
  nameOf,
  classOf,
  shiftMonth,
  startOf,
  weekDays,
  type DayEntry,
  type RangeView,
} from "./calendar-api";
import { prefetch, useSwr } from "./swr";
import { shiftDate } from "./today-api";

const hhmm = (t: string) => t.slice(0, 5);
const dows = ["T2", "T3", "T4", "T5", "T6", "T7", "CN"];
const MAX_DOTS = 4;

/**
 * Read-only week and month overview for the Today screen. Picking a day only changes the selected date; the session
 * list under it (and its attendance flow) stays in Today. Colour tells classes apart, never the status: dots differ in
 * shape (dashed ring = not created yet, square = needs attention) and every day cell has a full text label.
 */
export default function TodayCalendar({
  selected,
  today,
  onSelect,
  refreshKey,
  canWrite,
  onCreated,
}: {
  selected: string;
  today: string;
  onSelect: (date: string) => void;
  /** Changes whenever Today saved something, so the overview refetches. */
  refreshKey: number;
  canWrite: boolean;
  /** Sessions were created from the overview: refresh the day list. */
  onCreated: () => void;
}) {
  const [month, setMonth] = useState(false);
  const dates = month ? monthDays(selected) : weekDays(selected);
  const from = dates[0];
  const to = dates[dates.length - 1];
  const { data, error, reload } = useSwr<RangeView>(`range:${from}:${to}`, () =>
    loadRange(from, to),
  );
  useEffect(() => {
    if (refreshKey > 0) void reload();
  }, [refreshKey, reload]);
  // Warm the neighbouring page so a swipe or arrow tap answers from the cache.
  useEffect(() => {
    for (const dir of [-1, 1]) {
      const step = month
        ? shiftMonth(selected, dir)
        : shiftDate(selected, dir * 7);
      const next = month ? monthDays(step) : weekDays(step);
      prefetch(`range:${next[0]}:${next[next.length - 1]}`, () =>
        loadRange(next[0], next[next.length - 1]),
      );
    }
  }, [selected, month]);

  const days = groupByDay(data);
  const attn = attention(days, dates, today);
  const moved = useRef(false);
  const grid = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (!moved.current) return;
    moved.current = false;
    grid.current
      ?.querySelector<HTMLButtonElement>(`[data-date="${selected}"]`)
      ?.focus();
  }, [selected]);

  const step = (dir: number) =>
    onSelect(month ? shiftMonth(selected, dir) : shiftDate(selected, dir * 7));
  function onKey(e: KeyboardEvent) {
    const by: Record<string, number> = {
      ArrowLeft: -1,
      ArrowRight: 1,
      ArrowUp: -7,
      ArrowDown: 7,
    };
    if (e.key in by) {
      e.preventDefault();
      moved.current = true;
      onSelect(shiftDate(selected, by[e.key]));
    } else if (e.key === "PageDown" || e.key === "PageUp") {
      e.preventDefault();
      moved.current = true;
      onSelect(shiftMonth(selected, e.key === "PageDown" ? 1 : -1));
    }
  }
  const unplannedToday = (days.get(selected) ?? []).filter(
    (e): e is Extract<DayEntry, { kind: "unplanned" }> =>
      e.kind === "unplanned",
  );
  const plan = bulkPlan(data?.unplanned, today);
  const touch = useRef<number | null>(null);
  const unit = month ? "tháng" : "tuần";
  const title = month
    ? `Tháng ${selected.slice(5, 7)}/${selected.slice(0, 4)}`
    : `Tuần ${ddmm(from)}–${`${ddmm(to)}/${to.slice(0, 4)}`}`;
  const classes = new Map<string, string>();
  for (const list of days.values())
    for (const e of list) classes.set(classOf(e), nameOf(e));

  return (
    <section className="cal" aria-label="Lịch tuần và tháng">
      <div className="cal-bar">
        <button aria-label={`Xem ${unit} trước`} onClick={() => step(-1)}>
          ‹
        </button>
        <h2 className="cal-title" aria-live="polite">
          {title}
        </h2>
        <button aria-label={`Xem ${unit} sau`} onClick={() => step(1)}>
          ›
        </button>
        <button onClick={() => onSelect(today)}>Về hôm nay</button>
        <button aria-expanded={month} onClick={() => setMonth(!month)}>
          {month ? "Thu về tuần" : "Xem tháng"}
        </button>
      </div>
      {error ? (
        <p role="alert" className="error">
          Không tải được lịch. Danh sách buổi trong ngày vẫn dùng được.
        </p>
      ) : null}
      {(attn.missing > 0 || attn.overdue > 0) && attn.oldest && (
        <div className="cal-alert" role="status">
          <span>
            ⚠ {attn.missing > 0 && `${attn.missing} buổi chưa điểm danh`}
            {attn.missing > 0 && attn.overdue > 0 && " · "}
            {attn.overdue > 0 && `${attn.overdue} buổi quá hạn`}
          </span>
          <button onClick={() => onSelect(attn.oldest!)}>
            Tới ngày cũ nhất
          </button>
        </div>
      )}
      <div className="cal-dows" aria-hidden="true">
        {dows.map((d) => (
          <div key={d}>{d}</div>
        ))}
      </div>
      <div
        ref={grid}
        className={`cal-grid ${month ? "month" : "week"}`}
        role="group"
        aria-label={month ? "Lịch tháng" : "Lịch tuần"}
        onKeyDown={onKey}
        onTouchStart={(e) => (touch.current = e.touches[0].clientX)}
        onTouchEnd={(e) => {
          if (touch.current == null) return;
          const dx = e.changedTouches[0].clientX - touch.current;
          touch.current = null;
          if (Math.abs(dx) > 60) step(dx < 0 ? 1 : -1);
        }}
      >
        {dates.map((date) => {
          const list = days.get(date) ?? [];
          const out = month && date.slice(0, 7) !== selected.slice(0, 7);
          return (
            <button
              key={date}
              data-date={date}
              aria-pressed={date === selected}
              aria-current={date === today ? "date" : undefined}
              aria-label={dayLabel(date, list, today)}
              tabIndex={date === selected ? 0 : -1}
              className={`cal-day${date === today ? " today" : ""}${out ? " out" : ""}`}
              onClick={() => onSelect(date)}
            >
              <span className="num" aria-hidden="true">
                {+date.slice(8)}
              </span>
              <span className="cal-dots" aria-hidden="true">
                {list.slice(0, MAX_DOTS).map((e) => (
                  <Dot key={e.key} entry={e} today={today} />
                ))}
                {list.length > MAX_DOTS && (
                  <span className="cal-more">+{list.length - MAX_DOTS}</span>
                )}
              </span>
              <span className="cal-chips" aria-hidden="true">
                {list.map((e) => (
                  <Chip key={e.key} entry={e} today={today} />
                ))}
              </span>
            </button>
          );
        })}
      </div>
      {unplannedToday.length > 0 && (
        <div className="cal-unplanned">
          <h3>Chưa tạo buổi ngày {ddmm(selected)}</h3>
          <ul>
            {unplannedToday.map((e) => (
              <li key={e.key}>
                {hhmm(e.slot.startTime)}–{hhmm(e.slot.endTime)} ·{" "}
                {e.slot.className}
              </li>
            ))}
          </ul>
        </div>
      )}
      {canWrite && (
        <BulkCreate
          plan={plan}
          scopeLabel={month ? "trong tháng này" : "trong tuần này"}
          onCreated={() => {
            void reload();
            onCreated();
          }}
        />
      )}
      <p className="cal-legend">
        ● đã có buổi · ◌ chưa tạo buổi · ■ chưa điểm danh hoặc quá hạn · bù: học
        bù · chữ gạch: đã hủy. Màu chỉ để phân biệt lớp.
      </p>
      {classes.size > 0 && (
        <p className="cal-classes">
          {[...classes].map(([id, name]) => (
            <span key={id} style={{ color: classColor(id) }}>
              ■ <span style={{ color: "var(--ink)" }}>{name}</span>
            </span>
          ))}
        </p>
      )}
    </section>
  );
}

function Dot({ entry, today }: { entry: DayEntry; today: string }) {
  const attn = needsAttention(entry, today);
  return (
    <span
      className={`cal-dot${entry.kind === "unplanned" ? " unplanned" : ""}${attn ? " attn" : ""}`}
      style={{ color: classColor(classOf(entry)) }}
    />
  );
}

function Chip({ entry, today }: { entry: DayEntry; today: string }) {
  const cancelled =
    entry.kind === "session" && entry.item.session.status === "cancelled";
  const makeup =
    entry.kind === "session" && entry.item.session.kind === "makeup";
  const mark =
    entry.kind === "unplanned"
      ? " ◌"
      : isMissing(entry) || isOverdue(entry, today)
        ? " !"
        : "";
  // The text is drawn by CSS (data-label): the chips are decoration, the day cell's label already says all of it.
  return (
    <span
      className={`cal-chip${entry.kind === "unplanned" ? " unplanned" : ""}${cancelled ? " cancelled" : ""}`}
      style={{ borderLeftColor: classColor(classOf(entry)) }}
      data-label={`${hhmm(startOf(entry))} ${nameOf(entry)}${makeup ? " bù" : ""}${mark}`}
    />
  );
}
