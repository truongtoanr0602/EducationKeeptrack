export type QuotaBalance = {
  kind: string;
  granted: number | null;
  held: number;
  used: number;
  remaining: number;
};

export const kindLabels: Record<string, string> = {
  grading: "Chấm bài",
  comment: "Nhận xét",
  planning: "Kế hoạch",
};

// Turn types an Admin can grant per period; empty = not granted (no default counts).
export const quotaKinds = ["grading", "comment", "planning"] as const;

export function quotaBody(values: Record<string, string>) {
  return Object.fromEntries(
    quotaKinds.map((kind) => [
      kind,
      (values[kind] ?? "").trim() === "" ? null : Number(values[kind]),
    ]),
  );
}

export function QuotaInputs({
  prefix,
  values,
  onChange,
}: {
  prefix: string;
  values: Record<string, string>;
  onChange: (values: Record<string, string>) => void;
}) {
  return (
    <fieldset className="full-row quota-fields">
      <legend>Hạn mức AI của kỳ (để trống = chưa cấp)</legend>
      {quotaKinds.map((kind) => (
        <label key={kind}>
          {kindLabels[kind]}
          <input
            id={`${prefix}-${kind}`}
            type="number"
            min={0}
            max={100000}
            value={values[kind] ?? ""}
            onChange={(e) => onChange({ ...values, [kind]: e.target.value })}
          />
        </label>
      ))}
    </fieldset>
  );
}

export function QuotaTable({ quotas }: { quotas: QuotaBalance[] }) {
  return (
    <div className="table-wrap">
      <table className="data-table" aria-label="Hạn mức AI theo loại">
        <thead>
          <tr>
            <th>Loại lượt</th>
            <th>Được cấp</th>
            <th>Còn dùng</th>
            <th>Đang giữ</th>
            <th>Đã dùng</th>
          </tr>
        </thead>
        <tbody>
          {quotas.map((q) => (
            <tr key={q.kind}>
              <td>{kindLabels[q.kind] ?? q.kind}</td>
              <td>{q.granted ?? <span className="muted">Chưa cấp</span>}</td>
              <td>
                <strong>{q.remaining}</strong>
              </td>
              <td>{q.held}</td>
              <td>{q.used}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
