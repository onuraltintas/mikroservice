const pendingTargets = new WeakMap<HTMLElement, { top: number; expiresAt: number }>();

export function scrollReadingAhead(container: HTMLElement, activeSelector: string): void {
  if (container.scrollHeight <= container.clientHeight + 1) return;
  const active = container.querySelector<HTMLElement>(activeSelector);
  if (!active) return;

  const bounds = container.getBoundingClientRect();
  const top = bounds.top + container.clientTop;
  const visibleTop = Math.max(0, top);
  const visibleBottom = Math.min(window.innerHeight, top + container.clientHeight);
  const visibleHeight = visibleBottom - visibleTop;
  if (visibleHeight <= 0) return;

  const activeBounds = active.getBoundingClientRect();
  const lineHeight = parseFloat(getComputedStyle(active).lineHeight) || activeBounds.height;
  const rowGap = active.parentElement ? parseFloat(getComputedStyle(active.parentElement).rowGap) || 0 : 0;
  const rowHeight = lineHeight + rowGap;
  if (rowHeight <= 0) return;

  let pending = pendingTargets.get(container);
  if (pending && (container.scrollTop >= pending.top - 1 || Date.now() >= pending.expiresAt)) {
    pendingTargets.delete(container);
    pending = undefined;
  }
  // Treat the requested position as reached while smooth scrolling, so timer
  // updates do not keep restarting the same animation.
  const effectiveScrollTop = Math.max(container.scrollTop, pending?.top ?? 0);
  const activeBottom = activeBounds.bottom - top + container.scrollTop;
  const bottom = effectiveScrollTop + visibleBottom - top;
  const bufferRows = Math.min(3, Math.floor(visibleHeight / rowHeight / 2));
  const remainingRows = Math.floor((bottom - activeBottom + 1) / rowHeight);
  if (remainingRows > bufferRows) return;

  // Keep one preceding line for context and reveal a new block of text.
  const target = Math.min(container.scrollHeight - container.clientHeight,
    Math.max(0, activeBounds.top - visibleTop + container.scrollTop - rowHeight));
  if (target <= effectiveScrollTop + 1) return;

  pendingTargets.set(container, { top: target, expiresAt: Date.now() + 1000 });
  container.scrollTo({ top: target,
    behavior: window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'instant' : 'smooth' });
}
