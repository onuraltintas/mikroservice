import { Component, EventEmitter, Input, Output } from '@angular/core';

export interface StaffSectionItem { value: string; label: string; testId?: string }
export interface StaffSectionGroup { label: string; items: StaffSectionItem[] }

/**
 * Roadmap O2: sections in labelled groups; on the phone the same choices are a single menu.
 * Roadmap D4: the buttons follow the ARIA tabs pattern: one tab stop, arrow keys / Home / End move and select,
 * each tab controls the workspace's panel (`<div role="tabpanel" [id]="panelId" ...>`).
 */
@Component({
  selector: 'staff-section-nav',
  standalone: true,
  template: `
    <div class="section-nav" role="tablist" [attr.aria-label]="label" (keydown)="move($event)">
      @for (group of groups; track group.label) {
        <div class="section-group" role="none">
          <span class="section-group-label" aria-hidden="true">{{ group.label }}</span>
          <div class="section-group-items" role="none">
            @for (item of group.items; track item.value) {
              <button type="button" role="tab" [id]="tabId(item.value)" [attr.data-testid]="item.testId ?? null"
                [attr.aria-selected]="active === item.value" [attr.aria-controls]="panelId" [attr.tabindex]="active === item.value ? 0 : -1"
                [class.active]="active === item.value" (click)="select.emit(item.value)">{{ item.label }}</button>
            }
          </div>
        </div>
      }
    </div>
    <label class="section-select"><span>{{ label }}</span>
      <select [value]="active" (change)="choose($event)">
        @for (group of groups; track group.label) {
          <optgroup [label]="group.label">
            @for (item of group.items; track item.value) { <option [value]="item.value" [selected]="active === item.value">{{ item.label }}</option> }
          </optgroup>
        }
      </select>
    </label>`,
  styles: [`
    /* B design (21.11.2026): soft card, pill tabs with a colour dot per group, the chosen tab graphite with an amber dot;
       tabs slide in once and lift under the pointer. Nothing moves with reduced motion. */
    :host { display: block; margin-top: 19px; }
    .section-nav { display: flex; flex-wrap: wrap; gap: 10px 18px; padding: 12px 14px; border: 0; border-radius: 16px; background: var(--ui-surface);
      box-shadow: var(--ds-shadow-raised, 0 1px 3px color-mix(in srgb, var(--ui-text) 10%, transparent)); }
    .section-group { display: flex; flex-direction: column; gap: 6px; --dot: var(--ds-info, var(--ui-brand)); }
    .section-group:nth-child(4n + 2) { --dot: var(--ui-success); }
    .section-group:nth-child(4n + 3) { --dot: var(--ds-violet, var(--ui-brand)); }
    .section-group:nth-child(4n + 4) { --dot: var(--ds-orange, var(--ui-warning)); }
    .section-group-label { display: inline-flex; align-items: center; gap: 6px; padding: 2px 8px 0; color: var(--ui-text-muted); font-size: 12px; font-weight: 800; letter-spacing: 0; }
    .section-group-label::before { content: ''; width: 8px; height: 8px; border-radius: 50%; background: var(--dot); }
    .section-group-items { display: flex; flex-wrap: wrap; gap: 6px; }
    /* Live test 16.11.2026 (#22): the tabs' look is set here, the same for every tab, whatever class attribute they carry. */
    button { min-height: 40px; padding: 8px 16px; border: 1px solid color-mix(in srgb, var(--ui-border) 70%, transparent); border-radius: 999px; color: var(--ui-text);
      background: var(--ui-surface); font: inherit; font-size: 14px; font-weight: 650; cursor: pointer;
      transition: transform .2s ease, background .2s ease, box-shadow .2s ease; animation: tab-in .45s cubic-bezier(.2, .8, .2, 1) both; }
    button:hover:not(.active) { transform: translateY(-2px); background: color-mix(in srgb, var(--dot) 10%, var(--ui-surface)); box-shadow: 0 6px 14px -8px var(--dot); }
    button.active { display: inline-flex; align-items: center; gap: 8px; border-color: transparent; background: var(--ui-brand); color: var(--ui-brand-contrast);
      box-shadow: 0 8px 18px -10px color-mix(in srgb, var(--ui-brand) 80%, transparent); }
    button.active::after { content: ''; width: 7px; height: 7px; border-radius: 50%; background: var(--ds-accent, var(--ui-warning)); animation: tab-spark 2.4s ease-in-out infinite; }
    .section-group:nth-child(2) button { animation-delay: 90ms; } .section-group:nth-child(3) button { animation-delay: 180ms; }
    button:focus-visible, select:focus-visible { outline: 3px solid var(--ui-border-strong); outline-offset: 2px; }
    .section-select { display: none; flex-direction: column; gap: 6px; font-size: 13px; font-weight: 750; color: var(--ui-text); }
    select { min-height: 44px; padding: 0 10px; border: 1px solid var(--ui-border-strong); border-radius: 12px; color: inherit; background: var(--ui-surface); font: inherit; font-size: 15px; }
    @keyframes tab-in { from { opacity: 0; transform: translateY(6px); } to { opacity: 1; transform: none; } }
    @keyframes tab-spark { 0%, 100% { transform: scale(1); opacity: 1; } 50% { transform: scale(1.5); opacity: .6; } }
    @media (max-width: 700px) { .section-nav { display: none; } .section-select { display: flex; } }
    @media (prefers-reduced-motion: reduce) { button, button.active::after { animation: none; transition: none; } }
  `]
})
export class StaffSectionNavComponent {
  @Input({ required: true }) groups: StaffSectionGroup[] = [];
  @Input() active = '';
  @Input() label = 'Bölüm';
  /** Prefix of the tab ids; the workspace's panel has the id `${idPrefix}-panel`. */
  @Input() idPrefix = 'section';
  get panelId(): string { return `${this.idPrefix}-panel`; }
  tabId(value: string): string { return `${this.idPrefix}-tab-${value}`; }
  @Output() readonly select = new EventEmitter<string>();

  /** Arrow keys, Home and End move to the neighbouring section and select it (automatic activation). */
  move(event: KeyboardEvent): void {
    const values = this.groups.flatMap(group => group.items.map(item => item.value));
    const at = values.indexOf(this.active);
    if (!values.length) return;
    const next = ({ ArrowRight: at + 1, ArrowDown: at + 1, ArrowLeft: at - 1, ArrowUp: at - 1, Home: 0, End: values.length - 1 } as Record<string, number>)[event.key];
    if (next === undefined) return;
    event.preventDefault();
    const value = values[(next + values.length) % values.length];
    (event.currentTarget as HTMLElement | null)?.ownerDocument.getElementById(this.tabId(value))?.focus();
    this.select.emit(value);
  }

  choose(event: Event): void {
    const element = event.target as HTMLSelectElement;
    const value = element.value;
    // The section changes only if the workspace allows it; until then the menu keeps showing where we are.
    element.value = this.active;
    this.select.emit(value);
  }
}
