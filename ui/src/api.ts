import { clearCache } from "./cache";

export class ApiError extends Error {
  constructor(
    public status: number,
    message: string,
    public errors?: Record<string, string[]>,
    public data: Record<string, unknown> = {},
  ) {
    super(message);
  }
}

// The antiforgery token is reused across writes: one round trip instead of two per write. It is tied
// to the signed-in user, so it is dropped on any /auth write (login, logout, password change…) and
// whenever the server says the form session expired.
let csrfToken: Promise<string> | null = null;
export function resetCsrf() {
  csrfToken = null;
}
function fetchCsrf(): Promise<string> {
  csrfToken ??= fetch("/api/auth/csrf", {
    credentials: "same-origin",
    cache: "no-store",
  })
    .then(async (response) => {
      if (!response.ok)
        throw new ApiError(
          response.status,
          "Không thể mở phiên biểu mẫu. Vui lòng thử lại.",
        );
      return ((await response.json()) as { token: string }).token;
    })
    .catch((e) => {
      csrfToken = null;
      throw e;
    });
  return csrfToken;
}
const csrfExpired = "Phiên biểu mẫu đã hết hạn. Vui lòng thử lại.";

async function send(
  path: string,
  method: string,
  body: unknown,
): Promise<Response> {
  const headers: Record<string, string> = {};
  if (method !== "GET") {
    headers["X-CSRF-TOKEN"] = await fetchCsrf();
    headers["Content-Type"] = "application/json";
  }
  return fetch(`/api${path}`, {
    method,
    headers,
    body: body === undefined ? undefined : JSON.stringify(body),
    credentials: "same-origin",
    cache: "no-store",
  });
}

export async function api<T>(
  path: string,
  method = "GET",
  body?: unknown,
): Promise<T> {
  let response = await send(path, method, body);
  if (response.status === 400 && method !== "GET") {
    // A stale token is rejected before the request runs, so one retry with a fresh token cannot repeat a write.
    const rejected = await response
      .clone()
      .json()
      .catch(() => ({}) as { message?: string });
    if ((rejected as { message?: string }).message === csrfExpired) {
      resetCsrf();
      response = await send(path, method, body);
    }
  }
  if (method !== "GET") {
    if (path.startsWith("/auth/")) resetCsrf();
    // Any write may change what cached reads show, so cached reads are dropped.
    if (response.ok) clearCache();
  }
  if (!response.ok) {
    const error = (await response.json().catch(() => ({}))) as {
      message?: string;
      errors?: Record<string, string[]>;
    } & Record<string, unknown>;
    throw new ApiError(
      response.status,
      error.message ??
        (response.status === 401
          ? "Phiên đã hết hạn. Vui lòng đăng nhập lại."
          : "Không thể hoàn tất yêu cầu. Vui lòng thử lại."),
      error.errors,
      error,
    );
  }
  return response.status === 204
    ? (undefined as T)
    : (response.json() as Promise<T>);
}

// Multipart upload of one file (Excel import, question images); same CSRF token and error shape as api().
export async function upload<T>(path: string, file: Blob, fileName: string): Promise<T> {
  const post = async () => {
    const form = new FormData();
    form.append("file", file, fileName);
    return fetch(`/api${path}`, {
      method: "POST",
      headers: { "X-CSRF-TOKEN": await fetchCsrf() },
      body: form,
      credentials: "same-origin",
      cache: "no-store",
    });
  };
  let response = await post();
  if (response.status === 400) {
    const rejected = (await response.clone().json().catch(() => ({}))) as { message?: string };
    if (rejected.message === csrfExpired) {
      resetCsrf();
      response = await post();
    }
  }
  if (response.ok) clearCache();
  if (!response.ok) {
    const error = (await response.json().catch(() => ({}))) as { message?: string; errors?: Record<string, string[]> } & Record<string, unknown>;
    throw new ApiError(response.status, error.message ?? "Không thể tải tệp lên. Vui lòng thử lại.", error.errors, error);
  }
  return (await response.json()) as T;
}

export type Access = {
  canRead: boolean;
  canWrite: boolean;
  canUseAi: boolean;
  status:
    "blocked" | "unverified" | "pending" | "rejected" | "active" | "expired";
};
export type Me = { email: string; role: "Teacher" | "Admin"; access: Access };
export type TeacherClass = {
  id: string;
  name: string;
  subject: string;
  description: string | null;
};
export type ClassDraft = { name: string; subject: string; description: string };
export const limits = { name: 120, subject: 80, description: 500 };
