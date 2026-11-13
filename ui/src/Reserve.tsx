import {
  useCallback,
  useEffect,
  useRef,
  useState,
  type FormEvent,
} from "react";
import { api, ApiError } from "./api";
import { today } from "./roster-api";
import { money } from "./tuition-api";
import {
  reserveStatus,
  type ReserveDraft,
  type ReserveReport,
  type ReserveRequest,
  type StudentReserve,
} from "./reserve-api";

const text = (e: unknown) =>
  e instanceof ApiError
    ? Object.values(e.errors ?? {})
        .flat()
        .join(" ") || e.message
    : "Không thể hoàn tất thao tác. Vui lòng thử lại.";

export function Reserve({
  classId,
  refreshToken,
  canWrite,
  onDenied,
  onChanged,
}: {
  classId: string;
  refreshToken: string;
  canWrite: boolean;
  onDenied: (error: ApiError) => void;
  onChanged: () => void;
}) {
  const [report, setReport] = useState<ReserveReport | null>(null);
  const [error, setError] = useState("");
  const request = useRef(0);
  const load = useCallback(async () => {
    const current = ++request.current;
    try {
      const result = await api<ReserveReport>(
        `/billing/classes/${classId}/reserve`,
      );
      if (request.current === current) {
        setReport(result);
        setError("");
      }
    } catch (e) {
      if (request.current !== current) return;
      setError(text(e));
      if (e instanceof ApiError) onDenied(e);
    }
  }, [classId, onDenied]);
  useEffect(() => {
    void load();
    return () => {
      request.current++;
    };
  }, [load, refreshToken]);
  const changed = () => {
    void load();
    onChanged();
  };
  return (
    <section aria-label="Tiền trả trước và bảo lưu" className="reserve-panel">
      <h3>Tiền trả trước và bảo lưu nghỉ ốm</h3>
      {error && (
        <p role="alert" className="error">
          {error}
        </p>
      )}
      {report && (
        <>
          <p className="notice" data-testid="reserve-policy">
            {report.policy.message}
          </p>
          {report.students.map((student) => (
            <StudentReserveCard
              key={student.studentId}
              classId={classId}
              student={student}
              canWrite={canWrite}
              onDenied={onDenied}
              onChanged={changed}
            />
          ))}
        </>
      )}
    </section>
  );
}

