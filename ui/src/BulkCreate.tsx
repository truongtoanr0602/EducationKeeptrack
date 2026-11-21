import { useState } from "react";
import { api, ApiError } from "./api";
import { ddmm, type BulkClass } from "./calendar-api";
import { Popup } from "./ui/popup";

type Outcome = { classId: string; ok: boolean; text: string };

const reason = (e: unknown) =>
  e instanceof ApiError
    ? Object.values(e.errors ?? {})
        .flat()
        .join(" ") || e.message
    : "Không tạo được. Vui lòng thử lại.";

/**
 * Previews what "create sessions" would add for every class, then creates class by class with the existing generate
 * endpoint (nothing is automatic). One class failing, for example on a clash, does not stop the others; each class gets
 * its own result line.
 */
export function BulkCreate({
  plan: livePlan,
  scopeLabel,
  onCreated,
}: {
  plan: BulkClass[];
  scopeLabel: string;
  onCreated: () => void;
}) {
  const [open, setOpen] = useState(false);
  const [busy, setBusy] = useState(false);
  const [results, setResults] = useState<Outcome[] | null>(null);
  // The plan is frozen while the dialog is open: creating sessions empties it, and the results must stay readable.
  const [frozen, setFrozen] = useState<BulkClass[]>([]);
  const plan = open ? frozen : livePlan;
  const total = plan.reduce((sum, row) => sum + row.count, 0);

  async function confirm() {
    setBusy(true);
    const out: Outcome[] = [];
    for (const row of plan) {
      try {
        const schedule = await api<{ version: number }>(
          `/classes/${row.classId}/schedule`,
        );
        const created = await api<unknown[]>(
          `/classes/${row.classId}/sessions/generate`,
          "POST",
          { from: row.from, to: row.to, scheduleVersion: schedule.version },
        );
        out.push({
          classId: row.classId,
          ok: true,
          text: `Đã tạo ${created.length} buổi.`,
        });
      } catch (e) {
        out.push({ classId: row.classId, ok: false, text: reason(e) });
      }
    }
    setResults(out);
    setBusy(false);
    onCreated();
  }
  const close = (value: boolean) => {
    setOpen(value);
    if (!value) setResults(null);
  };

  return (
    <>
      {livePlan.length > 0 && !open && (
        <button
          className="cal-bulk"
          onClick={() => {
            setFrozen(livePlan);
            setOpen(true);
          }}
        >
          Tạo {total} buổi chưa có {scopeLabel}
        </button>
      )}
      <Popup
        open={open}
        onOpenChange={close}
        locked={busy}
        title="Tạo buổi chưa có"
        description={`Tạo các buổi theo lịch tuần của từng lớp, từ hôm nay trở đi ${scopeLabel}.`}
        footer={
          results ? (
            <button className="primary" onClick={() => close(false)}>
              Đóng
            </button>
          ) : (
            <>
              <button
                className="primary"
                disabled={busy}
                onClick={() => void confirm()}
              >
                {busy ? "Đang tạo…" : `Xác nhận tạo ${total} buổi`}
              </button>
              <button disabled={busy} onClick={() => close(false)}>
                Hủy
              </button>
            </>
          )
        }
      >
        <ul className="cal-bulk-list">
          {plan.map((row) => {
            const result = results?.find((r) => r.classId === row.classId);
            return (
              <li key={row.classId}>
                <b>{row.className}</b>: {row.count} buổi ({ddmm(row.from)}
                {row.to !== row.from && `–${ddmm(row.to)}`})
                {result && (
                  <span
                    role={result.ok ? "status" : "alert"}
                    className={result.ok ? "ok" : "error"}
                  >
                    {" "}
                    {result.ok ? "✓" : "✗"} {result.text}
                  </span>
                )}
              </li>
            );
          })}
        </ul>
      </Popup>
    </>
  );
}
