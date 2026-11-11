import { useCallback, useState } from "react";
import { api, ApiError, type Me } from "./api";

// Shared handling of 401 (session expired → inline login, input kept) and 403 (refresh service access).
export function useAccess(me: Me) {
  const [access, setAccess] = useState(me.access);
  const [expired, setExpired] = useState(false);
  const denied = useCallback(
    (e: unknown) => {
      if (!(e instanceof ApiError)) return;
      if (e.status === 401) setExpired(true);
      if (e.status === 403)
        void api<Me>("/auth/me")
          .then((user) => setAccess(user.access))
          .catch(() =>
            setAccess({ ...me.access, canRead: false, canWrite: false, status: "blocked" }),
          );
    },
    [me.access],
  );
  const relogin = useCallback(async () => {
    const user = await api<Me>("/auth/me");
    setAccess(user.access);
    setExpired(false);
  }, []);
  return { access, expired, denied, relogin, canWrite: access.canWrite && !expired };
}
