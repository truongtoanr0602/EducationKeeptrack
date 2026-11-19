import { useEffect, useRef, useState } from "react";
import { money } from "./tuition-api";
import type { CashflowReport, PeriodStudent } from "./statistics-api";
import "./stats-charts.css";

const toNumber = (value: string) => Number(value);

/** 1.200.000 -> "1,2tr", 450.000 -> "450k": compact labels for axes and bars. */
export function compactVnd(value: number) {
  if (value >= 1_000_000) {
    const m = value / 1_000_000;
    return `${(Math.round(m * 10) / 10).toString().replace(".", ",")}tr`;
  }
  if (value >= 1_000) return `${Math.round(value / 1_000)}k`;
  return String(value);
}

function niceMax(value: number) {
  if (value <= 0) return 1;
  const exp = Math.pow(10, Math.floor(Math.log10(value)));
  const f = value / exp;
  const step = f <= 1 ? 1 : f <= 2 ? 2 : f <= 5 ? 5 : 10;
  return step * exp;
}

function useWidth<T extends HTMLElement>() {
  const ref = useRef<T>(null);
  const [width, setWidth] = useState(0);
  useEffect(() => {
    const el = ref.current;
    if (!el) return;
    const update = () => setWidth(el.clientWidth);
    update();
    if (typeof ResizeObserver === "undefined") return;
    const observer = new ResizeObserver(update);
    observer.observe(el);
    return () => observer.disconnect();
  }, []);
  return [ref, width] as const;
}

const dayMs = 86_400_000;
const parse = (iso: string) => Date.parse(`${iso}T00:00:00Z`);
const iso = (ms: number) => new Date(ms).toISOString().slice(0, 10);

type Bucket = { key: string; label: string; total: number; count: number };

/** Daily buckets for ranges up to ~2 months, otherwise one bucket per month. */
export function bucketReceipts(report: CashflowReport): Bucket[] {
  const start = parse(report.from);
  const end = parse(report.to);
  if (Number.isNaN(start) || Number.isNaN(end) || end < start) return [];
  const days = Math.round((end - start) / dayMs) + 1;
  const byDay = days <= 62;
  const buckets = new Map<string, Bucket>();
  const keyOf = (date: string) => (byDay ? date : date.slice(0, 7));
  const labelOf = (key: string) =>
    byDay
      ? `${key.slice(8)}/${key.slice(5, 7)}`
      : `${key.slice(5)}/${key.slice(2, 4)}`;
  for (let t = start; t <= end; t += byDay ? dayMs : 28 * dayMs) {
    const key = keyOf(iso(t));
    if (!buckets.has(key))
      buckets.set(key, { key, label: labelOf(key), total: 0, count: 0 });
  }
  const endKey = keyOf(report.to);
  if (!buckets.has(endKey))
    buckets.set(endKey, {
      key: endKey,
      label: labelOf(endKey),
      total: 0,
      count: 0,
    });
  for (const { receipt } of report.receipts) {
    const bucket = buckets.get(keyOf(receipt.receivedOn));
    if (!bucket) continue;
    bucket.total += toNumber(receipt.amountVnd);
    bucket.count += 1;
  }
  return [...buckets.values()].sort((a, b) => a.key.localeCompare(b.key));
}

export function CashflowChart({ report }: { report: CashflowReport }) {
  const [wrap, width] = useWidth<HTMLDivElement>();
  const [hover, setHover] = useState<number | null>(null);
  const buckets = bucketReceipts(report);
  const total = buckets.reduce((s, b) => s + b.total, 0);
  if (buckets.length === 0 || total === 0) {
    return (
      <div className="viz-root viz-empty">
        Chưa có khoản thực thu trong khoảng này để vẽ biểu đồ.
      </div>
    );
  }
  const height = 220;
  const pad = { top: 12, right: 8, bottom: 28, left: 44 };
  const w = Math.max(width, 240);
  const innerW = w - pad.left - pad.right;
  const innerH = height - pad.top - pad.bottom;
  const max = niceMax(Math.max(...buckets.map((b) => b.total)));
  const ticks = [0, max / 2, max];
  const slot = innerW / buckets.length;
  const barW = Math.min(28, Math.max(3, slot - 2));
  const labelEvery = Math.max(
    1,
    Math.ceil(buckets.length / Math.floor(innerW / 44)),
  );
  const y = (v: number) => pad.top + innerH - (v / max) * innerH;
  const active = hover === null ? null : buckets[hover];
  return (
    <figure className="viz-root" aria-label="Biểu đồ thực thu">
      <figcaption>
        <strong>
          Thực thu theo {buckets[0].key.length === 10 ? "ngày" : "tháng"}
        </strong>
        <span className="viz-legend">
          <i className="viz-swatch" /> Số tiền đã nhận
        </span>
      </figcaption>
      <div className="viz-plot" ref={wrap}>
        <svg
          width={w}
          height={height}
          role="img"
          aria-label={`Biểu đồ cột thực thu, tổng ${money(String(total))}. Số liệu chi tiết ở bảng bên dưới.`}
          onPointerLeave={() => setHover(null)}
        >
          {ticks.map((t) => (
            <g key={t}>
              <line
                x1={pad.left}
                x2={w - pad.right}
                y1={y(t)}
                y2={y(t)}
                className={t === 0 ? "viz-axis" : "viz-grid"}
              />
              <text
                x={pad.left - 6}
                y={y(t) + 4}
                className="viz-tick"
                textAnchor="end"
              >
                {compactVnd(t)}
              </text>
            </g>
          ))}
          {buckets.map((b, i) => {
            const cx = pad.left + slot * i + slot / 2;
            const h = (b.total / max) * innerH;
            return (
              <g key={b.key}>
                {hover === i && (
                  <rect
                    x={cx - slot / 2}
                    y={pad.top}
                    width={slot}
                    height={innerH}
                    className="viz-hover"
                  />
                )}
                {b.total > 0 && (
                  <path
                    className="viz-bar"
                    d={roundedTop(
                      cx - barW / 2,
                      y(b.total),
                      barW,
                      h,
                      Math.min(4, barW / 2),
                    )}
                  />
                )}
                {i % labelEvery === 0 && (
                  <text
                    x={cx}
                    y={height - 8}
                    className="viz-tick"
                    textAnchor="middle"
                  >
                    {b.label}
                  </text>
                )}
                <rect
                  x={cx - slot / 2}
                  y={pad.top}
                  width={slot}
                  height={innerH + 4}
                  fill="transparent"
                  onPointerEnter={() => setHover(i)}
                  onClick={() => setHover(i)}
                />
              </g>
            );
          })}
        </svg>
        {active && hover !== null && (
          <div
            className="viz-tooltip"
            role="presentation"
            style={{
              left: Math.min(
                Math.max(pad.left + slot * hover + slot / 2, 70),
                w - 70,
              ),
            }}
          >
            <strong>{active.label}</strong>
            <span>{money(String(active.total))}</span>
            <small>{active.count} khoản nhận</small>
          </div>
        )}
      </div>
    </figure>
  );
}

