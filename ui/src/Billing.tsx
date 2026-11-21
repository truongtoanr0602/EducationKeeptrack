import { useEffect, useState } from "react";
import { api, type Me, type TeacherClass } from "./api";
import { useAccess } from "./access";
import { loadDebts, type DebtItem, type DebtsView } from "./billing-api";
import { useSwr } from "./swr";
import { ReceiptForm } from "./ReceiptForm";
import { today, type Student } from "./roster-api";
import { money } from "./tuition-api";
import { classHref } from "./router";
import { downloadFile, monthlyReportFile, zaloUrl } from "./share-report";

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
  const [openKey, setOpenKey] = useState<string | null>(null);
  const [sharing, setSharing] = useState<string | null>(null);
  const [shareNote, setShareNote] = useState<Record<string, string>>({});
  // The contact phone of a profile is the parent's number: it opens the right Zalo chat.
  const { data: students } = useSwr<Student[]>("students", () =>
    api<Student[]>("/students"),
  );
  const {
    data: view,
    error: failure,
    reload: load,
  } = useSwr<DebtsView>(`debts:${month}:${classId}:${page}`, () =>
    loadDebts(month, classId, page, PAGE_SIZE),
  );
  const error = failure
    ? failure instanceof Error
      ? failure.message
      : "Không thể tải công nợ."
    : "";
  useEffect(() => {
    if (failure) denied(failure);
  }, [failure, denied]);

  async function prepare(item: DebtItem, key: string, zalo: boolean) {
    setSharing(key);
    setShareNote((n) => ({ ...n, [key]: "" }));
    // A new tab can only be opened straight from the click, not after the PDF has been prepared.
    const tab = zalo ? window.open("about:blank", "_blank") : null;
    if (tab) tab.opener = null;
    try {
      const file = await monthlyReportFile(
        item.classId,
        item.studentId,
        month,
        item.name,
      );
      downloadFile(file);
      if (tab)
        tab.location.href = zaloUrl(
          students?.find((s) => s.id === item.studentId)?.phone,
        );
      setShareNote((n) => ({
        ...n,
        [key]: zalo
          ? "Đã tải PDF về máy. Đính kèm file này trong cuộc trò chuyện Zalo để gửi."
          : "Đã tải PDF về máy.",
      }));
    } catch (e) {
      tab?.close();
      setShareNote((n) => ({
        ...n,
        [key]: e instanceof Error ? e.message : "Không thể chuẩn bị báo cáo.",
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
                      onClick={() => void prepare(item, key, false)}
                    >
                      {sharing === key ? "Đang chuẩn bị…" : "Tải báo cáo PDF"}
                    </button>
                    <button
                      disabled={sharing !== null}
                      onClick={() => void prepare(item, key, true)}
                    >
                      Gửi qua Zalo
                    </button>
                    <a
                      className="button-link"
                      href={`${location.pathname}?reportClass=${item.classId}&reportStudent=${item.studentId}&reportMonth=${month}${classHref(item.classId, "reports")}`}
                    >
                      Xem trước PDF
                    </a>
                    <a
                      className="button-link"
                      href={classHref(item.classId, "tuition")}
                    >
                      Chi tiết
                    </a>
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
