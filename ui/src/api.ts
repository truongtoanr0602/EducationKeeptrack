export class ApiError extends Error {
  constructor(
    public status: number,
    message: string,
    public errors?: Record<string, string[]>,
  ) {
    super(message);
  }
}

export async function api<T>(
  path: string,
  method = "GET",
  body?: unknown,
): Promise<T> {
  const headers: Record<string, string> = {};
  if (method !== "GET") {
    const csrfResponse = await fetch("/api/auth/csrf", {
      credentials: "same-origin",
      cache: "no-store",
    });
    if (!csrfResponse.ok)
      throw new ApiError(
        csrfResponse.status,
        "Không thể mở phiên biểu mẫu. Vui lòng thử lại.",
      );
    const csrf: { token: string } = await csrfResponse.json();
    headers["X-CSRF-TOKEN"] = csrf.token;
    headers["Content-Type"] = "application/json";
  }
  const response = await fetch(`/api${path}`, {
    method,
    headers,
    body: body === undefined ? undefined : JSON.stringify(body),
    credentials: "same-origin",
    cache: "no-store",
  });
  if (!response.ok) {
    const error = (await response.json().catch(() => ({}))) as {
      message?: string;
      errors?: Record<string, string[]>;
    };
    throw new ApiError(
      response.status,
      error.message ??
        (response.status === 401
          ? "Phiên đã hết hạn. Vui lòng đăng nhập lại."
          : "Không thể hoàn tất yêu cầu. Vui lòng thử lại."),
      error.errors,
    );
  }
  return response.status === 204
    ? (undefined as T)
    : (response.json() as Promise<T>);
}

export type Access = {
  canRead: boolean;
  canWrite: boolean;
  canUseAi: boolean;
  status: "blocked" | "unverified" | "pending" | "active" | "expired";
};
export type Me = { email: string; access: Access };
export type TeacherClass = {
  id: string;
  name: string;
  subject: string;
  description: string | null;
};
export type ClassDraft = { name: string; subject: string; description: string };
export const limits = { name: 120, subject: 80, description: 500 };
