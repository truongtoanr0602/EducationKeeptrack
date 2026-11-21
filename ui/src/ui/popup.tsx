import { useEffect, useState, type ReactNode } from "react";
import * as Dialog from "@radix-ui/react-dialog";
import { Drawer } from "vaul";
import { X } from "lucide-react";
import { cn } from "./cn";

// A form that opens over the page instead of being appended to it. One component, two presentations:
// a centred dialog or a right-hand panel on a desktop, a bottom sheet on a phone.
//
// Behaviour that the screens depend on:
// - `locked`: nothing can close it (a request may have committed and must be retried, not abandoned).
// - `suspended`: it is hidden and stops blocking the page (the session expired and the sign-in form shows).
//   It stays mounted, so the typed input survives.
// - Clicking outside never closes it: a stray click must not discard a half-written form.
// - `footer` lives inside the popup because everything else on the page is inert while it is open.

const desktopQuery = "(min-width: 768px)";

function useDesktop() {
  const [desktop, setDesktop] = useState(() =>
    typeof window === "undefined"
      ? true
      : window.matchMedia(desktopQuery).matches,
  );
  useEffect(() => {
    const query = window.matchMedia(desktopQuery);
    const update = () => setDesktop(query.matches);
    query.addEventListener("change", update);
    return () => query.removeEventListener("change", update);
  }, []);
  return desktop;
}

export type PopupProps = {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  description?: string;
  variant?: "center" | "side";
  /** A wider centred dialog, for content such as a document preview. */
  wide?: boolean;
  locked?: boolean;
  suspended?: boolean;
  footer?: ReactNode;
  children: ReactNode;
};

const overlay = "popup-overlay fixed inset-0 z-[60] bg-slate-900/45";
const titleClass = "pr-9 text-lg font-semibold leading-snug text-slate-900";
const descriptionClass = "mt-1 text-sm text-slate-500";

export function Popup({
  open,
  onOpenChange,
  title,
  description,
  variant = "center",
  wide = false,
  locked = false,
  suspended = false,
  footer,
  children,
}: PopupProps) {
  const desktop = useDesktop();
  const hidden = suspended ? { display: "none" } : undefined;
  const body = (
    <>
      {description && <p className={descriptionClass}>{description}</p>}
      <div className="popup-body mt-4">{children}</div>
      {footer && (
        <div
          className={cn(
            "mt-4 flex flex-wrap items-center gap-2 border-t border-slate-200 pt-3",
            // Only a preview keeps its action in reach: on a form the sticky bar would sit over the fields and buttons.
            wide && "sticky bottom-0 bg-white pb-1",
          )}
        >
          {footer}
        </div>
      )}
    </>
  );
  if (!desktop)
    return (
      <Drawer.Root
        open={open}
        onOpenChange={onOpenChange}
        dismissible={!locked}
        modal={!suspended}
        handleOnly
      >
        <Drawer.Portal>
          <Drawer.Overlay className={overlay} style={hidden} />
          <Drawer.Content
            className="fixed inset-x-0 bottom-0 z-[61] flex max-h-[92dvh] flex-col rounded-t-2xl bg-white shadow-2xl outline-none"
            style={hidden}
            aria-describedby={undefined}
            onInteractOutside={(event) => event.preventDefault()}
          >
            <Drawer.Handle className="mt-3 mb-1" />
            <div className="overflow-y-auto px-5 pt-2 pb-8">
              <Drawer.Title className={titleClass}>{title}</Drawer.Title>
              {body}
            </div>
          </Drawer.Content>
        </Drawer.Portal>
      </Drawer.Root>
    );
  return (
    <Dialog.Root open={open} onOpenChange={onOpenChange} modal={!suspended}>
      <Dialog.Portal>
        <Dialog.Overlay className={overlay} style={hidden} />
        <Dialog.Content
          className={cn(
            "fixed z-[61] overflow-y-auto bg-white p-6 shadow-2xl outline-none",
            variant === "side"
              ? "popup-side top-0 right-0 h-full w-[min(100vw,30rem)] border-l border-slate-200"
              : "popup-center top-1/2 left-1/2 max-h-[90vh] -translate-x-1/2 -translate-y-1/2 rounded-2xl",
            wide ? "w-[min(96vw,62rem)]" : "w-[min(92vw,34rem)]",
          )}
          style={hidden}
          aria-describedby={undefined}
          onInteractOutside={(event) => event.preventDefault()}
          onEscapeKeyDown={(event) => locked && event.preventDefault()}
        >
          <Dialog.Title className={titleClass}>{title}</Dialog.Title>
          {!locked && (
            <Dialog.Close
              aria-label="Đóng"
              className="absolute top-4 right-4 inline-flex size-8 items-center justify-center rounded-full border-0 bg-transparent p-0 text-slate-500 hover:bg-slate-100 hover:text-slate-900"
            >
              <X className="size-4" aria-hidden="true" />
            </Dialog.Close>
          )}
          {body}
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  );
}
