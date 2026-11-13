import { useEffect, useRef, useState } from "react";
import { api, ApiError } from "./api";
import { today } from "./roster-api";
import { assessmentStates, type StudentProgress } from "./learning-api";

const W = 640;
const H = 240;
const PAD = { left: 44, right: 16, top: 16, bottom: 36 };
const methodText: Record<string, string> = { manual: "Tự chấm" };

function shift(date: string, days: number) {
  const d = new Date(`${date}T00:00:00Z`);
  d.setUTCDate(d.getUTCDate() + days);
  return d.toISOString().slice(0, 10);
}

/** Two independent series, one per indicator; gaps (not assigned / not assessed) are never drawn as zero. */
export function Progress({
  classId,
  students,
  onDenied,
}: {
  classId: string;
  students: { id: string; name: string }[];
  onDenied: (e: ApiError) => void;
}) {
  const [studentId, setStudentId] = useState("");
  const [to, setTo] = useState(today());
  const [from, setFrom] = useState(() => shift(today(), -90));
  const [data, setData] = useState<StudentProgress | null>(null);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const request = useRef(0);
  const chosen = students.some((x) => x.id === studentId)
    ? studentId
    : (students[0]?.id ?? "");

  useEffect(() => {
    if (!chosen) {
      setData(null);
      return;
    }
    const current = ++request.current;
    setBusy(true);
    setError("");
    api<StudentProgress>(
      `/learning/classes/${classId}/assessments/progress/${chosen}?from=${from}&to=${to}`,
    )
      .then((result) => {
        if (current === request.current) setData(result);
      })
      .catch((e) => {
        if (current !== request.current) return;
        setData(null);
        setError(
          e instanceof ApiError
            ? Object.values(e.errors ?? {})
                .flat()
                .join(" ") || e.message
            : "Không thể tải biểu đồ.",
        );
        if (e instanceof ApiError) onDenied(e);
      })
      .finally(() => {
        if (current === request.current) setBusy(false);
      });
    return () => {
      request.current++;
    };
  }, [classId, chosen, from, to, onDenied]);

  const points = data?.points ?? [];
  const x = (i: number) =>
    points.length < 2
      ? (PAD.left + W - PAD.right) / 2
      : PAD.left + (i * (W - PAD.left - PAD.right)) / (points.length - 1);
  const y = (v: number) =>
    PAD.top + ((100 - v) * (H - PAD.top - PAD.bottom)) / 100;
  // Break a line wherever a point is missing instead of bridging a gap.
  const segments = (key: "completion" | "correctness") => {
    const runs: { i: number; v: number }[][] = [];
    let run: { i: number; v: number }[] = [];
    points.forEach((p, i) => {
      const v = p.plotted ? p[key] : null;
      if (v == null) {
        if (run.length) runs.push(run);
        run = [];
      } else run.push({ i, v });
    });
    if (run.length) runs.push(run);
    return runs;
  };
  const series = [
    { key: "completion" as const, label: "Mức hoàn thành", color: "#2563eb", dash: undefined },
    { key: "correctness" as const, label: "Mức làm đúng", color: "#c2410c", dash: "6 4" },
  ];

  return (
    <section aria-label="Tiến độ học sinh" className="progress-panel">
      <h3>Tiến độ học sinh</h3>
      <p>
        Hai chỉ số độc lập từ kết quả đã xác nhận. Mỗi mức (25/50/75/100%) là
        nhóm giáo viên chọn, không phải tỷ lệ chính xác.
      </p>
      <label>
        Học sinh
        <select value={chosen} onChange={(e) => setStudentId(e.target.value)}>
          {!students.length && <option value="">Chưa có học sinh</option>}
          {students.map((s) => (
            <option key={s.id} value={s.id}>
              {s.name}
            </option>
          ))}
        </select>
      </label>
      <label>
        Từ ngày
        <input
          type="date"
          value={from}
          max={to}
          onChange={(e) => e.target.value && setFrom(e.target.value)}
        />
      </label>
      <label>
        Đến ngày
        <input
          type="date"
          value={to}
          min={from}
          onChange={(e) => e.target.value && setTo(e.target.value)}
        />
      </label>
      {busy && <p role="status">Đang tải biểu đồ…</p>}
      {error && (
        <p role="alert" className="error">
          {error}
        </p>
      )}
      {data && (
        <>
          <p data-testid="progress-completeness">
            Đã vẽ {data.completeness.plotted}/{data.completeness.total} bài
            {data.completeness.notAssigned > 0 &&
              ` · ${data.completeness.notAssigned} bài không giao`}
            {data.completeness.unconfirmed > 0 &&
              ` · ${data.completeness.unconfirmed} bài chưa xác nhận`}
            {data.completeness.unassessed > 0 &&
              ` · ${data.completeness.unassessed} bài chưa đánh giá`}
          </p>
          {!data.enoughForTrend && (
            <p className="notice" data-testid="progress-insufficient">
              Chưa đủ dữ liệu để xem xu hướng: cần ít nhất 2 bài đã đánh giá.
              Thiếu dữ liệu không có nghĩa là học sinh sa sút.
            </p>
          )}
          <svg
            role="img"
            aria-label={`Biểu đồ mức hoàn thành và mức làm đúng của ${data.studentName}`}
            viewBox={`0 0 ${W} ${H}`}
            width="100%"
          >
            {[0, 25, 50, 75, 100].map((v) => (
              <g key={v}>
                <line
                  x1={PAD.left}
                  x2={W - PAD.right}
                  y1={y(v)}
                  y2={y(v)}
                  stroke="currentColor"
                  opacity={0.15}
                />
                <text
                  x={PAD.left - 6}
                  y={y(v) + 4}
                  textAnchor="end"
                  fontSize="11"
                  fill="currentColor"
                >
                  {v}%
                </text>
              </g>
            ))}
            {points.map((p, i) => (
              <text
                key={p.assessmentId}
                x={x(i)}
                y={H - 12}
                textAnchor="middle"
                fontSize="10"
                fill="currentColor"
              >
                {p.date.slice(5)}
              </text>
            ))}
            {points.map((p, i) =>
              p.plotted ? null : (
                <line
                  key={p.assessmentId}
                  x1={x(i)}
                  x2={x(i)}
                  y1={PAD.top}
                  y2={H - PAD.bottom}
                  stroke="currentColor"
                  opacity={0.25}
                  strokeDasharray="2 3"
                />
              ),
            )}
            {series.map((s) => (
              <g key={s.key} stroke={s.color} fill={s.color}>
                {segments(s.key).map((run) => (
                  <polyline
                    key={run[0].i}
                    fill="none"
                    strokeWidth="2"
                    strokeDasharray={s.dash}
                    points={run.map((p) => `${x(p.i)},${y(p.v)}`).join(" ")}
                  />
                ))}
                {points.map((p, i) =>
                  p.plotted && p[s.key] != null ? (
                    <circle
                      key={p.assessmentId}
                      cx={x(i)}
                      cy={y(p[s.key] as number)}
                      r="4"
                    />
                  ) : null,
                )}
              </g>
            ))}
          </svg>
          <ul className="legend" aria-label="Chú thích">
            {series.map((s) => (
              <li key={s.key}>
                <span style={{ color: s.color }}>{s.dash ? "╌╌" : "──"}</span>{" "}
                {s.label}
              </li>
            ))}
            <li>┆ Bài không tính vào biểu đồ (không giao/chưa đánh giá)</li>
          </ul>
          <table>
            <caption>Số liệu của biểu đồ</caption>
            <thead>
              <tr>
                <th>Ngày</th>
                <th>Bài đánh giá</th>
                <th>Cách chấm</th>
                <th>Trạng thái</th>
                <th>Mức hoàn thành</th>
                <th>Mức làm đúng</th>
              </tr>
            </thead>
            <tbody>
              {points.map((p) => (
                <tr key={p.assessmentId}>
                  <td>{p.date}</td>
                  <td>{p.title}</td>
                  <td>{methodText[p.method] ?? p.method}</td>
                  <td title={p.reason}>
                    {p.confirmed ? assessmentStates[p.status] : "Chưa xác nhận"}
                  </td>
                  <td>{p.plotted ? `${p.completion}%` : "—"}</td>
                  <td>{p.plotted ? `${p.correctness}%` : "—"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </>
      )}
    </section>
  );
}
