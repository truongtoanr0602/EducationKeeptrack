import { today } from "./roster-api";
import {
  dayShort,
  hhmm,
  previewCount,
  previewWeeks,
  weekOrder,
  type Slot,
} from "./schedule-calendar";

const DEFAULT_WEEKS = 5;
const show = (date: string) =>
  `${date.slice(8)}/${date.slice(5, 7)}/${date.slice(0, 4)}`;

/**
 * Which days the saved weekly schedule would give sessions on, if the teacher then creates sessions from it. The
 * creation flow passes the number of weeks it will really generate, so the preview shows exactly those days.
 */
export function MonthPreview({
  slots,
  effectiveFrom,
  weeks = DEFAULT_WEEKS,
}: {
  slots: Slot[];
  effectiveFrom: string;
  weeks?: number;
}) {
  const rows = previewWeeks({
    slots,
    effectiveFrom,
    today: today(),
    weeks,
  });
  const inRange = rows.flat().filter((cell) => cell.inRange);
  const count = previewCount(rows);
  return (
    <section className="month-preview" aria-label="Xem trước buổi học">
      <h4>Xem trước {weeks} tuần tới</h4>
      <p className="muted" role="status">
        {count === 0
          ? "Lịch tuần đang trống nên chưa có buổi nào."
          : `${count} buổi từ ${show(inRange[0].date)} đến ${show(inRange[inRange.length - 1].date)}, nếu bạn tạo buổi theo lịch này.`}
      </p>
      <table>
        <thead>
          <tr>
            {weekOrder.map((day) => (
              <th scope="col" key={day}>
                {dayShort[day]}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr key={row[0].date}>
              {row.map((cell) => (
                <td
                  key={cell.date}
                  className={cell.inRange ? undefined : "out"}
                  data-date={cell.date}
                  data-sessions={cell.slots.length || undefined}
                >
                  <span className="day-number">
                    {cell.day === 1
                      ? `${cell.day}/${cell.date.slice(5, 7)}`
                      : cell.day}
                  </span>
                  {cell.slots.map((slot, index) => (
                    <span className="session-time" key={index}>
                      <span className="sr-only">Buổi lúc </span>
                      {hhmm(slot.startTime)}
                    </span>
                  ))}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </section>
  );
}
