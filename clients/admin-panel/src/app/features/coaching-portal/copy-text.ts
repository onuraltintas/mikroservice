/** G5 (#45): what "Kopyala" did: copied to the clipboard, only selected on the page (no clipboard access), or neither. */
export type CopyResult = 'copied' | 'selected' | 'failed';

/**
 * G5 (#45): copies a short text (the parent link code) with the Clipboard API. Without it (an older browser, a page that is
 * not secure, a refused permission) the text in `element` is selected and copied with the older copy command (review L10);
 * when that fails too, the text stays selected so the student can copy it themselves.
 */
export async function copyText(text: string, element?: HTMLElement | null): Promise<CopyResult> {
  try {
    const clipboard = typeof navigator === 'undefined' ? undefined : navigator.clipboard;
    if (clipboard?.writeText) { await clipboard.writeText(text); return 'copied'; }
  } catch { /* falls back to selecting the text */ }
  if (!element || !selectText(element)) return 'failed';
  try {
    const doc = element.ownerDocument as Document & { execCommand?: (command: string) => boolean };
    if (typeof doc.execCommand === 'function' && doc.execCommand('copy')) return 'copied';
  } catch { /* the text stays selected */ }
  return 'selected';
}

/** Selects the text of an element (the fallback of {@link copyText}). */
export function selectText(element: HTMLElement): boolean {
  const selection = element.ownerDocument.defaultView?.getSelection();
  if (!selection) return false;
  const range = element.ownerDocument.createRange();
  range.selectNodeContents(element);
  selection.removeAllRanges(); selection.addRange(range);
  return true;
}

/** The status message shown after "Kopyala". */
export function copyMessage(result: CopyResult): string {
  return result === 'copied' ? 'Kod kopyalandı.'
    : result === 'selected' ? 'Kodu seçip kopyalayabilirsin.'
    : 'Kod kopyalanamadı. Kodu seçip kopyalayabilirsin.';
}
