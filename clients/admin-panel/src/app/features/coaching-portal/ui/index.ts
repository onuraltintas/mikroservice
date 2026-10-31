import {
  ChangeDetectionStrategy, Component, ElementRef, computed, effect, inject, input, model
} from '@angular/core';

/**
 * Student coaching UI kit (T1, 31.10.2026). Small presentational pieces the redesigned pages build on; every colour
 * comes from coaching-student-tokens.scss, so they follow light and dark mode. Names start with `cs-` (coaching student).
 */

export type CsTone = 'neutral' | 'brand' | 'accent' | 'success' | 'warning' | 'danger' | 'info';

@Component({
  selector: 'cs-page-header',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div class="text"><h1>{{ title() }}</h1>@if (subtitle()) {<p>{{ subtitle() }}</p>}</div>
    <div class="actions"><ng-content select="[actions]" /></div>`,
  styles: [`:host{display:flex;flex-wrap:wrap;align-items:flex-end;justify-content:space-between;gap:var(--coaching-space-4);margin-bottom:var(--coaching-space-6)}
    h1{margin:0;font-size:var(--coaching-text-xl);font-weight:800;line-height:1.25;color:var(--ui-text)}
    p{margin:var(--coaching-space-1) 0 0;font-size:var(--coaching-text-body);color:var(--ui-text-muted);max-width:60ch}
    .actions{display:flex;gap:var(--coaching-space-2);flex-wrap:wrap}`]
})
export class CsPageHeaderComponent {
  readonly title = input.required<string>();
  readonly subtitle = input<string>('');
}

@Component({
  selector: 'cs-card',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[attr.data-tone]': 'tone()' },
  template: `<ng-content />`,
  styles: [`:host{display:block;padding:var(--coaching-space-5);border:1px solid var(--ui-border);border-radius:var(--coaching-radius-card);background:var(--ui-surface);color:var(--ui-text)}
    :host([data-tone=raised]){background:var(--ui-surface-raised);box-shadow:var(--coaching-raised-shadow);border-color:transparent}
    :host([data-tone=brand]){background:var(--ui-brand-soft);border-color:var(--ui-brand)}
    :host([data-tone=accent]){background:var(--ui-accent-soft);border-color:var(--ui-accent)}
    @media (max-width:600px){:host{padding:var(--coaching-space-4)}}`]
})
export class CsCardComponent {
  readonly tone = input<'default' | 'raised' | 'brand' | 'accent'>('default');
}

@Component({
  selector: 'cs-status-chip',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[attr.data-tone]': 'tone()' },
  template: `{{ label() }}`,
  styles: [`:host{display:inline-flex;align-items:center;min-height:24px;padding:2px 10px;border-radius:var(--coaching-radius-pill);font-size:var(--coaching-text-sm);font-weight:700;line-height:1.4;white-space:nowrap;background:var(--ui-surface-muted);color:var(--ui-text-muted)}
    :host([data-tone=brand]){background:var(--ui-brand-soft);color:var(--ui-brand-text)}
    :host([data-tone=accent]){background:var(--ui-accent-soft);color:var(--ui-accent-text)}
    :host([data-tone=success]){background:var(--ui-success-soft);color:var(--ui-success)}
    :host([data-tone=warning]){background:var(--ui-warning-soft);color:var(--ui-warning)}
    :host([data-tone=danger]){background:var(--ui-danger-soft);color:var(--ui-danger)}
    :host([data-tone=info]){background:var(--ui-info-soft);color:var(--ui-info)}`]
})
export class CsStatusChipComponent {
  readonly label = input.required<string>();
  readonly tone = input<CsTone>('neutral');
}

@Component({
  selector: 'cs-list-row',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<span class="leading"><ng-content select="[leading]" /></span>
    <span class="main"><strong>{{ title() }}</strong>@if (meta()) {<small>{{ meta() }}</small>}<ng-content /></span>
    <span class="trailing"><ng-content select="[trailing]" /></span>`,
  styles: [`:host{display:grid;grid-template-columns:auto minmax(0,1fr) auto;align-items:center;gap:var(--coaching-space-3);min-height:56px;padding:var(--coaching-space-3) 0;border-bottom:1px solid var(--ui-border);color:var(--ui-text)}
    :host(:last-child){border-bottom:0}
    .leading:empty,.trailing:empty{display:none}
    .main{display:grid;gap:2px;min-width:0}
    strong{font-size:var(--coaching-text-body);font-weight:700;overflow-wrap:anywhere}
    small{font-size:var(--coaching-text-sm);color:var(--ui-text-muted)}`]
})
export class CsListRowComponent {
  readonly title = input.required<string>();
  readonly meta = input<string>('');
}

