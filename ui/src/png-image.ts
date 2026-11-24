/** Image types a QR upload accepts. The server stores and shows PNG only, so anything else is converted first. */
export const QR_ACCEPT = "image/png,image/jpeg,image/webp";
const CONVERTIBLE = new Set(["image/jpeg", "image/webp"]);
/** A phone photo can be large; the picture is only read here, never stored as it came. */
export const MAX_SOURCE_BYTES = 10 * 1024 * 1024;

const readBase64 = (blob: Blob) =>
  new Promise<string>((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(String(reader.result).split(",")[1]);
    reader.onerror = reject;
    reader.readAsDataURL(blob);
  });

export class ImageRejected extends Error {}

/**
 * Base64 of a PNG for `file`. A PNG is passed through byte for byte. A JPEG or WebP is drawn on a white canvas
 * (neither has transparency to keep), shrunk to `maxSide` pixels on its longer side and saved as PNG: a QR code is flat
 * black and white, so PNG keeps its edges sharp. Throws ImageRejected for any other type or an unreadable image.
 */
export async function toPngBase64(file: File, maxSide = 1024) {
  if (file.type === "image/png") return readBase64(file);
  if (!CONVERTIBLE.has(file.type) || file.size > MAX_SOURCE_BYTES)
    throw new ImageRejected("Chọn ảnh PNG, JPG hoặc WebP.");
  let bitmap: ImageBitmap;
  try {
    bitmap = await createImageBitmap(file, { imageOrientation: "from-image" });
  } catch {
    throw new ImageRejected("Không đọc được ảnh này.");
  }
  const scale = Math.min(1, maxSide / Math.max(bitmap.width, bitmap.height));
  const canvas = document.createElement("canvas");
  canvas.width = Math.max(1, Math.round(bitmap.width * scale));
  canvas.height = Math.max(1, Math.round(bitmap.height * scale));
  const context = canvas.getContext("2d");
  if (!context) throw new ImageRejected("Không đọc được ảnh này.");
  context.fillStyle = "#fff";
  context.fillRect(0, 0, canvas.width, canvas.height);
  context.drawImage(bitmap, 0, 0, canvas.width, canvas.height);
  bitmap.close();
  const png = await new Promise<Blob | null>((resolve) =>
    canvas.toBlob(resolve, "image/png"),
  );
  if (!png) throw new ImageRejected("Không đọc được ảnh này.");
  return readBase64(png);
}

/** Decoded size of a base64 string, to check the limit after a conversion. */
export const base64Bytes = (data: string) =>
  Math.floor((data.length * 3) / 4) -
  (data.endsWith("==") ? 2 : data.endsWith("=") ? 1 : 0);
