import { Component, input } from '@angular/core';

/** The way from sign-up to a working panel (20.11.2026): Hesap → Plan → Ödeme → Onay → Kurulum. */
export const START_STEPS = ['Hesap', 'Plan', 'Ödeme', 'Onay', 'Kurulum'] as const;

/**
 * Stage A of the institution's way (claude/maxkampus-kurum-sureci.md): where the owner is now and what comes next,
 * on /kayit and /plan. Step "Kurulum" is the panel's own guide.
 */
@Component({
  selector: 'mk-start-steps',
  standalone: true,
  template: `
    <ol class="steps" aria-label="Kurumunuzu açma adımları">
      @for (step of steps; track step; let i = $index) {
        <li [class.done]="i < current()" [class.now]="i === current()" [attr.aria-current]="i === current() ? 'step' : null">
          <span class="dot" aria-hidden="true">
            @if (i < current()) { <svg width="12" height="12" viewBox="0 0 12 12"><path d="M2.5 6.2 5 8.6l4.5-5" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/></svg> }
            @else { {{ i + 1 }} }
          </span>
          <span class="name">{{ step }}<span class="sr-only">{{ i < current() ? ', tamam' : i === current() ? ', şu an buradasınız' : '' }}</span></span>
        </li>
      }
    </ol>
  `,
  styles: `
    .steps { list-style: none; margin: 0 0 20px; padding: 0; display: flex; flex-wrap: wrap; gap: 6px 4px; align-items: center; }
    li { display: flex; align-items: center; gap: 6px; font-size: 14px; color: var(--ds-muted); }
    li + li::before { content: ''; width: 18px; height: 1px; background: var(--ds-field); margin-right: 2px; }
    .dot { display: inline-grid; place-items: center; width: 24px; height: 24px; border-radius: 50%; border: 1.5px solid var(--ds-field);
      background: var(--ds-surface); font-size: 12px; }
    li.done { color: var(--ds-text); }
    li.done .dot { background: var(--ds-primary); border-color: var(--ds-primary); color: var(--ds-on-primary); }
    li.now { color: var(--ds-text); font-weight: 600; }
    li.now .dot { border-color: var(--ds-primary); box-shadow: inset 0 0 0 1px var(--ds-primary); }
    .sr-only { position: absolute; width: 1px; height: 1px; overflow: hidden; clip: rect(0 0 0 0); white-space: nowrap; }
    @media (max-width: 420px) { li:not(.now) .name { position: absolute; width: 1px; height: 1px; overflow: hidden; clip: rect(0 0 0 0); } }
  `
})
export class StartStepsComponent {
  readonly current = input(0);
  readonly steps = START_STEPS;
}

/** Which step the plan page is on: choosing a plan, paying, waiting for approval, or done (the panel's guide). */
export function planStep(billing: { subscriptions: { isCurrent: boolean }[]; pendingOrders: { paidDeclaredAt: string | null }[] } | null): number {
  if (!billing) return 1;
  if (billing.subscriptions.some(item => item.isCurrent)) return 4;
  if (billing.pendingOrders.some(order => order.paidDeclaredAt)) return 3;
  return billing.pendingOrders.length ? 2 : 1;
}
