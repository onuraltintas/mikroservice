import { Component, EventEmitter, Input, Output } from '@angular/core';

export interface StaffSectionItem { value: string; label: string; testId?: string }
export interface StaffSectionGroup { label: string; items: StaffSectionItem[] }

/** Roadmap O2: sections in labelled groups; on the phone the same choices are a single menu. */
@Component({
  selector: 'staff-section-nav',
  standalone: true,
  template: `
    <nav class="section-nav" [attr.aria-label]="label">
      @for (group of groups; track group.label) {
        <div class="section-group" role="group" [attr.aria-label]="group.label">
          <span class="section-group-label" aria-hidden="true">{{ group.label }}</span>
          <div class="section-group-items">
            @for (item of group.items; track item.value) {
              <button type="button" [attr.data-testid]="item.testId ?? null" [attr.aria-pressed]="active === item.value"
                [attr.aria-current]="active === item.value ? 'page' : null" [class.active]="active === item.value" (click)="select.emit(item.value)">{{ item.label }}</button>
            }
          </div>
        </div>
      }
    </nav>
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
    :host { display: block; margin-top: 19px; }
    .section-nav { display: flex; flex-wrap: wrap; gap: 8px 14px; padding: 6px; border: 1px solid var(--ui-border, #d6dfe2); border-radius: 11px; background: var(--ui-surface, #fff); }
    .section-group { display: flex; flex-direction: column; gap: 4px; }
    .section-group-label { padding: 2px 8px 0; color: var(--ui-text-muted, #5b6b66); font-size: 11px; font-weight: 800; letter-spacing: .08em; text-transform: uppercase; }
    .section-group-items { display: flex; flex-wrap: wrap; gap: 4px; }
    button { min-height: 38px; padding: 0 14px; border: 0; border-radius: 8px; color: var(--ui-text, #1d372f); background: transparent; font: inherit; font-size: 13px; font-weight: 750; cursor: pointer; }
    button.active { background: var(--ui-brand-soft, #e9f1e6); box-shadow: 0 1px 3px rgb(29 55 47 / 12%); }
    button:focus-visible, select:focus-visible { outline: 2px solid #779372; outline-offset: 2px; }
    .section-select { display: none; flex-direction: column; gap: 6px; font-size: 13px; font-weight: 750; color: var(--ui-text, #1d372f); }
    select { min-height: 44px; padding: 0 10px; border: 1px solid var(--ui-border, #d6dfe2); border-radius: 10px; color: inherit; background: var(--ui-surface, #fff); font: inherit; font-size: 15px; }
    @media (max-width: 700px) { .section-nav { display: none; } .section-select { display: flex; } }
  `]
})
export class StaffSectionNavComponent {
  @Input({ required: true }) groups: StaffSectionGroup[] = [];
  @Input() active = '';
  @Input() label = 'Bölüm';
  @Output() readonly select = new EventEmitter<string>();

  choose(event: Event): void {
    const element = event.target as HTMLSelectElement;
    const value = element.value;
    // The section changes only if the workspace allows it; until then the menu keeps showing where we are.
    element.value = this.active;
    this.select.emit(value);
  }
}
