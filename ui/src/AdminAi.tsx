import { useState, type FormEvent } from "react";
import { api } from "./api";
import type { TeacherDetailView } from "./admin-api";
import { kindLabels, QuotaTable } from "./QuotaTable";

type Props = {
  teacher: TeacherDetailView;
  fail: (e: unknown, fallback: string) => void;
  done: (text: string) => Promise<void>;
};

const toInput = (value: number | null | undefined) =>
  value === null || value === undefined ? "" : String(value);
const fromInput = (value: string) =>
  value.trim() === "" ? null : Number(value);

// AI permission is per account and independent of quota; quota is a snapshot of the current period.
export function AdminAi({ teacher, fail, done }: Props) {
  const current = teacher.periods.find((p) => p.current);
  const [busy, setBusy] = useState(false);
  const [aiReason, setAiReason] = useState("");
  const [values, setValues] = useState<Record<string, string>>(() =>
    Object.fromEntries(teacher.quotas.map((q) => [q.kind, toInput(q.granted)])),
  );
  const [quotaReason, setQuotaReason] = useState("");
  async function run(action: () => Promise<void>, fallback: string) {
    setBusy(true);
    try {
      await action();
    } catch (e) {
      fail(e, fallback);
    } finally {
      setBusy(false);
    }
  }
  const toggle = (event: FormEvent) => {
    event.preventDefault();
    void run(async () => {
      await api(`/admin/teachers/${teacher.id}/ai`, "POST", {
        enabled: !teacher.aiEnabled,
        reason: aiReason,
      });
      setAiReason("");
      await done(
        teacher.aiEnabled
          ? "Đã tắt AI. Lượt đã nhận vẫn được hoàn tất; số lượt không bị đặt lại."
          : "Đã bật AI. Số lượt đã dùng được giữ nguyên.",
      );
    }, "Không thể đổi quyền AI.");
  };
  const saveQuotas = (event: FormEvent) => {
    event.preventDefault();
    if (!current) return;
    void run(async () => {
      await api(`/admin/periods/${current.id}/quotas`, "PUT", {
        ...Object.fromEntries(
          Object.entries(values).map(([kind, value]) => [
            kind,
            fromInput(value),
          ]),
        ),
        reason: quotaReason,
        version: current.version,
      });
      setQuotaReason("");
      await done("Đã điều chỉnh hạn mức của kỳ hiện tại.");
    }, "Không thể điều chỉnh hạn mức.");
  };
  return (
    <div className="roster-form" aria-busy={busy}>
      <h4>Quyền AI: {teacher.aiEnabled ? "đang bật" : "đang tắt"}</h4>
      <form onSubmit={toggle}>
        <label htmlFor="ai-reason">Lý do</label>
        <input
          id="ai-reason"
          maxLength={500}
          required
          value={aiReason}
          onChange={(e) => setAiReason(e.target.value)}
        />
        <button disabled={busy}>
          {teacher.aiEnabled ? "Tắt AI" : "Bật AI"}
        </button>
      </form>
      <QuotaTable quotas={teacher.quotas} />
      {current ? (
        <form onSubmit={saveQuotas}>
          <h4>Hạn mức kỳ hiện tại (để trống = chưa cấp)</h4>
          {teacher.quotas.map((q) => (
            <span key={q.kind}>
              <label htmlFor={`quota-${q.kind}`}>
                {kindLabels[q.kind] ?? q.kind}
              </label>
              <input
                id={`quota-${q.kind}`}
                type="number"
                min={0}
                max={100000}
                value={values[q.kind] ?? ""}
                onChange={(e) =>
                  setValues((v) => ({ ...v, [q.kind]: e.target.value }))
                }
              />
            </span>
          ))}
          <label htmlFor="quota-reason">Lý do điều chỉnh</label>
          <input
            id="quota-reason"
            maxLength={500}
            required
            value={quotaReason}
            onChange={(e) => setQuotaReason(e.target.value)}
          />
          <button disabled={busy}>Lưu hạn mức</button>
        </form>
      ) : (
        <p className="hint">Chưa có kỳ đang hiệu lực để cấp hạn mức.</p>
      )}
    </div>
  );
}
