import { useCallback, useEffect, useRef, useState } from "react";
import { api, ApiError, type Me, type TeacherClass } from "./api";
import { today } from "./roster-api";
import { Login } from "./Login";
import { RateForm } from "./RateForm";
import { AdjustmentForm } from "./AdjustmentForm";
import { Payments } from "./Payments";
import { CashflowStatistics } from "./CashflowStatistics";
import {
  money,
  feeBasis,
  type RatePlan,
  type TuitionReport,
} from "./tuition-api";

export function Tuition({
  classroom,
  me,
  refreshToken,
}: {
  classroom: TeacherClass;
  me: Me;
  refreshToken: number;
}) {
  const [month, setMonth] = useState(
    () =>
      new URLSearchParams(window.location.search).get("tuitionMonth") ??
      today().slice(0, 7),
  );
  const [appliedMonth, setAppliedMonth] = useState(month);
  const [plan, setPlan] = useState<RatePlan>({ version: 0, rates: [] });
  const [report, setReport] = useState<TuitionReport | null>(null);
  const [access, setAccess] = useState(me.access);
  const [sessionExpired, setSessionExpired] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");
  const [expanded, setExpanded] = useState<string | null>(null);
  const request = useRef(0);
  const path = `/billing/classes/${classroom.id}`;
  const denied = useCallback((e: ApiError) => {
    if (e.status === 401) setSessionExpired(true);
    if (e.status === 403)
      void api<Me>("/auth/me")
        .then((user) => setAccess(user.access))
        .catch(() =>
          setAccess((current) => ({
            ...current,
            canRead: false,
            canWrite: false,
          })),
        );
  }, []);
  const load = useCallback(
    async (selectedMonth: string) => {
      const current = ++request.current;
      setLoading(true);
      setError("");
      try {
        const [rates, fees] = await Promise.all([
          api<RatePlan>(`${path}/rates`),
          api<TuitionReport>(
            `${path}/tuition?month=${encodeURIComponent(selectedMonth)}`,
          ),
        ]);
        if (current !== request.current) return;
        setPlan(rates);
        setReport(fees);
        setAppliedMonth(selectedMonth);
        const url = new URL(window.location.href);
        url.searchParams.set("tuitionMonth", selectedMonth);
        window.history.replaceState(null, "", url);
      } catch (e) {
        if (current !== request.current) return;
        setError(
          e instanceof ApiError
            ? Object.values(e.errors ?? {})
                .flat()
                .join(" ") || e.message
            : "Không thể tải bảng phí. Vui lòng thử lại.",
        );
        if (e instanceof ApiError) denied(e);
      } finally {
        if (current === request.current) setLoading(false);
      }
    },
    [path, denied],
  );
  useEffect(() => {
    void load(appliedMonth);
  }, [load, appliedMonth, refreshToken]);
  useEffect(() => {
    if (
      report?.status !== "pending" ||
      sessionExpired ||
      !access.canRead ||
      error
    )
      return;
    const timer = window.setTimeout(() => void load(appliedMonth), 1500);
    return () => window.clearTimeout(timer);
  }, [report, appliedMonth, sessionExpired, access.canRead, error, load]);
  useEffect(
    () => () => {
      request.current++;
    },
    [],
  );
  async function resume() {
    const user = await api<Me>("/auth/me");
    if (user.email !== me.email) {
      window.location.assign("/");
      return;
    }
    setAccess(user.access);
    setSessionExpired(false);
    await load(appliedMonth);
  }
  return (
    <section
      className="tuition-workspace roster"
      aria-label="Học phí theo buổi"
    >
      {sessionExpired && (
        <>
          <p className="notice">
            Phiên đã hết hạn. Đăng nhập lại cùng tài khoản để tiếp tục; đơn giá
            đang nhập vẫn được giữ trong trang này.
          </p>
          <Login onLogin={resume} />
        </>
      )}
      <div hidden={sessionExpired || !access.canRead}>
        <div className="page-heading">
          <div>
            <span className="eyebrow">HỌC PHÍ THỰC HỌC</span>
            <h2>Học phí theo buổi · {classroom.name}</h2>
            <p>
              Tính buổi đã diễn ra có mặt và điều chỉnh có lý do. Tổng học phí
              chưa trừ thanh toán.
            </p>
          </div>
        </div>
        {!access.canWrite && (
          <p className="notice">
            Quyền sử dụng đã hết hạn. Bạn chỉ có thể xem bảng phí đã lưu; dữ
            liệu nhập chưa lưu vẫn được giữ.
          </p>
        )}
        <RateForm
          classId={classroom.id}
          plan={plan}
          canWrite={access.canWrite && !loading}
          onDenied={denied}
          onSaved={(rates) => {
            setPlan(rates);
            setMessage("Đã lưu đơn giá.");
            void load(appliedMonth);
          }}
        />
        {message && (
          <p role="status" className="notice success">
            {message}
          </p>
        )}
        <ul aria-label="Đơn giá đã lưu">
          {plan.rates.map((rate) => (
            <li key={rate.id}>
              Từ {rate.effectiveFrom} · {money(rate.unitPriceVnd)}
            </li>
          ))}
        </ul>
        <div className="session-range">
          <label>
            Tháng học phí
            <input
              type="month"
              value={month}
              onChange={(e) => setMonth(e.target.value)}
            />
          </label>
          <button disabled={loading} onClick={() => void load(month)}>
            Tải lại bảng phí
          </button>
        </div>
        {loading && <p role="status">Đang tải bảng phí…</p>}
        {error && (
          <p className="error" role="alert">
            {error}
          </p>
        )}
        {report && (
          <>
            <p>
              Bảng phí tháng {report.month} · Phiên bản {report.version}
            </p>
            <p className="notice" role="status">
              {loading
                ? "Đang kiểm tra cập nhật bảng phí…"
                : error
                  ? "Chưa thể xác minh dữ liệu mới. Bảng dưới thuộc lần tải trước."
                  : report.status === "pending"
                    ? "Đang chờ cập nhật điểm danh/đơn giá. Số tiền đang hiển thị thuộc lần tổng hợp trước."
                    : report.status === "missing-rate"
                      ? "Chưa có đơn giá cho một số buổi thực học. Tổng đã tính chưa gồm các buổi đó."
                      : "Đã cập nhật bảng phí."}
            </p>
            <p
              className="tuition-total"
              role="status"
              aria-label="Tổng học phí"
            >
              {report.status === "ready" ? "Học phí phát sinh" : "Tổng đã tính"}
              : {money(report.totalVnd)}
            </p>
            {report.students.length === 0 && (
              <p>Chưa có học sinh trong lớp này.</p>
            )}
            <div className="tuition-students">
              {report.students.map((student) => (
                <article className="class-card" key={student.studentId}>
                  <h3>Học phí: {student.name}</h3>
                  <p>{money(student.totalVnd)}</p>
                  <button
                    aria-expanded={expanded === student.studentId}
                    onClick={() =>
                      setExpanded(
                        expanded === student.studentId
                          ? null
                          : student.studentId,
                      )
                    }
                  >
                    Chi tiết {student.name}
                  </button>
                  <div
                    className="tuition-table-wrap"
                    hidden={expanded !== student.studentId}
                  >
                    <table aria-label={`Căn cứ học phí ${student.name}`}>
                      <thead>
                        <tr>
                          <th>Buổi học</th>
                          <th>Căn cứ</th>
                          <th>Đơn giá</th>
                          <th>Phí buổi</th>
                        </tr>
                      </thead>
                      <tbody>
                        {student.lines.map((line) => (
                          <tr key={line.sessionId}>
                            <td>
                              {line.date} · {line.startTime.slice(0, 5)}
                            </td>
                            <td>{feeBasis(line)}</td>
                            <td>
                              {line.unitPriceVnd === null
                                ? "—"
                                : money(line.unitPriceVnd)}
                            </td>
                            <td>{money(line.amountVnd)}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                    <h4>Lịch sử điều chỉnh</h4>
                    <ul aria-label="Lịch sử điều chỉnh">
                      {student.adjustments.map((adjustment) => (
                        <li key={adjustment.id}>
                          {adjustment.month} ·{" "}
                          {adjustment.direction === "increase"
                            ? "Tăng"
                            : "Giảm"}{" "}
                          {money(adjustment.amountVnd)} · {adjustment.reason}
                          <p>
                            Người ghi: {adjustment.recordedBy} ·{" "}
                            {new Date(adjustment.recordedAt).toLocaleString(
                              "vi-VN",
                            )}{" "}
                            · Phiên bản {adjustment.version}
                          </p>
                        </li>
                      ))}
                    </ul>
                    {student.adjustments.length === 0 && (
                      <p>Chưa có điều chỉnh.</p>
                    )}
                    <AdjustmentForm
                      classId={classroom.id}
                      studentId={student.studentId}
                      month={report.month}
                      version={report.version}
                      canWrite={
                        access.canWrite &&
                        !sessionExpired &&
                        !loading &&
                        !error &&
                        report.status === "ready" &&
                        report.month === appliedMonth
                      }
                      canReplay={access.canWrite && !sessionExpired}
                      onDenied={denied}
                      onSaved={() => void load(appliedMonth)}
                    />
                    {student.lines.length === 0 && (
                      <p>Không có buổi học trong tháng.</p>
                    )}
                  </div>
                </article>
              ))}
            </div>
          </>
        )}
        <CashflowStatistics
          key={`statistics:${classroom.id}`}
          classId={classroom.id}
          month={appliedMonth}
          refreshToken={`${refreshToken}:${report?.version ?? 0}`}
          onDenied={denied}
        />
        <Payments
          key={classroom.id}
          classId={classroom.id}
          month={appliedMonth}
          refreshToken={`${refreshToken}:${report?.version ?? 0}`}
          canWrite={access.canWrite && !sessionExpired}
          onDenied={denied}
          onChanged={() => void load(appliedMonth)}
        />
      </div>
      {!access.canRead && !sessionExpired && (
        <p className="error" role="alert">
          Tài khoản hiện không được truy cập học phí.
        </p>
      )}
    </section>
  );
}
