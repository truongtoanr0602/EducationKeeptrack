import { useRef, useState, type PointerEvent } from "react";
import {
  STEP_MINUTES,
  blockPosition,
  dayShort,
  gridHours,
  hhmm,
  slotLabel,
  toTime,
  weekOrder,
  type Slot,
} from "./schedule-calendar";
import type { OtherSlot } from "./schedule-overlap";

const HOUR_PX = 22;

type Props = {
  slots: Slot[];
  selected: number | null;
  onSelect: (index: number) => void;
  /** Desktop only: dragging on a day column proposes a slot. */
  onDraw?: (day: number, start: string, end: string) => void;
  disabled?: boolean;
  /** Slots of the teacher's other classes, drawn grey behind this class's blocks. */
  others?: OtherSlot[];
  /** Indexes of `slots` that overlap another class. */
  clashing?: Set<number>;
};

/**
 * A visual summary of the week (Monday first). It is not the only way to edit: the text list under it and the quick
 * entry form do the same things with a keyboard or a screen reader, so the grid is hidden from assistive technology
 * and its blocks are plain click targets, not focusable controls.
 */
export function WeekCalendar({
  slots,
  selected,
  onSelect,
  onDraw,
  disabled,
  others = [],
  clashing,
}: Props) {
  const range = gridHours([...slots, ...others]);
  const hours = range.toHour - range.fromHour;
  const total = hours * 60;
  const [ghost, setGhost] = useState<{
    day: number;
    from: number;
    to: number;
  } | null>(null);
  const anchor = useRef(0);

  function minuteAt(event: PointerEvent<HTMLElement>) {
    const rect = event.currentTarget.getBoundingClientRect();
    const ratio = Math.min(
      1,
      Math.max(0, (event.clientY - rect.top) / rect.height),
    );
    const minute =
      range.fromHour * 60 +
      Math.round((ratio * total) / STEP_MINUTES) * STEP_MINUTES;
    return Math.min(range.toHour * 60, 24 * 60 - 1, minute);
  }

  return (
    <div className="week-calendar" aria-hidden="true">
      <div className="week-head">
        <span />
        {weekOrder.map((day) => (
          <span key={day}>{dayShort[day]}</span>
        ))}
      </div>
      <div className="week-body" style={{ height: hours * HOUR_PX }}>
        <div className="week-hours">
          {Array.from({ length: hours }, (_, i) => (
            <span key={i} style={{ top: i * HOUR_PX }}>
              {String(range.fromHour + i).padStart(2, "0")}
            </span>
          ))}
        </div>
        {weekOrder.map((day) => (
          <div
            key={day}
            className="week-col"
            data-day={day}
            style={{ backgroundSize: `100% ${HOUR_PX}px` }}
            onPointerDown={(event) => {
              if (
                !onDraw ||
                disabled ||
                event.pointerType !== "mouse" ||
                event.button !== 0
              )
                return;
              event.currentTarget.setPointerCapture(event.pointerId);
              anchor.current = minuteAt(event);
              setGhost({ day, from: anchor.current, to: anchor.current });
            }}
            onPointerMove={(event) => {
              if (!ghost || ghost.day !== day) return;
              const minute = minuteAt(event);
              setGhost({
                day,
                from: Math.min(anchor.current, minute),
                to: Math.max(anchor.current, minute),
              });
            }}
            onPointerUp={() => {
              if (ghost?.day === day && ghost.to - ghost.from >= STEP_MINUTES)
                onDraw?.(day, toTime(ghost.from), toTime(ghost.to));
              setGhost(null);
            }}
            onPointerCancel={() => setGhost(null)}
          >
            {others.map((slot, index) =>
              slot.dayOfWeek === day ? (
                <div
                  key={`other-${index}`}
                  className="week-block other"
                  title={`${slot.className} · ${hhmm(slot.startTime)}–${hhmm(slot.endTime)}`}
                  style={{
                    top: `${blockPosition(slot, range).top}%`,
                    height: `${blockPosition(slot, range).height}%`,
                  }}
                >
                  <span>{slot.className}</span>
                </div>
              ) : null,
            )}
            {slots.map((slot, index) =>
              slot.dayOfWeek === day ? (
                <div
                  key={index}
                  className={`week-block${selected === index ? " selected" : ""}${clashing?.has(index) ? " clash" : ""}`}
                  data-slot={index}
                  title={slotLabel(slot)}
                  style={{
                    top: `${blockPosition(slot, range).top}%`,
                    height: `${blockPosition(slot, range).height}%`,
                  }}
                  onPointerDown={(event) => event.stopPropagation()}
                  onClick={() => onSelect(index)}
                >
                  <span>{hhmm(slot.startTime)}</span>
                  <span className="week-block-end">–{hhmm(slot.endTime)}</span>
                </div>
              ) : null,
            )}
            {ghost?.day === day && (
              <div
                className="week-block ghost"
                style={{
                  top: `${((ghost.from - range.fromHour * 60) / total) * 100}%`,
                  height: `${((ghost.to - ghost.from) / total) * 100}%`,
                }}
              />
            )}
          </div>
        ))}
      </div>
    </div>
  );
}
