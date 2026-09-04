/**
 * Shared feedback contract used by every browser client.
 *
 * Keeping this type framework-agnostic means each Angular client can render
 * feedback with its own Material imports while exposing the same API to
 * feature code.
 */
export interface ToastOptions {
  title?: string;
  duration?: number;
  actionLabel?: string;
}

export interface ConfirmOptions {
  title?: string;
  confirmText?: string;
  cancelText?: string;
}

export interface PromptOptions {
  title?: string;
  confirmText?: string;
  cancelText?: string;
  placeholder?: string;
  multiline?: boolean;
}
