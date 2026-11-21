import { useId, useState } from "react";
import { ApiError } from "../api";

// Per-field validation messages (GOV.UK error pattern): the message sits next to the field it is about, is tied to it
// with aria-describedby and aria-invalid so a screen reader reads it with the field, and the first invalid field takes
// focus. The form keeps its summary `role="alert"` line; this adds the field-level part.

export type FieldErrors = Record<string, string>;

export function useFieldErrors() {
  const prefix = useId();
  const [errors, setErrors] = useState<FieldErrors>({});
  const idOf = (key: string) => `${prefix}${key}-error`;
  return {
    errors,
    clear: () => setErrors({}),
    /** Shows the messages and moves focus to the first invalid field of `form` (capture it before any await). */
    show(next: FieldErrors, form?: HTMLFormElement | null) {
      setErrors(next);
      if (form && Object.keys(next).length > 0)
        window.requestAnimationFrame(() =>
          form.querySelector<HTMLElement>('[aria-invalid="true"]')?.focus(),
        );
    },
    /** Spread on the input: marks it invalid and points at its message. */
    props: (key: string) =>
      errors[key]
        ? { "aria-invalid": true as const, "aria-describedby": idOf(key) }
        : {},
    /** The message element; place it after the field's label. */
    message: (key: string) =>
      errors[key] ? (
        <small id={idOf(key)} className="field-error">
          <b>Lỗi:</b> {errors[key]}
        </small>
      ) : null,
  };
}

/** The messages a failed request carries, keyed by field. `map` turns the server's key into the form's field key. */
export function apiFieldErrors(
  error: unknown,
  map: Record<string, string>,
): FieldErrors {
  if (!(error instanceof ApiError) || !error.errors) return {};
  const out: FieldErrors = {};
  for (const [key, messages] of Object.entries(error.errors)) {
    const field = map[key];
    if (field && messages.length) out[field] = messages.join(" ");
  }
  return out;
}
