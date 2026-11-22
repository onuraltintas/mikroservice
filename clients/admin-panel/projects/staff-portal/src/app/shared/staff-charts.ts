import { Component, Pipe, PipeTransform, computed, input } from '@angular/core';

// Small, dependency-free charts for the staff portal (B design, 21.11.2026). Colours come only from the design tokens;
// every chart states its values in its accessible name; bars start at zero; nothing moves with reduced motion.

export type StaffChartTone = 'info' | 'violet' | 'success' | 'orange' | 'accent' | 'danger';
export interface StaffChartPoint { label: string; value: number; }
export interface StaffSplitPart { label: string; value: number; tone: StaffChartTone; }
export interface StaffSeriesChart { name: string; points: StaffChartPoint[]; }

const number = new Intl.NumberFormat('tr-TR', { maximumFractionDigits: 1 });
const formatValue = (value: number, percent: boolean) => (percent ? '%' : '') + number.format(value);

/** Turns report rows of named measures into one chart per measure, so measures of different scales never share an axis. */
@Pipe({ name: 'staffSeriesCharts', standalone: true })
export class StaffSeriesChartsPipe implements PipeTransform {
  transform(rows: { name: string; series: { name: string; value: number }[] }[] | null | undefined): StaffSeriesChart[] {
    const charts = new Map<string, StaffChartPoint[]>();
    for (const row of rows ?? []) {
      for (const measure of row.series ?? []) {
        if (!Number.isFinite(measure.value)) continue;
        const points = charts.get(measure.name) ?? [];
        points.push({ label: row.name, value: measure.value });
        charts.set(measure.name, points);
      }
    }
    return [...charts].map(([name, points]) => ({ name, points }));
  }
}

/** Column chart: values written on the bars while there is room, every value in the accessible name. */
@Component({
  selector: 'staff-bars', standalone: true,
  template: `
    <figure class="bars" [class]="'bars tone-' + tone()" role="img" [attr.aria-label]="label()">
      @if (title()) { <figcaption aria-hidden="true">{{ title() }}</figcaption> }
      <div class="plot" aria-hidden="true" [class.dense]="view().length > 12">
        @for (point of view(); track $index) {
          <div class="col" [class.peak]="point.peak" [class.last]="$last" [style.--h]="point.height" [style.--i]="$index" [attr.title]="point.label + ': ' + point.text">
            <span class="val">{{ point.text }}</span>
            <span class="bar"></span>
            <span class="lab" [class.hide]="!point.showLabel">{{ point.label }}</span>
          </div>
        }
      </div>
    </figure>`,
  styles: [`
    :host { display: block; min-width: 0; }
    .bars { margin: 0; --tone: var(--ds-info); --tone-soft: var(--ds-info-soft); }
    .tone-violet { --tone: var(--ds-violet); --tone-soft: var(--ds-violet-soft); } .tone-success { --tone: var(--ds-success); --tone-soft: var(--ds-success-soft); }
    .tone-orange { --tone: var(--ds-orange); --tone-soft: var(--ds-orange-soft); } .tone-accent { --tone: var(--ds-warning); --tone-soft: var(--ds-warning-soft); }
    .tone-danger { --tone: var(--ds-danger); --tone-soft: var(--ds-danger-soft); }
    figcaption { margin: 0 0 .5rem; color: var(--ds-muted); font-size: .8125rem; font-weight: 700; }
    .plot { display: flex; align-items: stretch; gap: 6px; height: 150px; padding: 10px 16px 0 10px; border-radius: 14px; background: var(--tone-soft); overflow-x: auto; }
    .col { position: relative; display: grid; grid-template-rows: auto 1fr auto; grid-template-columns: minmax(0, 1fr); flex: 1 0 22px; min-width: 22px; justify-items: center; }
    .plot.dense { gap: 3px; } .plot.dense .col { flex-basis: 10px; min-width: 10px; } .plot.dense .bar { border-radius: 4px 4px 2px 2px; }
    .val { grid-row: 1; align-self: end; margin-bottom: 3px; color: var(--ds-text); font-size: .75rem; font-weight: 700; font-variant-numeric: tabular-nums; white-space: nowrap; }
    .plot.dense .val { display: none; }
    .plot.dense .col.peak .val, .plot.dense .col.last .val { display: block; }
    .bar { grid-row: 2; align-self: end; width: 100%; max-width: 34px; height: calc(var(--h) * 1%); min-height: 3px; border-radius: 8px 8px 3px 3px;
      background: var(--tone); opacity: .72; transform-origin: bottom; transition: opacity .2s ease, transform .2s ease;
      animation: staff-bar-grow .7s cubic-bezier(.2, .8, .2, 1) both; animation-delay: calc(var(--i) * 35ms); }
    .col.peak .bar, .col.last .bar { opacity: 1; }
    .col:hover .bar { opacity: 1; transform: scaleX(1.08); }
    .lab { grid-row: 3; max-width: 100%; height: 1.6em; padding-top: 4px; overflow: hidden; color: var(--ds-muted); font-size: .75rem; text-overflow: ellipsis; white-space: nowrap; }
    .plot.dense .lab { justify-self: start; max-width: none; overflow: visible; }
    .lab.hide { visibility: hidden; }
    @keyframes staff-bar-grow { from { transform: scaleY(0); } }
    @media (prefers-reduced-motion: reduce) { .bar { animation: none; transition: none; } }
  `]
})
export class StaffBarsComponent {
  readonly points = input<StaffChartPoint[]>([]);
  readonly title = input('');
  readonly tone = input<StaffChartTone>('info');
  readonly percent = input(false);
  readonly view = computed(() => {
    const points = this.points();
    const max = Math.max(0, ...points.map(point => point.value));
    const step = Math.max(1, Math.ceil(points.length / 8));
    return points.map((point, index) => ({
      label: point.label, text: formatValue(point.value, this.percent()),
      height: max > 0 ? Math.max(0, point.value) / max * 100 : 0,
      peak: max > 0 && point.value === max, showLabel: index % step === 0
    }));
  });
  readonly label = computed(() => `${this.title() || 'Grafik'}: ` +
    (this.view().map(point => `${point.label} ${point.text}`).join(', ') || 'veri yok'));
}

