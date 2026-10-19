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
import { paymentStatus } from "./payments-api";
import type { CashflowReport, PeriodReport } from "./statistics-api";

type View = "cashflow" | "period";
type Report =
  | { view: "cashflow"; data: CashflowReport }
  | { view: "period"; data: PeriodReport };

export function CashflowStatistics({
  classId,
  month,
  refreshToken,
  onDenied,
}: {
  classId: string;
  month: string;
  refreshToken: string;
  onDenied: (error: ApiError) => void;
}) {
  const [initial] = useState(() => {
    const query = new URLSearchParams(window.location.search);
    return {
      view:
        query.get("statsView") === "period"
          ? ("period" as const)
          : ("cashflow" as const),
      from: query.get("statsFrom") ?? today().slice(0, 7) + "-01",
      to: query.get("statsTo") ?? today(),
      month: query.get("statsMonth") ?? month,
    };
  });
  const [view, setView] = useState<View>(initial.view);
  const [from, setFrom] = useState(initial.from);
  const [to, setTo] = useState(initial.to);
  const [period, setPeriod] = useState(initial.month);
  const [filters, setFilters] = useState(initial);
  const [report, setReport] = useState<Report | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [selected, setSelected] = useState<string | null>(null);
  const request = useRef(0);
  const load = useCallback(async () => {
    const current = ++request.current;
    setLoading(true);
    setError("");
    setReport(null);
    setSelected(null);
    try {
      const path = `/billing/classes/${classId}`;
      const result: Report =
        view === "cashflow"
          ? {
              view,
              data: await api<CashflowReport>(
                `${path}/cashflow?${new URLSearchParams({ from: filters.from, to: filters.to })}`,
              ),
            }
          : {
              view,
              data: await api<PeriodReport>(
                `${path}/period-balances?${new URLSearchParams({ month: filters.month })}`,
              ),
            };
      if (current !== request.current) return;
      setReport(result);
      const url = new URL(window.location.href);
      url.searchParams.set("statsView", view);
      url.searchParams.set("statsFrom", filters.from);
      url.searchParams.set("statsTo", filters.to);
      url.searchParams.set("statsMonth", filters.month);
      window.history.replaceState(null, "", url);
    } catch (e) {
      if (current !== request.current) return;
      setError(
        e instanceof ApiError
          ? Object.values(e.errors ?? {})
              .flat()
              .join(" ") || e.message
          : "Không thể tải thống kê. Vui lòng thử lại.",
      );
      if (e instanceof ApiError) onDenied(e);
    } finally {
      if (current === request.current) setLoading(false);
    }
  }, [classId, view, filters, onDenied]);
  useEffect(() => {
    void load();
    return () => {
      request.current++;
    };
  }, [load, refreshToken]);
  useEffect(() => {
    if (report?.data.status !== "pending" || loading || error) return;
    const timer = window.setTimeout(() => void load(), 1500);
    return () => window.clearTimeout(timer);
  }, [report, loading, error, load]);
  function submit(event: FormEvent) {
    event.preventDefault();
    setFilters({ view, from, to, month: period });
  }
  const cash = report?.view === "cashflow" ? report.data : null;
  const debts = report?.view === "period" ? report.data : null;
  const receipt = cash?.receipts.find((x) => x.receipt.id === selected);
  const student = debts?.students.find((x) => x.studentId === selected);
  return (
    <section
      aria-label="Thống kê thu tiền và công nợ"
      className="payments-workspace"
    >
      <h3>Thống kê thu tiền và công nợ</h3>
      <p>
        Theo lớp hiện tại. Ngày thực nhận theo giờ Việt Nam (Asia/Ho_Chi_Minh);
        kỳ nghĩa vụ là tháng phát sinh học phí. Chỉ khoản nhận còn hiệu lực được
        tính là thực thu. Dùng bảo lưu không phải tiền mới nhận.
      </p>
      <div className="form-actions">
        <button
          type="button"
          aria-pressed={view === "cashflow"}
          onClick={() => setView("cashflow")}
        >
          Thực thu theo ngày nhận
        </button>
        <button
          type="button"
          aria-pressed={view === "period"}
          onClick={() => setView("period")}
        >
          Công nợ theo kỳ học
        </button>
      </div>
      <form className="session-range" onSubmit={submit} noValidate>
        {view === "cashflow" ? (
          <>
            <label>
              Từ ngày thực nhận
              <input
                type="date"
                value={from}
                onChange={(e) => setFrom(e.target.value)}
              />
            </label>
            <label>
              Đến ngày thực nhận
              <input
                type="date"
                value={to}
                onChange={(e) => setTo(e.target.value)}
              />
            </label>
          </>
        ) : (
          <label>
            Kỳ nghĩa vụ
            <input
              type="month"
              value={period}
              onChange={(e) => setPeriod(e.target.value)}
            />
          </label>
        )}
        <button disabled={loading}>Tải thống kê</button>
      </form>
      {loading && <p role="status">Đang tải thống kê…</p>}
      {error && (
        <p role="alert" className="error">
          {error}
        </p>
      )}
      {report && (
        <>
          <p>Phiên bản thống kê: {report.data.version}</p>
          {report.data.status !== "ready" && (
            <p role="status" className="notice">
              {report.data.status === "pending"
                ? "Phí đang chờ cập nhật; số liệu công nợ thuộc lần tổng hợp trước."
                : report.data.status === "missing-rate"
                  ? "Thiếu đơn giá: nghĩa vụ và công nợ chưa đầy đủ."
                  : "Có tiền giữ để đối chiếu; khoản này chưa được tính vào đã thanh toán."}
            </p>
          )}
        </>
      )}
      {cash && (
        <>
          <p>
            Thực thu từ {cash.from} đến {cash.to} (gồm hai ngày đầu/cuối).
          </p>
          <p role="status" aria-label="Tổng thực thu">
            Tổng thực thu: {money(cash.totalReceivedVnd)}
          </p>
          <p>
            Tiền chưa phân bổ từ các khoản nhận trong khoảng:{" "}
            {money(cash.totalUnallocatedVnd)}
          </p>
          <div className="tuition-table-wrap">
            <table aria-label="Chi tiết thực thu">
              <thead>
                <tr>
                  <th>Ngày thực nhận</th>
                  <th>Học sinh</th>
                  <th>Số tiền nhận</th>
                  <th>Chi tiết</th>
                </tr>
              </thead>
              <tbody>
                {cash.receipts.map((row) => (
                  <tr key={row.receipt.id}>
                    <td>{row.receipt.receivedOn}</td>
                    <td>{row.studentName}</td>
                    <td>{money(row.receipt.amountVnd)}</td>
                    <td>
                      <button
                        type="button"
                        onClick={() => setSelected(row.receipt.id)}
                      >
                        Đối chiếu khoản nhận {row.receipt.receivedOn}
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {cash.receipts.length === 0 && (
            <p>Không có khoản nhận tiền còn hiệu lực trong khoảng này.</p>
          )}
        </>
      )}
      {receipt && (
        <section aria-label="Đối chiếu giao dịch" className="class-card">
          <h4>Khoản nhận của {receipt.studentName}</h4>
          <p>Mã giao dịch: {receipt.receipt.id}</p>
          <p>
            Ngày thực nhận: {receipt.receipt.receivedOn} · Số tiền:{" "}
            {money(receipt.receipt.amountVnd)}
          </p>
          <p>Ghi chú: {receipt.receipt.note || "Không có"}</p>
          <p>
            Phân bổ:{" "}
            {receipt.receipt.allocations
              .map((a) => `${a.month}: ${money(a.amountVnd)}`)
              .join("; ") || "Chưa phân bổ"}
          </p>
          <p>Tiền chưa phân bổ: {money(receipt.receipt.unallocatedVnd)}</p>
          <button
            type="button"
            onClick={() =>
              document
                .getElementById(`receipt-${receipt.receipt.id}`)
                ?.scrollIntoView({ block: "center" })
            }
          >
            Mở lịch sử khoản nhận tiền
          </button>
        </section>
      )}
      {debts && (
        <>
          <p>
            Công nợ kỳ nghĩa vụ {debts.month}, gồm các khoản phân bổ trả kỳ này
            bất kể ngày nhận tiền.
          </p>
          <p role="status" aria-label="Tổng học phí kỳ">
            Tổng học phí sau điều chỉnh: {money(debts.totalFeesVnd)}
          </p>
          <p role="status" aria-label="Tổng đã thanh toán kỳ">
            Tổng đã thanh toán: {money(debts.totalPaidVnd)}
          </p>
          <p role="status" aria-label="Tổng công nợ kỳ">
            Tổng còn nợ: {money(debts.totalDebtVnd)}
          </p>
          <p>Tiền giữ để đối chiếu kỳ: {money(debts.totalHeldVnd)}</p>
          <div className="tuition-table-wrap">
            <table aria-label="Chi tiết công nợ kỳ">
              <thead>
                <tr>
                  <th>Học sinh</th>
                  <th>Học phí sau điều chỉnh</th>
                  <th>Đã thanh toán</th>
                  <th>Còn nợ</th>
                  <th>Trạng thái / Chi tiết</th>
                </tr>
              </thead>
              <tbody>
                {debts.students.map((row) => (
                  <tr key={row.studentId}>
                    <td>{row.name}</td>
                    <td>{money(row.feesVnd)}</td>
                    <td>{money(row.paidVnd)}</td>
                    <td>{money(row.debtVnd)}</td>
                    <td>
                      {paymentStatus[row.status]}
                      <button
                        type="button"
                        onClick={() => setSelected(row.studentId)}
                      >
                        Đối chiếu kỳ {row.name}
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {debts.students.length === 0 && <p>Chưa có học sinh trong lớp.</p>}
        </>
      )}
      {student && (
        <section aria-label="Đối chiếu kỳ" className="class-card">
          <h4>
            {student.name} · Kỳ {debts?.month}
          </h4>
          <p>
            Học phí sau điều chỉnh: {money(student.feesVnd)} · Đã thanh toán:{" "}
            {money(student.paidVnd)} · Còn nợ: {money(student.debtVnd)}
          </p>
          <p>
            Phân bổ gộp: {money(student.allocatedVnd)} · Tiền giữ để đối chiếu:{" "}
            {money(student.heldVnd)}
          </p>
          <ul>
            {student.allocations.map((a) => (
              <li key={a.receiptId}>
                {a.receivedOn} · Phân bổ vào kỳ: {money(a.amountVnd)}
                {a.note && ` · ${a.note}`} ·{" "}
                <button
                  type="button"
                  onClick={() =>
                    document
                      .getElementById(`receipt-${a.receiptId}`)
                      ?.scrollIntoView({ block: "center" })
                  }
                >
                  Khoản nhận {a.receiptId}
                </button>
              </li>
            ))}
          </ul>
          {student.allocations.length === 0 && (
            <p>Chưa có khoản nhận phân bổ vào kỳ.</p>
          )}
        </section>
      )}
    </section>
  );
}