function percent(value: number, max: number) {
  return max > 0 ? Math.max(0, Math.min(100, (value / max) * 100)) : 0;
}

@Component({
  selector: 'cs-progress-ring',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div role="progressbar" [attr.aria-label]="label()" aria-valuemin="0" [attr.aria-valuemax]="max()" [attr.aria-valuenow]="value()">
    <svg viewBox="0 0 44 44" aria-hidden="true"><circle class="track" cx="22" cy="22" r="18" /><circle class="fill" cx="22" cy="22" r="18" [attr.stroke-dasharray]="dash()" /></svg>
    <span class="count">{{ value() }}<small>/{{ max() }}</small></span></div>`,
  styles: [`:host{display:inline-block;width:var(--cs-ring-size,72px)}
    div{position:relative;display:grid;place-items:center}
    svg{width:100%;transform:rotate(-90deg)}
    circle{fill:none;stroke-width:5}
    .track{stroke:var(--ui-surface-muted)}
    .fill{stroke:var(--ui-brand);stroke-linecap:round;transition:stroke-dasharray .4s ease}
    .count{position:absolute;font-weight:800;font-size:var(--coaching-text-md);color:var(--ui-text)}
    small{font-size:11px;font-weight:600;color:var(--ui-text-muted)}
    @media (prefers-reduced-motion:reduce){.fill{transition:none}}`]
})
export class CsProgressRingComponent {
  readonly value = input.required<number>();
  readonly max = input<number>(100);
  readonly label = input.required<string>();
  readonly dash = computed(() => { const length = 2 * Math.PI * 18; return `${(percent(this.value(), this.max()) / 100) * length} ${length}`; });
}

@Component({
  selector: 'cs-progress-bar',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[attr.data-tone]': 'tone()' },
  template: `<div role="progressbar" [attr.aria-label]="label()" aria-valuemin="0" [attr.aria-valuemax]="max()" [attr.aria-valuenow]="value()"><span [style.width.%]="width()"></span></div>`,
  styles: [`:host{display:block}
    div{height:10px;border-radius:var(--coaching-radius-pill);background:var(--ui-surface-muted);overflow:hidden}
    span{display:block;height:100%;border-radius:inherit;background:var(--ui-brand);transition:width .4s ease}
    :host([data-tone=accent]) span{background:var(--ui-accent)}
    :host([data-tone=success]) span{background:var(--ui-success)}
    @media (prefers-reduced-motion:reduce){span{transition:none}}`]
})
export class CsProgressBarComponent {
  readonly value = input.required<number>();
  readonly max = input<number>(100);
  readonly label = input.required<string>();
  readonly tone = input<'brand' | 'accent' | 'success'>('brand');
  readonly width = computed(() => percent(this.value(), this.max()));
}

@Component({
  selector: 'cs-empty-state',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<strong>{{ title() }}</strong>@if (text()) {<p>{{ text() }}</p>}<div class="action"><ng-content /></div>`,
  styles: [`:host{display:grid;justify-items:center;gap:var(--coaching-space-2);padding:var(--coaching-space-8) var(--coaching-space-4);text-align:center;border:1px dashed var(--ui-border-strong);border-radius:var(--coaching-radius-card);color:var(--ui-text)}
    strong{font-size:var(--coaching-text-md)}
    p{margin:0;max-width:40ch;color:var(--ui-text-muted);font-size:var(--coaching-text-body)}
    .action:empty{display:none}`]
})
export class CsEmptyStateComponent {
  readonly title = input.required<string>();
  readonly text = input<string>('');
}

export interface CsTabOption { value: string; label: string; count?: number | null; }

