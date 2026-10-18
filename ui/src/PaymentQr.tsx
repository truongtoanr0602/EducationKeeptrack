import { useEffect, useRef, useState } from "react";
import { api, ApiError } from "./api";
import { money } from "./tuition-api";

export type Recipient = {
  bankBin: string;
  bankName: string;
  accountNo: string;
  accountName: string;
  hasStaticQr: boolean;
  version: number;
};
type Transfer = Recipient & {
  amountVnd: string;
  content: string;
  qrKind: "vietqr" | "static" | "manual";
  qrDataUrl: string | null;
  staticQrDataUrl: string | null;
  recipientVersion: number;
};
function message(e: unknown) {
  return e instanceof ApiError
    ? Object.values(e.errors ?? {})
        .flat()
        .join(" ") || e.message
    : "Không thể kết nối. Vui lòng thử lại.";
}

export function RecipientSettings({
  canWrite,
  onDenied,
  onChanged,
}: {
  canWrite: boolean;
  onDenied: (e: ApiError) => void;
  onChanged: (r: Recipient | null) => void;
}) {
  const [draft, setDraft] = useState({
    bankBin: "",
    bankName: "",
    accountNo: "",
    accountName: "",
    version: 0,
  });
  const [image, setImage] = useState<string | null>(null);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [loaded, setLoaded] = useState(false);
  const [hasQr, setHasQr] = useState(false);
  async function load() {
    setBusy(true);
    setError("");
    try {
      const r = await api<Recipient>("/billing/recipient");
      setDraft(r);
      setHasQr(r.hasStaticQr);
      setImage(null);
      onChanged(r);
      setLoaded(true);
    } catch (e) {
      if (e instanceof ApiError && e.status === 404) {
        onChanged(null);
        setLoaded(true);
      } else {
        setError(message(e));
        if (e instanceof ApiError) onDenied(e);
      }
    } finally {
      setBusy(false);
    }
  }
  useEffect(() => {
    void load();
  }, []);
  return (
    <section aria-label="Tài khoản nhận tiền">
      <h4>Tài khoản nhận tiền</h4>
      <p>
        Xem trước theo thông tin bạn nhập; chưa xác minh tên người nhận với ngân
        hàng.
      </p>
      <button disabled={busy} onClick={() => void load()}>
        Tải lại tài khoản nhận tiền
      </button>
      <form
        onSubmit={async (e) => {
          e.preventDefault();
          setBusy(true);
          setError("");
          try {
            const r = await api<Recipient>("/billing/recipient", "POST", {
              ...draft,
              staticQrBase64: image,
            });
            setDraft(r);
            setHasQr(r.hasStaticQr);
            setImage(null);
            onChanged(r);
          } catch (e) {
            setError(message(e));
            if (e instanceof ApiError) onDenied(e);
          } finally {
            setBusy(false);
          }
        }}
      >
        {(
          [
            ["bankBin", "Mã BIN ngân hàng"],
            ["bankName", "Ngân hàng"],
            ["accountNo", "Số tài khoản"],
            ["accountName", "Tên người nhận (viết hoa không dấu)"],
          ] as const
        ).map(([key, label]) => (
          <label key={key}>
            {label}
            <input
              required
              value={draft[key]}
              disabled={!canWrite || busy || !loaded}
              onChange={(e) => setDraft({ ...draft, [key]: e.target.value })}
            />
          </label>
        ))}
        <label>
          QR ngân hàng tĩnh (PNG, tối đa 1 MB)
          <input
            type="file"
            accept="image/png"
            disabled={!canWrite || busy}
            onChange={async (e) => {
              const file = e.target.files?.[0];
              if (!file) return;
              if (file.size > 1048576 || file.type !== "image/png") {
                setError("Chọn ảnh PNG tối đa 1 MB.");
                e.target.value = "";
                return;
              }
              setBusy(true);
              try {
                const data = await new Promise<string>((resolve, reject) => {
                  const reader = new FileReader();
                  reader.onload = () =>
                    resolve(String(reader.result).split(",")[1]);
                  reader.onerror = reject;
                  reader.readAsDataURL(file);
                });
                setImage(data);
                setError("");
              } catch {
                setError("Không thể đọc ảnh.");
              } finally {
                setBusy(false);
              }
            }}
          />
        </label>
        {(hasQr || image) && (
          <p>
            Đã có QR tĩnh riêng tư. Khi đổi người nhận, hãy tải QR tương ứng.
          </p>
        )}
        {image && (
          <img
            className="payment-qr"
            src={`data:image/png;base64,${image}`}
            alt="Xem trước QR tĩnh"
          />
        )}
        <button
          type="button"
          disabled={!canWrite || busy}
          onClick={() => {
            setImage("");
            setHasQr(false);
          }}
        >
          Xóa QR tĩnh khi lưu
        </button>
        <p>
          Xem trước người nhận: {draft.bankName} · {draft.accountNo} ·{" "}
          {draft.accountName}
        </p>
        <button disabled={!canWrite || busy || !loaded}>
          Lưu tài khoản nhận tiền
        </button>
      </form>
      {error && <p role="alert">{error}</p>}
    </section>
  );
}

