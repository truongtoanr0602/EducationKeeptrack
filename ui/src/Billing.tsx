import { useCallback, useEffect, useState } from "react";
import type { Me, TeacherClass } from "./api";
import { useAccess } from "./access";
import { loadDebts, type DebtItem, type DebtsView } from "./billing-api";
import { ReceiptForm } from "./ReceiptForm";
import { today } from "./roster-api";
import { money } from "./tuition-api";
import { classHref } from "./router";
import { shareMonthlyReport } from "./share-report";

const PAGE_SIZE = 20;
const notReady: Record<string, string> = {
  pending: "đang cập nhật học phí",
  "missing-rate": "thiếu đơn giá",
  "needs-review": "có tiền cần đối chiếu",
};

// "Hóa đơn": who still owes tuition across every class, with a one-step way to record money received.
// Corrections, reserves and QR instructions stay on the class tuition screen, linked from each row.
export function Billing({ me, classes }: { me: Me; classes: TeacherClass[] }) {
  const { canWrite, denied } = useAccess(me);
  const [month, setMonth] = useState(() => today().slice(0, 7));
  const [classId, setClassId] = useState("");
  const [page, setPage] = useState(1);
  const [view, setView] = useState<DebtsView | null>(null);
  const [error, setError] = useState("");
  const [openKey, setOpenKey] = useState<string | null>(null);
  const [sharing, setSharing] = useState<string | null>(null);
  const [shareNote, setShareNote] = useState<Record<string, string>>({});
  const load = useCallback(async () => {
    try {
      setView(await loadDebts(month, classId, page, PAGE_SIZE));
      setError("");
    } catch (e) {
      setError(e instanceof Error ? e.message : "Không thể tải công nợ.");
      denied(e);
    }
  }, [month, classId, page, denied]);
  useEffect(() => {
    void load();
  }, [load]);

  async function share(item: DebtItem, key: string) {
    setSharing(key);
    setShareNote((n) => ({ ...n, [key]: "" }));
    try {
      const result = await shareMonthlyReport(
        item.classId,
        item.studentId,
        month,
        item.name,
      );
      setShareNote((n) => ({
        ...n,
        [key]:
          result === "shared"
            ? "Đã mở bảng chia sẻ."
            : result === "downloaded"
              ? "Đã tải PDF về máy."
              : "",
      }));
    } catch (e) {
      setShareNote((n) => ({
        ...n,
        [key]: e instanceof Error ? e.message : "Không thể chia sẻ báo cáo.",
      }));
      denied(e);
    } finally {
      setSharing(null);
    }
  }
  const warnings = (view?.classes ?? []).filter((c) => c.status !== "ready");
  const pages = view ? Math.max(1, Math.ceil(view.count / view.pageSize)) : 1;
  return (
    <>
      <div className="page-heading compact">
        <div>
          <span className="eyebrow">HỌC PHÍ</span>
          <h1>Hóa đơn</h1>
          <p>Học sinh còn nợ học phí ở mọi lớp.</p>
        </div>
      </div>
      <div className="billing-filters">
        <label className="field">
          Tháng
          <input
            type="month"
            value={month}
            onChange={(e) => {
              setMonth(e.target.value);
              setPage(1);
            }}
          />
        </label>
        <label className="field">
          Lớp
          <select
            value={classId}
            onChange={(e) => {
              setClassId(e.target.value);
              setPage(1);
            }}
          >
            <option value="">Tất cả lớp</option>
            {classes.map((c) => (
              <option key={c.id} value={c.id}>
                {c.name}
              </option>
            ))}
          </select>
        </label>
      </div>
      {error && (
        <p role="alert" className="error">
          {error}
        </p>
      )}
      {!view && !error && <p role="status">Đang tải…</p>}
      {view && (
        <>
          <p className="billing-total" role="status">
            Còn nợ <strong>{money(view.totalDebtVnd)}</strong> · {view.count}{" "}
            học sinh
          </p>
          {warnings.map((c) => (
            <p key={c.classId} className="notice">
              {c.className}: {notReady[c.status] ?? c.status}. Số liệu có thể
              chưa đầy đủ; mở{" "}
              <a href={classHref(c.classId, "tuition")}>chi tiết học phí</a> để
              kiểm tra.
            </p>
          ))}
          {view.items.length === 0 && (
            <section className="empty">
              <h2>Không có khoản nợ</h2>
              <p>Tất cả học sinh đã trả đủ học phí.</p>
            </section>
          )}
          <ul className="billing-list" aria-label="Học sinh còn nợ">
            {view.items.map((item) => {
              const key = `${item.classId}/${item.studentId}`;
              const open = openKey === key;
              const ready =
                view.classes.find((c) => c.classId === item.classId)?.status ===
                "ready";
              return (
                <li key={key} className="billing-card">
                  <div className="billing-row">
                    <span className="billing-who">
                      <strong>{item.name}</strong>
                      <span>{item.className}</span>
                    </span>
                    <span className="billing-debt">
                      {money(item.totalDebtVnd)}
                    </span>
                  </div>
                  <div className="billing-actions">
                    <button
                      aria-expanded={open}
                      disabled={!canWrite || !ready}
                      onClick={() => setOpenKey(open ? null : key)}
                    >
                      Ghi nhận đã thu
                    </button>
                    <button
                      disabled={sharing !== null}
                      onClick={() => void share(item, key)}
                    >
                      {sharing === key ? "Đang chuẩn bị…" : "Gửi báo cáo PDF"}
                    </button>
                    <a href={classHref(item.classId, "tuition")}>Chi tiết</a>
                  </div>
                  {shareNote[key] && <p role="status">{shareNote[key]}</p>}
                  {open && (
                    <ReceiptForm
                      classId={item.classId}
                      studentId={item.studentId}
                      version={item.version}
                      suggested={item.totalDebtVnd}
                      canWrite={canWrite && ready}
                      canReplay={canWrite}
                      onDenied={denied}
                      onSaved={() => {
                        setOpenKey(null);
                        void load();
                      }}
                    />
                  )}
                </li>
              );
            })}
          </ul>
          {pages > 1 && (
            <div className="billing-pager" role="group" aria-label="Phân trang">
              <button disabled={page <= 1} onClick={() => setPage(page - 1)}>
                ← Trước
              </button>
              <span>
                Trang {page}/{pages}
              </span>
              <button
                disabled={page >= pages}
                onClick={() => setPage(page + 1)}
              >
                Sau →
              </button>
            </div>
          )}
        </>
      )}
    </>
  );
}
