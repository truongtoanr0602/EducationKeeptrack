import { useCallback, useEffect, useState } from "react";
import type { Me, TeacherClass } from "./api";
import { useAccess } from "./access";
import { CrossClass } from "./CrossClass";
import { today } from "./roster-api";
import { money } from "./tuition-api";
import {
  loadMonth,
  type AttendanceMonth,
  type MoneyMonth,
} from "./reports-api";
import { classHref } from "./router";

const notReady: Record<string, string> = {
  pending: "đang cập nhật học phí",
  "missing-rate": "thiếu đơn giá",
  "needs-review": "có tiền cần đối chiếu",
};

// "Báo cáo": the month at a glance. Drafting and exporting the PDF per student stays one step away.
export function MonthReport({
  me,
  classes,
}: {
  me: Me;
  classes: TeacherClass[];
}) {
  const { denied } = useAccess(me);
  const [month, setMonth] = useState(() => today().slice(0, 7));
  const [data, setData] = useState<[AttendanceMonth, MoneyMonth] | null>(null);
  const [error, setError] = useState("");
  const load = useCallback(async () => {
    setData(null);
    try {
      setData(await loadMonth(month));
      setError("");
    } catch (e) {
      setError(e instanceof Error ? e.message : "Không thể tải báo cáo.");
      denied(e);
    }
  }, [month, denied]);
  useEffect(() => {
    void load();
  }, [load]);
  const [attendance, moneyMonth] = data ?? [];
  const rate =
    attendance?.attendanceRate == null
      ? "—"
      : `${Math.round(attendance.attendanceRate * 100)}%`;
  const warnings = (moneyMonth?.classes ?? []).filter(
    (c) => c.status !== "ready",
  );
  return (
    <>
      <div className="page-heading compact">
        <div>
          <span className="eyebrow">BÁO CÁO THÁNG</span>
          <h1>Báo cáo</h1>
        </div>
      </div>
      <label className="field">
        Tháng
        <input
          type="month"
          value={month}
          onChange={(e) => e.target.value && setMonth(e.target.value)}
        />
      </label>
      {error && (
        <p role="alert" className="error">
          {error}
        </p>
      )}
      {!data && !error && <p role="status">Đang tải…</p>}
      {data && attendance && moneyMonth && (
        <>
          <ul className="report-cards" aria-label="Số liệu tháng">
            <li>
              <span>Buổi đã dạy</span>
              <strong>{attendance.held}</strong>
              <small>
                {attendance.planned} dự kiến · {attendance.cancelled} đã hủy
              </small>
            </li>
            <li>
              <span>Chuyên cần</span>
              <strong>{rate}</strong>
              <small>
                {attendance.present} có mặt · {attendance.absent} vắng
              </small>
            </li>
            <li>
              <span>Đã thu trong tháng</span>
              <strong>{money(moneyMonth.receivedVnd)}</strong>
              <small>Học phí phát sinh {money(moneyMonth.feesVnd)}</small>
            </li>
            <li>
              <span>Còn nợ</span>
              <strong>{money(moneyMonth.debtVnd)}</strong>
              <small>Tất cả các kỳ, tính đến hôm nay</small>
            </li>
          </ul>
          {warnings.map((c) => (
            <p key={c.classId} className="notice">
              {c.className}: {notReady[c.status] ?? c.status}. Số liệu có thể
              chưa đầy đủ; mở{" "}
              <a href={classHref(c.classId, "tuition")}>chi tiết học phí</a>.
            </p>
          ))}
        </>
      )}
      <h2 className="report-sub">Báo cáo PDF gửi phụ huynh</h2>
      <CrossClass kind="reports" classes={classes} bare />
    </>
  );
}
