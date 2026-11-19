/**
 * Material icon ligatures ("dashboard", "pause", "close") are text, so screen readers read them out and they end up
 * in link and button names ("dashboard Ana Sayfa"). Every icon is decorative here; buttons that show only an icon
 * carry their own aria-label. This marks all icons aria-hidden, including those rendered later.
 */
const ICON_SELECTOR = '.material-icons, .material-icons-outlined, .material-symbols-outlined, mat-icon';

export function hideIconFromAssistiveTech(element: Element): void {
  if (!element.hasAttribute('aria-hidden')) element.setAttribute('aria-hidden', 'true');
}

export function hideDecorativeIcons(root: ParentNode): void {
  if (root instanceof Element && root.matches(ICON_SELECTOR)) hideIconFromAssistiveTech(root);
  root.querySelectorAll(ICON_SELECTOR).forEach(hideIconFromAssistiveTech);
}

export function watchDecorativeIcons(doc: Document = document): MutationObserver | null {
  if (typeof MutationObserver === 'undefined' || !doc?.body) return null;
  hideDecorativeIcons(doc.body);
  const observer = new MutationObserver(mutations => {
    for (const mutation of mutations) {
      mutation.addedNodes.forEach(node => { if (node instanceof Element) hideDecorativeIcons(node); });
    }
  });
  observer.observe(doc.body, { childList: true, subtree: true });
  return observer;
}