export function PaymentQr({
  classId,
  studentId,
  month,
  version,
  recipient,
  enabled,
  onDenied,
}: {
  classId: string;
  studentId: string;
  month: string;
  version: number;
  recipient: Recipient | null;
  enabled: boolean;
  onDenied: (e: ApiError) => void;
}) {
  const [transfer, setTransfer] = useState<Transfer | null>(null);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [imageFailed, setImageFailed] = useState(false);
  const [useStatic, setUseStatic] = useState(false);
  const generation = useRef(0);
  useEffect(() => {
    generation.current++;
    setTransfer(null);
    setError("");
    setBusy(false);
  }, [classId, studentId, month, version, recipient?.version, enabled]);
  // Revalidate while a request is visible; never keep a known stale QR on screen.
  useEffect(() => {
    if (!transfer) return;
    let active = true;
    const timer = window.setInterval(async () => {
      try {
        const [r, p] = await Promise.all([
          api<Recipient>("/billing/recipient"),
          api<{ version: number; status: string }>(
            `/billing/classes/${classId}/payments?month=${month}`,
          ),
        ]);
        if (
          active &&
          (r.version !== transfer.recipientVersion ||
            p.version !== transfer.version ||
            p.status !== "ready")
        ) {
          generation.current++;
          setTransfer(null);
          setError(
            "Mã cũ lỗi thời. Tải lại công nợ và tài khoản rồi tạo mã mới.",
          );
        }
      } catch {
        if (active) {
          setTransfer(null);
          setError("Không xác minh được mã hiện tại. Vui lòng tải lại.");
        }
      }
    }, 5000);
    return () => {
      active = false;
      window.clearInterval(timer);
    };
  }, [transfer, classId, month]);
  return (
    <section aria-label="Yêu cầu chuyển khoản">
      <button
        disabled={!enabled || busy}
        onClick={async () => {
          const current = ++generation.current;
          setBusy(true);
          setTransfer(null);
          setError("");
          setImageFailed(false);
          setUseStatic(false);
          try {
            const result = await api<Transfer>(
              `/billing/classes/${classId}/transfer-request`,
              "POST",
              {
                studentId,
                month,
                version,
                recipientVersion: recipient?.version ?? 0,
              },
            );
            if (current === generation.current) setTransfer(result);
          } catch (e) {
            if (current === generation.current) {
              setError(message(e));
              if (e instanceof ApiError) onDenied(e);
            }
          } finally {
            if (current === generation.current) setBusy(false);
          }
        }}
      >
        Tạo yêu cầu chuyển khoản
      </button>
      {busy && <p role="status">Đang tạo mã…</p>}
      {error && <p role="alert">{error}</p>}
      {transfer && (
        <>
          <p>
            {transfer.qrKind === "vietqr" && !imageFailed && !useStatic
              ? "VietQR có số tiền và nội dung"
              : (transfer.qrKind === "static" || useStatic) && !imageFailed
                ? "QR tĩnh — tự nhập số tiền và nội dung"
                : "Chuyển khoản thủ công — tự nhập đầy đủ thông tin"}
          </p>
          {transfer.qrKind !== "vietqr" || imageFailed || useStatic ? (
            <p role="alert">
              Không có VietQR dùng được. Kiểm tra người nhận, tự nhập đúng số
              tiền và nội dung bên dưới.
            </p>
          ) : null}
          {transfer.qrDataUrl && !imageFailed && (
            <img
              className="payment-qr"
              src={useStatic ? transfer.staticQrDataUrl! : transfer.qrDataUrl}
              alt={
                transfer.qrKind === "vietqr" && !useStatic
                  ? "VietQR chuyển khoản"
                  : "QR ngân hàng tĩnh"
              }
              onError={() => {
                if (
                  transfer.qrKind === "vietqr" &&
                  !useStatic &&
                  transfer.staticQrDataUrl
                )
                  setUseStatic(true);
                else setImageFailed(true);
              }}
            />
          )}
          <p>
            Ngân hàng: {transfer.bankName} ({transfer.bankBin})
          </p>
          <p>Tài khoản: {transfer.accountNo}</p>
          <p>Người nhận: {transfer.accountName}</p>
          <p>Số tiền chuyển: {money(transfer.amountVnd)}</p>
          <p>Nội dung: {transfer.content}</p>
          <p>
            Quét QR không xác nhận đã nhận tiền. Chỉ ghi nhận sau khi kiểm tra
            tài khoản ngân hàng.
          </p>
        </>
      )}
    </section>
  );
}
