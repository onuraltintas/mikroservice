/**
 * Roadmap D4: keyboard for a `role="tablist"` of buttons. Arrow keys, Home and End move to the neighbouring tab,
 * focus it and select it (automatic activation, like a click).
 */
export function moveTabFocus(event: KeyboardEvent): void {
  const list = event.currentTarget as HTMLElement | null;
  const tabs = [...(list?.querySelectorAll<HTMLElement>('[role=tab]') ?? [])];
  if (!tabs.length) return;
  const at = Math.max(0, tabs.findIndex(tab => tab === document.activeElement || tab.getAttribute('aria-selected') === 'true'));
  const next = ({ ArrowRight: at + 1, ArrowDown: at + 1, ArrowLeft: at - 1, ArrowUp: at - 1, Home: 0, End: tabs.length - 1 } as Record<string, number>)[event.key];
  if (next === undefined) return;
  event.preventDefault();
  const tab = tabs[(next + tabs.length) % tabs.length];
  tab.focus(); tab.click();
}
