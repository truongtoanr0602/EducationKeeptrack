import React, { Suspense, lazy } from "react";
import ReactDOM from "react-dom/client";
import { App } from "./App";
import "./style.css";
import "./shell.css";
import "./account.css";
import "./density.css";

// Students open an exam by link (#/lam-bai/<token>) without an account (ADR 0006): that page never loads the
// teacher shell or asks who is signed in.
const ExamTake = lazy(() => import("./ExamTake").then((m) => ({ default: m.ExamTake })));
const examToken = /^#\/lam-bai\/([0-9a-f]{32})\/?$/i.exec(window.location.hash)?.[1];
window.addEventListener("hashchange", () => {
  const next = /^#\/lam-bai\/([0-9a-f]{32})\/?$/i.exec(window.location.hash)?.[1];
  if (next !== examToken) window.location.reload();
});
ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    {examToken ? (
      <Suspense fallback={<p role="status">Đang tải…</p>}>
        <ExamTake token={examToken} />
      </Suspense>
    ) : (
      <App />
    )}
  </React.StrictMode>,
);
