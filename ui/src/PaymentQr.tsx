import { useEffect, useRef, useState } from "react";
import { api, ApiError } from "./api";
import { Popup } from "./ui/popup";
import { money } from "./tuition-api";
import { BankPicker } from "./BankPicker";
import { SampleQr } from "./SampleQr";
import {
  accountErrors,
  digitsOnly,
  findBank,
  normalizeAccountName,
} from "./banks";

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
  const [saved, setSaved] = useState<Recipient | null>(null);
  // "Ngân hàng khác": a BIN the list does not know is typed and kept by hand.
  const [manual, setManual] = useState(false);
  const [image, setImage] = useState<string | null>(null);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [loaded, setLoaded] = useState(false);
  const [hasQr, setHasQr] = useState(false);
  function apply(r: Recipient) {
    setDraft(r);
    setSaved(r);
    setHasQr(r.hasStaticQr);
    setImage(null);
    setErrors({});
    // A saved bank the list does not know stays as it is, shown in the manual fields, never blanked.
    setManual(!findBank(r.bankBin));
    onChanged(r);
  }
  async function load() {
    setBusy(true);
    setError("");
    try {
      apply(await api<Recipient>("/billing/recipient"));
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
  const dirty =
    !saved ||
    saved.bankBin !== draft.bankBin ||
    saved.bankName !== draft.bankName ||
    saved.accountNo !== draft.accountNo ||
    saved.accountName !== draft.accountName.trim() ||
    image !== null;
  const locked = !canWrite || busy || !loaded;
  const fieldError = (key: string) =>
    errors[key] && (
      <small id={`recipient-${key}-error`} className="error">
        {errors[key]}
      </small>
    );
  return (
    <section aria-label="Tài khoản nhận tiền">
      <div className="card-heading">
        <h4>Tài khoản nhận tiền</h4>
        <button disabled={busy} onClick={() => void load()}>
          Tải lại tài khoản nhận tiền
        </button>
      </div>
      <p>
        Xem trước theo thông tin bạn nhập; chưa xác minh tên người nhận với ngân
        hàng.
      </p>
      <form
        className="roster-form"
        noValidate
        onSubmit={async (e) => {
          e.preventDefault();
          const found = accountErrors(draft);
          if (manual && !draft.bankName.trim())
            found.bankName = "Nhập tên ngân hàng.";
          setErrors(found);
          if (Object.keys(found).length) return;
          setBusy(true);
          setError("");
          try {
            apply(
              await api<Recipient>("/billing/recipient", "POST", {
                ...draft,
                bankName: draft.bankName.trim(),
                accountName: draft.accountName.trim(),
                staticQrBase64: image,
              }),
            );
          } catch (e) {
            setError(message(e));
            if (e instanceof ApiError) onDenied(e);
          } finally {
            setBusy(false);
          }
        }}
      >
        {manual ? (
          <>
            <label>
              Mã BIN ngân hàng
              <input
                inputMode="numeric"
                autoComplete="off"
                maxLength={6}
                aria-invalid={!!errors.bankBin || undefined}
                aria-describedby="recipient-bankBin-error"
                value={draft.bankBin}
                disabled={locked}
                onChange={(e) =>
                  setDraft({
                    ...draft,
                    bankBin: e.target.value.replace(/\D/g, "").slice(0, 6),
                  })
                }
              />
            </label>
            {fieldError("bankBin")}
            <label>
              Tên ngân hàng
              <input
                maxLength={100}
                aria-invalid={!!errors.bankName || undefined}
                aria-describedby="recipient-bankName-error"
                value={draft.bankName}
                disabled={locked}
                onChange={(e) =>
                  setDraft({ ...draft, bankName: e.target.value })
                }
              />
            </label>
            {fieldError("bankName")}
            <button
              type="button"
              disabled={locked}
              onClick={() => {
                setManual(false);
                setDraft({ ...draft, bankBin: "", bankName: "" });
              }}
            >
              Chọn ngân hàng từ danh sách
            </button>
          </>
        ) : (
          <>
            <BankPicker
              bin={draft.bankBin}
              disabled={locked}
              invalid={!!errors.bankBin}
              describedBy="recipient-bankBin-error"
              onPick={(bank) => {
                setDraft((d) => ({
                  ...d,
                  bankBin: bank.bin,
                  bankName: bank.shortName,
                }));
                setErrors((current) => ({ ...current, bankBin: "" }));
              }}
              onClear={() =>
                setDraft((d) => ({ ...d, bankBin: "", bankName: "" }))
              }
              onOther={() => {
                setManual(true);
                setDraft((d) => ({ ...d, bankBin: "", bankName: "" }));
              }}
            />
            {fieldError("bankBin")}
          </>
        )}
        <label>
          Số tài khoản
          <input
            inputMode="numeric"
            autoComplete="off"
            maxLength={19}
            aria-invalid={!!errors.accountNo || undefined}
            aria-describedby="recipient-accountNo-error"
            value={draft.accountNo}
            disabled={locked}
            onChange={(e) =>
              setDraft({ ...draft, accountNo: digitsOnly(e.target.value) })
            }
          />
        </label>
        {fieldError("accountNo")}
        <label>
          Tên chủ tài khoản
          <input
            autoComplete="off"
            autoCapitalize="characters"
            maxLength={50}
            aria-invalid={!!errors.accountName || undefined}
            aria-describedby="recipient-accountName-hint recipient-accountName-error"
            value={draft.accountName}
            disabled={locked}
            onChange={(e) =>
              setDraft({
                ...draft,
                accountName: normalizeAccountName(e.target.value),
              })
            }
          />
        </label>
        <small id="recipient-accountName-hint" className="hint">
          Tự chuyển thành chữ hoa không dấu, giống tên in trên tài khoản ngân
          hàng.
        </small>
        {fieldError("accountName")}
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
        <p>
          Xem trước người nhận: {draft.bankName} · {draft.accountNo} ·{" "}
          {draft.accountName.trim()}
        </p>
        <div className="form-actions">
          <button className="primary" disabled={locked}>
            Lưu tài khoản nhận tiền
          </button>
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
        </div>
      </form>
      {error && <p role="alert">{error}</p>}
      <SampleQr
        disabled={!canWrite || busy || !saved || dirty}
        reason={
          !saved
            ? "Lưu tài khoản nhận tiền trước khi tạo QR mẫu."
            : dirty
              ? "Bạn đã sửa thông tin chưa lưu. Lưu trước để QR mẫu dùng đúng tài khoản."
              : undefined
        }
        onDenied={onDenied}
      />
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
      {enabled && !recipient && (
        <p className="notice">
          Chưa có tài khoản nhận tiền.{" "}
          <a href="#/setup/2">Thiết lập tài khoản nhận tiền</a> để tạo QR.
        </p>
      )}
      {(busy || error || transfer) && (
        <Popup
          open
          title="Yêu cầu chuyển khoản"
          onOpenChange={(next) => {
            if (next) return;
            // Closing drops the code: a QR must never stay on screen after it may have gone stale.
            generation.current++;
            setTransfer(null);
            setError("");
            setBusy(false);
          }}
        >
          <section aria-label="Mã chuyển khoản" className="grid gap-2">
            {busy && <p role="status">Đang tạo mã…</p>}
            {error && <p role="alert">{error}</p>}
            {transfer && (
              <>
                <p>
                  {transfer.qrKind === "vietqr" && !imageFailed && !useStatic
                    ? "VietQR có số tiền và nội dung"
                    : (transfer.qrKind === "static" || useStatic) &&
                        !imageFailed
                      ? "QR tĩnh — tự nhập số tiền và nội dung"
                      : "Chuyển khoản thủ công — tự nhập đầy đủ thông tin"}
                </p>
                {transfer.qrKind !== "vietqr" || imageFailed || useStatic ? (
                  <p role="alert">
                    Không có VietQR dùng được. Kiểm tra người nhận, tự nhập đúng
                    số tiền và nội dung bên dưới.
                  </p>
                ) : null}
                <div className="vietqr-card">
                  <div className="vietqr-head">
                    <span className="vietqr-brand" aria-hidden="true">
                      <b className="viet">VIET</b>
                      <b className="qr">QR</b>
                    </span>
                    <small>Quét mã bằng ứng dụng ngân hàng</small>
                  </div>
                  {transfer.qrDataUrl && !imageFailed && (
                    <img
                      className="payment-qr"
                      src={
                        useStatic
                          ? transfer.staticQrDataUrl!
                          : transfer.qrDataUrl
                      }
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
                  <div className="vietqr-amount">
                    <span>Số tiền chuyển:</span>{" "}
                    <strong>{money(transfer.amountVnd)}</strong>
                  </div>
                  <div className="vietqr-info">
                    <p>
                      <span>Ngân hàng:</span> {transfer.bankName} (
                      {transfer.bankBin})
                    </p>
                    <p>
                      <span>Tài khoản:</span> {transfer.accountNo}
                    </p>
                    <p>
                      <span>Người nhận:</span> {transfer.accountName}
                    </p>
                    <p>
                      <span>Nội dung:</span> {transfer.content}
                    </p>
                  </div>
                </div>
                <p>
                  Quét QR không xác nhận đã nhận tiền. Chỉ ghi nhận sau khi kiểm
                  tra tài khoản ngân hàng.
                </p>
              </>
            )}
          </section>
        </Popup>
      )}
    </section>
  );
}