function roundedTop(x: number, y: number, w: number, h: number, r: number) {
  const rr = Math.min(r, h);
  return `M${x},${y + h}V${y + rr}Q${x},${y} ${x + rr},${y}H${x + w - rr}Q${x + w},${y} ${x + w},${y + rr}V${y + h}Z`;
}

export function PeriodChart({
  students,
  totalFees,
  totalPaid,
  totalDebt,
}: {
  students: PeriodStudent[];
  totalFees: string;
  totalPaid: string;
  totalDebt: string;
}) {
  const fees = toNumber(totalFees);
  const paid = toNumber(totalPaid);
  const debt = toNumber(totalDebt);
  if (students.length === 0 || fees <= 0) {
    return (
      <div className="viz-root viz-empty">
        Kỳ này chưa có học phí để vẽ biểu đồ.
      </div>
    );
  }
  const pct = Math.min(100, Math.round((Math.min(paid, fees) / fees) * 100));
  const rows = students
    .map((s) => ({
      id: s.studentId,
      name: s.name,
      fees: toNumber(s.feesVnd),
      paid: Math.min(toNumber(s.paidVnd), toNumber(s.feesVnd)),
      debt: toNumber(s.debtVnd),
    }))
    .sort((a, b) => b.debt - a.debt || b.fees - a.fees);
  const max = Math.max(
    ...rows.map((r) => Math.max(r.fees, r.paid + r.debt)),
    1,
  );
  return (
    <figure className="viz-root" aria-label="Biểu đồ công nợ theo học sinh">
      <figcaption>
        <strong>Tiến độ thu học phí kỳ này</strong>
        <span className="viz-legend">
          <i className="viz-swatch" /> Đã thanh toán
          <i className="viz-swatch viz-swatch-debt" /> Còn nợ
        </span>
      </figcaption>
      <div className="viz-hero">
        <span className="viz-hero-number">{pct}%</span>
        <span className="viz-hero-caption">
          đã thu · {money(String(paid))} / {money(String(fees))}
        </span>
      </div>
      <div
        className="viz-meter"
        role="img"
        aria-label={`Đã thu ${pct}% học phí, còn nợ ${money(String(debt))}`}
      >
        <span
          className="viz-fill-paid"
          style={{ width: `${(Math.min(paid, fees) / fees) * 100}%` }}
        />
        <span
          className="viz-fill-debt"
          style={{ width: `${(Math.min(debt, fees) / fees) * 100}%` }}
        />
      </div>
      <ul
        className="viz-rows"
        aria-label="Công nợ từng học sinh (nhiều nợ nhất trước)"
      >
        {rows.map((r) => (
          <li key={r.id} className="viz-row">
            <span className="viz-row-name" title={r.name}>
              {r.name}
            </span>
            <span className="viz-row-track">
              <span
                className="viz-fill-paid"
                style={{ width: `${(r.paid / max) * 100}%` }}
              />
              <span
                className="viz-fill-debt"
                style={{ width: `${(r.debt / max) * 100}%` }}
              />
            </span>
            <span className="viz-row-value">
              {r.debt > 0 ? `Nợ ${compactVnd(r.debt)}` : "Đã đủ"}
            </span>
          </li>
        ))}
      </ul>
    </figure>
  );
}
