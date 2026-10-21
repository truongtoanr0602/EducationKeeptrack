import { useEffect, useRef, useState } from "react";
import { api, ApiError } from "./api";
import type { Student } from "./roster-api";
import { reportPath, type ExportCommand, type MonthlyExport, type MonthlyPreview, type ReportDraft } from "./reporting-api";
import { ReportDraftForm } from "./ReportDraftForm";
import { ReportPreview } from "./ReportPreview";
import { ReportExports } from "./ReportExports";

export function MonthlyReports({ classId, students, canWrite, onDenied }: { classId: string; students: Student[]; canWrite: boolean; onDenied: (error: ApiError) => void }) {
  const query = new URLSearchParams(location.search);
  const ownQuery = query.get("reportClass") === classId;
  const [studentId, setStudentId] = useState(ownQuery ? query.get("reportStudent") ?? "" : "");
  const [month, setMonth] = useState(ownQuery && /^\d{4}-\d{2}$/.test(query.get("reportMonth") ?? "") ? query.get("reportMonth")! : new Date().toLocaleDateString("sv-SE", { timeZone: "Asia/Ho_Chi_Minh" }).slice(0, 7));
  const selected = students.some(s => s.id === studentId) ? studentId : students[0]?.id ?? "";
  useEffect(() => {
    if (!selected) return;
    const url = new URL(location.href); url.searchParams.set("reportClass", classId); url.searchParams.set("reportStudent", selected); url.searchParams.set("reportMonth", month);
    history.replaceState(null, "", url);
  }, [classId, selected, month]);
  return <section className="monthly-reports" aria-label="Báo cáo PDF tháng">
    <h3>Báo cáo PDF tháng</h3><p>Lưu nhận xét, kiểm tra nguồn và xuất báo cáo riêng cho từng học sinh.</p>
    <div className="report-scope">
      <label>Học sinh báo cáo<select value={selected} onChange={e => setStudentId(e.target.value)}>{students.map(s => <option value={s.id} key={s.id}>{s.name}</option>)}</select></label>
      <label>Tháng báo cáo<input type="month" value={month} min="0001-01" max="9998-12" onChange={e => { if (e.target.value) setMonth(e.target.value); }} /></label>
    </div>
    {selected ? <ReportEditor key={`${classId}/${selected}/${month}`} path={reportPath(classId, selected, month)} classId={classId} canWrite={canWrite} onDenied={onDenied} /> : <p>Thêm học sinh vào lớp để lập báo cáo.</p>}
  </section>;
}

function ReportEditor({ path, classId, canWrite, onDenied }: { path: string; classId: string; canWrite: boolean; onDenied: (error: ApiError) => void }) {
  const [draft, setDraft] = useState<ReportDraft | null>(null);
  const [saved, setSaved] = useState("");
  const [preview, setPreview] = useState<MonthlyPreview | null>(null);
  const [exports, setExports] = useState<MonthlyExport[]>([]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [uncertain, setUncertain] = useState(false);
  const command = useRef<ExportCommand | null>(null);
  const inFlight = useRef(false);
  const dirty = !!draft && JSON.stringify(draft) !== saved;
  useEffect(() => {
    let live = true;
    void Promise.all([api<ReportDraft>(path + "/draft"), api<MonthlyExport[]>(path + "/exports")]).then(([value, items]) => {
      if (live) { setDraft(value); setSaved(JSON.stringify(value)); setExports(items); }
    }).catch(e => { if (live) { setError(e instanceof Error ? e.message : "Không thể tải báo cáo."); if (e instanceof ApiError) onDenied(e); } });
    return () => { live = false; };
  }, [path, onDenied]);
  async function act(work: () => Promise<void>) {
    if (inFlight.current) return; inFlight.current = true; setBusy(true); setError(""); setNotice("");
    try { await work(); } catch (e) {
      setError(e instanceof ApiError && e.errors ? Object.values(e.errors).flat().join(" ") : e instanceof Error ? e.message : "Không thể hoàn tất báo cáo.");
      if (e instanceof ApiError) onDenied(e);
    } finally { inFlight.current = false; setBusy(false); }
  }
  async function save() {
    if (!draft || !canWrite) return;
    const result = await api<ReportDraft>(path + "/draft", "PUT", draft);
    setDraft(result); setSaved(JSON.stringify(result)); setPreview(null); setNotice("Đã lưu bản nháp báo cáo.");
  }
  async function capture() {
    if (dirty || uncertain) return;
    setPreview(await api<MonthlyPreview>(path + "/previews", "POST", {})); command.current = null;
  }
  async function exportPdf() {
    if (!preview || dirty) return;
    command.current ??= { previewId: preview.id, draftVersion: preview.draftVersion, idempotencyKey: crypto.randomUUID() };
    try {
      const item = await api<MonthlyExport>(path + "/exports", "POST", command.current);
      command.current = null; setUncertain(false); setExports(items => [item, ...items.filter(x => x.id !== item.id)]); setNotice("PDF đã lưu trong kho riêng tư.");
    } catch (e) {
      if (e instanceof ApiError && e.status === 409 && e.message !== "export-in-progress") { command.current = null; setUncertain(false); setPreview(null); }
      else setUncertain(true);
      throw e;
    }
  }
  async function download(id: string) {
    const link = await api<{ url: string }>(`${path}/exports/${id}/download-link`, "POST", {});
    const anchor = document.createElement("a"); anchor.href = link.url; anchor.download = ""; anchor.referrerPolicy = "no-referrer"; document.body.append(anchor); anchor.click(); anchor.remove();
  }
  if (!draft) return error ? <p role="alert" className="error">{error}</p> : <p>Đang tải bản nháp báo cáo…</p>;
  return <>
    {!canWrite && <p className="notice">Bản đã lưu vẫn được xem trước, xuất và tải. Nội dung báo cáo hiện chỉ đọc.</p>}
    {error && <p role="alert" className="error">{error}</p>}{notice && <p role="status">{notice}</p>}
    <ReportDraftForm draft={draft} disabled={!canWrite || busy || uncertain} dirty={dirty} onChange={value => { setDraft(value); setPreview(null); }} onSave={() => void act(save)} />
    {dirty && <p className="notice">Lưu nội dung trước khi xem trước hoặc xuất PDF.</p>}
    {uncertain && <p className="notice">Lệnh xuất chưa rõ kết quả. Thử lại cùng lệnh để tránh xuất trùng.</p>}
    <div className="roster-actions">
      <button disabled={busy || dirty || uncertain} onClick={() => void act(capture)}>Xem trước báo cáo</button>
      <button disabled={busy || dirty || !preview} onClick={() => void act(exportPdf)}>{uncertain ? "Thử lại lệnh xuất" : "Xuất PDF"}</button>
      <button onClick={() => { if (location.hash !== `#${classId}`) return; document.querySelector('[aria-label="Học phí theo buổi"]')?.scrollIntoView({ behavior: "smooth" }); }}>Đối chiếu học phí</button>
    </div>
    {preview && <ReportPreview preview={preview} path={path} />}
    <ReportExports items={exports} busy={busy} onDownload={id => void act(() => download(id))} />
  </>;
}