/** A horizontal bar for one share of a whole, such as submitted of assigned. */
@Component({
  selector: 'staff-meter', standalone: true,
  template: `<span class="meter" [class]="'meter tone-' + tone()" role="img" [attr.aria-label]="label() || (value() + ' / ' + max())"><i [style.width.%]="share()"></i></span>`,
  styles: [`
    :host { display: block; min-width: 3rem; }
    .meter { display: block; height: 8px; border-radius: 999px; background: var(--meter-track, var(--tone-soft)); overflow: hidden; --tone: var(--ds-info); --tone-soft: var(--ds-info-soft); }
    .tone-violet { --tone: var(--ds-violet); --tone-soft: var(--ds-violet-soft); } .tone-success { --tone: var(--ds-success); --tone-soft: var(--ds-success-soft); }
    .tone-orange { --tone: var(--ds-orange); --tone-soft: var(--ds-orange-soft); } .tone-accent { --tone: var(--ds-warning); --tone-soft: var(--ds-warning-soft); }
    .tone-danger { --tone: var(--ds-danger); --tone-soft: var(--ds-danger-soft); }
    i { display: block; height: 100%; border-radius: inherit; background: var(--tone); transform-origin: left; animation: staff-meter-fill .8s cubic-bezier(.2, .8, .2, 1) both; }
    @keyframes staff-meter-fill { from { transform: scaleX(0); } }
    @media (prefers-reduced-motion: reduce) { i { animation: none; } }
  `]
})
export class StaffMeterComponent {
  readonly value = input(0);
  readonly max = input(0);
  readonly tone = input<StaffChartTone>('info');
  readonly label = input('');
  readonly share = computed(() => this.max() > 0 ? Math.min(100, Math.max(0, this.value() / this.max() * 100)) : 0);
}

/** One whole split into parts, with a legend that carries the numbers. */
@Component({
  selector: 'staff-split', standalone: true,
  template: `
    <figure class="split" role="img" [attr.aria-label]="label()">
      @if (title()) { <figcaption aria-hidden="true">{{ title() }}</figcaption> }
      <div class="track" aria-hidden="true">
        @for (part of view(); track part.label) { @if (part.share > 0) { <span [class]="'seg tone-' + part.tone" [style.flex-grow]="part.value" [style.--i]="$index"></span> } }
      </div>
      <ul class="legend" aria-hidden="true">
        @for (part of view(); track part.label) {
          <li><i [class]="'dot tone-' + part.tone"></i>{{ part.label }} <strong>{{ part.text }}</strong> <small>%{{ part.share }}</small></li>
        }
      </ul>
    </figure>`,
  styles: [`
    :host { display: block; }
    .split { margin: 0; }
    figcaption { margin: 0 0 .5rem; color: var(--ds-text); font-size: .875rem; font-weight: 700; }
    .track { display: flex; gap: 3px; height: 14px; border-radius: 999px; background: var(--ds-soft); overflow: hidden; }
    .seg { flex: 1 1 0; min-width: 4px; background: var(--tone); transform-origin: left; animation: staff-split-in .7s cubic-bezier(.2, .8, .2, 1) both; animation-delay: calc(var(--i) * 90ms); }
    .legend { display: flex; flex-wrap: wrap; gap: .4rem 1.1rem; margin: .65rem 0 0; padding: 0; list-style: none; color: var(--ds-muted); font-size: .8125rem; }
    .legend li { display: inline-flex; align-items: center; gap: .4rem; }
    .legend strong { color: var(--ds-text); font-variant-numeric: tabular-nums; }
    .dot { width: 10px; height: 10px; border-radius: 3px; background: var(--tone); }
    .tone-info { --tone: var(--ds-info); } .tone-violet { --tone: var(--ds-violet); } .tone-success { --tone: var(--ds-success); }
    .tone-orange { --tone: var(--ds-orange); } .tone-accent { --tone: var(--ds-accent); } .tone-danger { --tone: var(--ds-danger); }
    @keyframes staff-split-in { from { transform: scaleX(0); } }
    @media (prefers-reduced-motion: reduce) { .seg { animation: none; } }
  `]
})
export class StaffSplitComponent {
  readonly parts = input<StaffSplitPart[]>([]);
  readonly title = input('');
  readonly view = computed(() => {
    const parts = this.parts().map(part => ({ ...part, value: Math.max(0, part.value) }));
    const total = parts.reduce((sum, part) => sum + part.value, 0);
    return parts.map(part => ({ ...part, text: number.format(part.value), share: total > 0 ? Math.round(part.value / total * 100) : 0 }));
  });
  readonly label = computed(() => `${this.title() || 'Dağılım'}: ` + this.view().map(part => `${part.label} ${part.text}`).join(', '));
}