@Component({
  selector: 'cs-segmented-tabs',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div role="tablist" [attr.aria-label]="label()" (keydown)="onKey($event)">
    @for (option of options(); track option.value) {
      <button type="button" role="tab" [attr.aria-selected]="option.value === value()" [attr.tabindex]="option.value === value() ? 0 : -1"
        (click)="value.set(option.value)">{{ option.label }}@if (option.count !== undefined && option.count !== null) {<span class="count">{{ option.count }}</span>}</button>
    }</div>`,
  styles: [`:host{display:block;max-width:100%;overflow-x:auto}
    div{display:inline-flex;gap:4px;padding:4px;border-radius:var(--coaching-radius-pill);background:var(--ui-surface-muted)}
    button{display:inline-flex;align-items:center;gap:6px;min-height:40px;padding:0 16px;border:0;border-radius:var(--coaching-radius-pill);background:transparent;color:var(--ui-text-muted);font:inherit;font-size:var(--coaching-text-body);font-weight:650;white-space:nowrap;cursor:pointer}
    button[aria-selected=true]{background:var(--ui-surface);color:var(--ui-text);box-shadow:var(--coaching-card-shadow)}
    .count{min-width:20px;padding:0 6px;border-radius:var(--coaching-radius-pill);background:var(--ui-brand-soft);color:var(--ui-brand-text);font-size:12px;font-weight:800}`]
})
export class CsSegmentedTabsComponent {
  private readonly host = inject(ElementRef<HTMLElement>);
  readonly options = input.required<CsTabOption[]>();
  readonly label = input.required<string>();
  readonly value = model.required<string>();

  onKey(event: KeyboardEvent) {
    const step = event.key === 'ArrowRight' ? 1 : event.key === 'ArrowLeft' ? -1 : 0;
    if (!step) return;
    event.preventDefault();
    const options = this.options();
    const index = (options.findIndex(option => option.value === this.value()) + step + options.length) % options.length;
    this.value.set(options[index].value);
    const tabs = this.host.nativeElement.querySelectorAll('[role=tab]') as NodeListOf<HTMLButtonElement>;
    tabs[index]?.focus();
  }
}

@Component({
  selector: 'cs-bottom-sheet',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `@if (open()) {
    <div class="scrim" (click)="open.set(false)"></div>
    <section role="dialog" aria-modal="true" [attr.aria-label]="title()" (keydown.escape)="open.set(false)">
      <header><strong>{{ title() }}</strong><button type="button" class="close" aria-label="Kapat" (click)="open.set(false)">✕</button></header>
      <div class="body"><ng-content /></div>
    </section>}`,
  styles: [`.scrim{position:fixed;inset:0;z-index:900;background:var(--coaching-scrim)}
    section{position:fixed;z-index:901;left:0;right:0;bottom:0;max-height:85dvh;overflow:auto;padding:var(--coaching-space-5) var(--coaching-space-5) calc(var(--coaching-space-5) + env(safe-area-inset-bottom));border-radius:20px 20px 0 0;background:var(--ui-surface);color:var(--ui-text);box-shadow:var(--coaching-raised-shadow)}
    header{display:flex;align-items:center;justify-content:space-between;gap:var(--coaching-space-3);margin-bottom:var(--coaching-space-4)}
    strong{font-size:var(--coaching-text-lg)}
    .close{min-width:44px;min-height:44px;border:0;border-radius:var(--coaching-radius-control);background:var(--ui-surface-muted);color:var(--ui-text);font:inherit;cursor:pointer}
    @media (min-width:801px){section{left:50%;right:auto;bottom:50%;width:min(560px,92vw);transform:translate(-50%,50%);border-radius:var(--coaching-radius-card)}}`]
})
export class CsBottomSheetComponent {
  private readonly host = inject(ElementRef<HTMLElement>);
  readonly title = input.required<string>();
  readonly open = model<boolean>(false);

  constructor() {
    effect(() => {
      if (!this.open()) return;
      queueMicrotask(() => {
        const body = this.host.nativeElement.querySelector('.body') as HTMLElement | null;
        const first = body?.querySelector('button, a[href], input, select, textarea, [tabindex]:not([tabindex="-1"])') as HTMLElement | null;
        (first ?? this.host.nativeElement.querySelector('.close') as HTMLElement | null)?.focus();
      });
    });
  }
}