function StudentReserveCard({
  classId,
  student,
  canWrite,
  onDenied,
  onChanged,
}: {
  classId: string;
  student: StudentReserve;
  canWrite: boolean;
  onDenied: (error: ApiError) => void;
  onChanged: () => void;
}) {
  const hasMoney = BigInt(student.collectedVnd) > 0n;
  if (!hasMoney && student.requests.length === 0) return null;
  return (
    <article aria-label={`Tiền trả trước của ${student.name}`}>
      <h4>{student.name}</h4>
      <p data-testid={`reserve-summary-${student.studentId}`}>
        Đã thu {money(student.collectedVnd)} · Đã dùng cho học phí{" "}
        {money(student.usedVnd)} · Chưa dùng {money(student.unusedVnd)} · Đã nằm
        trong bảo lưu {money(student.reservedVnd)} · Có thể xét bảo lưu tối đa{" "}
        {money(student.availableVnd)}
      </p>
      <div className="table-wrap">
        <table>
          <caption>Nguồn tiền đã thu</caption>
          <thead>
            <tr>
              <th>Ngày nhận</th>
              <th>Ghi chú</th>
              <th>Đã thu</th>
              <th>Đã dùng</th>
              <th>Chưa dùng</th>
              <th>Đã bảo lưu</th>
              <th>Còn xét được</th>
            </tr>
          </thead>
          <tbody>
            {student.sources.map((s) => (
              <tr key={s.receiptId}>
                <td>{s.receivedOn}</td>
                <td>{s.note ?? "—"}</td>
                <td>{money(s.collectedVnd)}</td>
                <td>{money(s.usedVnd)}</td>
                <td>{money(s.unusedVnd)}</td>
                <td>{money(s.reservedVnd)}</td>
                <td>{money(s.availableVnd)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {student.requests.map((r) => (
        <RequestCard
          key={`${r.id}:${r.version}`}
          classId={classId}
          request={r}
          canWrite={canWrite}
          onDenied={onDenied}
          onChanged={onChanged}
        />
      ))}
      {canWrite && BigInt(student.availableVnd) > 0n && (
        <RequestForm
          classId={classId}
          studentId={student.studentId}
          max={student.availableVnd}
          onDenied={onDenied}
          onSaved={onChanged}
        />
      )}
      {!canWrite && (
        <p className="notice">
          Chỉ xem: cần quyền sử dụng còn hạn để ghi đề nghị hoặc quyết định bảo
          lưu.
        </p>
      )}
    </article>
  );
}

function RequestForm({
  classId,
  studentId,
  max,
  onDenied,
  onSaved,
}: {
  classId: string;
  studentId: string;
  max: string;
  onDenied: (error: ApiError) => void;
  onSaved: () => void;
}) {
  const [from, setFrom] = useState(today);
  const [to, setTo] = useState(today);
  const [reason, setReason] = useState("");
  const [evidence, setEvidence] = useState("");
  const [amount, setAmount] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [uncertain, setUncertain] = useState(false);
  // The exact command is kept after a transport failure: it may already have committed.
  const pending = useRef<ReserveDraft | null>(null);
  async function submit(event: FormEvent) {
    event.preventDefault();
    if (busy) return;
    setError("");
    if (!pending.current) {
      if (
        !reason.trim() ||
        !/^\d{1,18}$/.test(amount) ||
        BigInt(amount) <= 0n ||
        !from ||
        !to ||
        to < from
      ) {
        setError("Nhập khoảng ngày nghỉ, lý do và số tiền nguyên đồng dương.");
        return;
      }
      pending.current = {
        studentId,
        absentFrom: from,
        absentTo: to,
        reason,
        evidenceNote: evidence,
        amountVnd: amount,
        idempotencyKey: crypto.randomUUID(),
      };
    }
    setBusy(true);
    try {
      await api<ReserveRequest>(
        `/billing/classes/${classId}/reserve/requests`,
        "POST",
        pending.current,
      );
      pending.current = null;
      setUncertain(false);
      setReason("");
      setEvidence("");
      setAmount("");
      onSaved();
    } catch (e) {
      if (e instanceof ApiError && e.status < 500) {
        pending.current = null;
        setUncertain(false);
        setError(text(e));
        onDenied(e);
      } else {
        setUncertain(true);
        setError(
          "Kết quả lưu chưa rõ. Thử lại đề nghị này để đối chiếu, tránh ghi trùng.",
        );
      }
    } finally {
      setBusy(false);
    }
  }
  const locked = busy || uncertain;
  return (
    <form
      className="roster-form"
      aria-label="Ghi đề nghị bảo lưu"
      onSubmit={submit}
      noValidate
    >
      <h5>Ghi đề nghị bảo lưu nghỉ ốm</h5>
      <p>
        Tối đa {money(max)} (tiền đã thu, chưa dùng và chưa nằm trong bảo lưu
        hoặc đề nghị khác).
      </p>
      <label className="field">
        Nghỉ từ ngày
        <input
          type="date"
          value={from}
          disabled={locked}
          onChange={(e) => setFrom(e.target.value)}
        />
      </label>
      <label className="field">
        Nghỉ đến ngày
        <input
          type="date"
          value={to}
          disabled={locked}
          onChange={(e) => setTo(e.target.value)}
        />
      </label>
      <label className="field">
        Lý do nghỉ
        <textarea
          maxLength={1000}
          value={reason}
          disabled={locked}
          onChange={(e) => setReason(e.target.value)}
        />
      </label>
      <label className="field">
        Ghi chú minh chứng (không bắt buộc)
        <textarea
          maxLength={1000}
          value={evidence}
          disabled={locked}
          onChange={(e) => setEvidence(e.target.value)}
        />
      </label>
      <label className="field">
        Số tiền đề nghị bảo lưu (đồng)
        <input
          inputMode="numeric"
          maxLength={18}
          value={amount}
          disabled={locked}
          onChange={(e) => setAmount(e.target.value)}
        />
      </label>
      {error && (
        <p role="alert" className="error">
          {error}
        </p>
      )}
      <button className="primary" disabled={busy}>
        {busy ? "Đang lưu…" : uncertain ? "Thử lại đề nghị" : "Ghi đề nghị"}
      </button>
    </form>
  );
}

function RequestCard({
  classId,
  request,
  canWrite,
  onDenied,
  onChanged,
}: {
  classId: string;
  request: ReserveRequest;
  canWrite: boolean;
  onDenied: (error: ApiError) => void;
  onChanged: () => void;
}) {
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [uncertain, setUncertain] = useState(false);
  const pending = useRef<{
    decision: string;
    reason: string;
    idempotencyKey: string;
    version: number;
  } | null>(null);
  async function decide(decision: "approve" | "reject") {
    if (busy) return;
    setError("");
    if (!pending.current) {
      if (!reason.trim()) {
        setError("Nhập lý do quyết định.");
        return;
      }
      pending.current = {
        decision,
        reason,
        idempotencyKey: crypto.randomUUID(),
        version: request.version,
      };
    }
    setBusy(true);
    try {
      await api<ReserveRequest>(
        `/billing/classes/${classId}/reserve/requests/${request.id}/decision`,
        "POST",
        pending.current,
      );
      pending.current = null;
      setUncertain(false);
      onChanged();
    } catch (e) {
      if (e instanceof ApiError && e.status < 500) {
        pending.current = null;
        setUncertain(false);
        setError(text(e));
        onDenied(e);
        if (e.status === 409) onChanged();
      } else {
        setUncertain(true);
        setError(
          "Kết quả chưa rõ. Bấm lại cùng quyết định để đối chiếu, tránh quyết định trùng.",
        );
      }
    } finally {
      setBusy(false);
    }
  }
  return (
    <div className="reserve-request" data-status={request.status}>
      <p>
        <strong>{reserveStatus[request.status]}</strong> ·{" "}
        {money(request.requestedVnd)} · nghỉ {request.absentFrom} đến{" "}
        {request.absentTo} · {request.reason}
        {request.evidenceNote ? ` · Minh chứng: ${request.evidenceNote}` : ""}
      </p>
      {request.sources.length > 0 && (
        <p>
          Lấy từ:{" "}
          {request.sources
            .map((s) => `khoản nhận ${s.receivedOn} (${money(s.amountVnd)})`)
            .join("; ")}
        </p>
      )}
      <details>
        <summary>Lịch sử ({request.history.length})</summary>
        <ul>
          {request.history.map((h) => (
            <li key={h.version}>
              {new Date(h.at).toLocaleString("vi-VN")} ·{" "}
              {h.action === "requested"
                ? "Ghi đề nghị"
                : h.action === "approved"
                  ? "Duyệt"
                  : "Từ chối"}
              : {h.reason}
            </li>
          ))}
        </ul>
      </details>
      {request.status === "pending" && canWrite && (
        <div>
          <label className="field">
            Lý do quyết định
            <input
              maxLength={1000}
              value={reason}
              disabled={busy || uncertain}
              onChange={(e) => setReason(e.target.value)}
            />
          </label>
          {error && (
            <p role="alert" className="error">
              {error}
            </p>
          )}
          <button
            className="primary"
            disabled={busy}
            onClick={() => void decide("approve")}
          >
            Duyệt bảo lưu
          </button>
          <button disabled={busy} onClick={() => void decide("reject")}>
            Từ chối
          </button>
        </div>
      )}
    </div>
  );
}
