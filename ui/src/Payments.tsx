import { useCallback, useEffect, useRef, useState } from "react";
import { api, ApiError } from "./api";
import { money } from "./tuition-api";
import { paymentStatus, type PaymentsReport } from "./payments-api";
import { ReceiptForm } from "./ReceiptForm";
import { PaymentQr, RecipientSettings, type Recipient } from "./PaymentQr";

export function Payments({
  classId,
  month,
  refreshToken,
  canWrite,
  onDenied,
}: {
  classId: string;
  month: string;
  refreshToken: string;
  canWrite: boolean;
  onDenied: (error: ApiError) => void;
}) {
  const [report, setReport] = useState<PaymentsReport | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [recipient, setRecipient] = useState<Recipient | null>(null);
  const request = useRef(0);
  const load = useCallback(async () => {
    const current = ++request.current;
    setLoading(true);
    setError("");
    try {
      const result = await api<PaymentsReport>(
        `/billing/classes/${classId}/payments?month=${encodeURIComponent(month)}`,
      );
      if (request.current === current) setReport(result);
    } catch (e) {
      if (request.current !== current) return;
      setError(
        e instanceof ApiError
          ? Object.values(e.errors ?? {})
              .flat()
              .join(" ") || e.message
          : "Không thể tải công nợ. Vui lòng thử lại.",
      );
      if (e instanceof ApiError) onDenied(e);
    } finally {
      if (request.current === current) setLoading(false);
    }
  }, [classId, month, onDenied]);
  useEffect(() => {
    void load();
    return () => {
      request.current++;
    };
  }, [load, refreshToken]);
  useEffect(() => {
    if (report?.status !== "pending" || loading || error) return;
    const timer = window.setTimeout(() => void load(), 1500);
    return () => window.clearTimeout(timer);
  }, [report, loading, error, load]);
  return (
    <section aria-label="Thanh toán và công nợ" className="payments-workspace">
      <h3>Thanh toán và công nợ</h3>
      <RecipientSettings
        canWrite={canWrite}
        onDenied={onDenied}
        onChanged={setRecipient}
      />
      <p>
        Trả kỳ nợ cũ nhất trước. Tiền dư được giữ riêng, chưa tự dùng cho kỳ sau
        hoặc hoàn tiền. Quét QR hay ảnh chuyển khoản chưa xác nhận đã nhận tiền.
      </p>
      <button onClick={() => void load()} disabled={loading}>
        Tải lại công nợ
      </button>
      {loading && <p role="status">Đang tải công nợ…</p>}
      {error && (
        <p role="alert" className="error">
          {error}
        </p>
      )}
      {report && (
        <>
          <p role="status" className="notice">
            {loading || error || report.month !== month
              ? "Chưa xác minh công nợ mới; dữ liệu dưới thuộc lần tải trước."
              : report.status === "pending"
                ? "Công nợ đang chờ cập nhật phí. Chưa thể nhận tiền."
                : report.status === "missing-rate"
                  ? "Công nợ chưa đầy đủ vì thiếu đơn giá. Chưa thể nhận tiền."
                  : report.status === "needs-review"
                    ? "Có tiền phân bổ vượt phí sau thay đổi điểm danh. Khoản này đang giữ để đối chiếu, không tự dùng lại. Chưa thể nhận tiền mới; cần xử lý qua nghiệp vụ điều chỉnh."
                    : "Đã cập nhật công nợ."}
          </p>
          <p>Tổng công nợ các kỳ: {money(report.totalDebtVnd)}</p>
          <div className="tuition-students">
            {report.students.map((student) => (
              <article
                key={student.studentId}
                className="class-card"
                aria-label={`Công nợ ${student.name}`}
              >
                <h4>Công nợ: {student.name}</h4>
                <p>
                  Nợ trước tháng {report.month}: {money(student.oldDebtVnd)}
                </p>
                <p>
                  Nợ tháng {report.month}: {money(student.currentDebtVnd)}
                </p>
                <p>Tổng cần thu: {money(student.totalDebtVnd)}</p>
                <p>Tiền chưa phân bổ: {money(student.unallocatedVnd)}</p>
                {student.allocationExcessVnd !== "0" && (
                  <p className="notice">
                    Tiền giữ để đối chiếu: {money(student.allocationExcessVnd)}
                  </p>
                )}
                <ul aria-label="Công nợ từng kỳ">
                  {student.periods.map((period) => (
                    <li key={period.month}>
                      {period.month} · {paymentStatus[period.status]} · Phí:{" "}
                      {money(period.feesVnd)} · Đã phân bổ:{" "}
                      {money(period.paidVnd)} · Còn nợ: {money(period.debtVnd)}
                      {period.allocationExcessVnd !== "0" &&
                        ` · Giữ để đối chiếu: ${money(period.allocationExcessVnd)}`}
                    </li>
                  ))}
                </ul>
                <PaymentQr
                  classId={classId}
                  studentId={student.studentId}
                  month={month}
                  version={report.version}
                  recipient={recipient}
                  enabled={
                    canWrite &&
                    !loading &&
                    !error &&
                    report.status === "ready" &&
                    report.month === month &&
                    student.totalDebtVnd !== "0"
                  }
                  onDenied={onDenied}
                />
                <ReceiptForm
                  classId={classId}
                  studentId={student.studentId}
                  version={report.version}
                  canWrite={
                    canWrite &&
                    !loading &&
                    !error &&
                    report.status === "ready" &&
                    report.month === month
                  }
                  onDenied={onDenied}
                  canReplay={canWrite}
                  onSaved={() => void load()}
                />
                <h4>Lịch sử nhận tiền</h4>
                {student.receipts.length === 0 && <p>Chưa ghi nhận tiền.</p>}
                <ul aria-label="Lịch sử nhận tiền">
                  {student.receipts.map((receipt) => (
                    <li key={receipt.id}>
                      <p>
                        {receipt.receivedOn} · Nhận {money(receipt.amountVnd)}
                        {receipt.note && ` · ${receipt.note}`}
                      </p>
                      <p>
                        Phân bổ:{" "}
                        {receipt.allocations.length === 0
                          ? "Chưa phân bổ"
                          : receipt.allocations
                              .map((a) => `${a.month}: ${money(a.amountVnd)}`)
                              .join("; ")}
                      </p>
                      <p>Chưa phân bổ: {money(receipt.unallocatedVnd)}</p>
                    </li>
                  ))}
                </ul>
              </article>
            ))}
          </div>
        </>
      )}
    </section>
  );
}
