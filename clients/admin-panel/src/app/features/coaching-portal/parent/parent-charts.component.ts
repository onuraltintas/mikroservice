import { ChangeDetectionStrategy, Component, DestroyRef, ElementRef, afterNextRender, computed, inject, input, signal } from '@angular/core';
import { shortDate } from './parent-model';

export interface ParentBar { label: string; value: number; max: number; text: string; detail?: string }

/**
 * V4 (01.11.2026): a row of small columns for the last weeks. Every column carries its number as text, so the height is
 * only a picture of what is already written; styles live in layout/coaching-parent.scss.
 */
@Component({
  selector: 'cp-week-bars',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<ul class="cp-bars" [attr.aria-label]="label()" [style.--cp-bars]="bars().length">
    @for (bar of bars(); track bar.label + $index; let last = $last) {
      <li [class.is-current]="last">
        <span class="cp-bar-value">{{ bar.text }}</span>
        <span class="cp-bar-track" aria-hidden="true"><span class="cp-bar-fill" [style.height.%]="height(bar)"></span></span>
        <span class="cp-bar-label">{{ bar.label }}@if (bar.detail) { <small>{{ bar.detail }}</small> }</span>
      </li>
    }
  </ul>`
})
export class ParentWeekBarsComponent {
  readonly bars = input.required<ParentBar[]>();
  readonly label = input.required<string>();
  height(bar: ParentBar) { return bar.max > 0 ? Math.max(bar.value > 0 ? 4 : 0, Math.min(100, Math.round((bar.value / bar.max) * 100))) : 0; }
}

export interface ReadingPoint { date: string; wpm: number | null; comprehension: number | null }

const percentFormat = new Intl.NumberFormat('tr-TR', { maximumFractionDigits: 1 });
const H = 200, LEFT = 40, TOP = 14, BOTTOM = H - 26;

/**
 * V4: reading speed (solid line, left axis, words per minute) and comprehension (dashed line, right axis, percent) over
 * the measured results. The same numbers are in the table under the picture.
 */
@Component({
  selector: 'cp-reading-trend',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <svg class="cp-trend" role="img" [attr.viewBox]="'0 0 ' + width() + ' ' + height" [attr.aria-label]="summary()" preserveAspectRatio="xMidYMid meet">
      @for (tick of chart().ticks; track tick.label) {
        <line class="cp-trend-grid" [attr.x1]="left" [attr.x2]="right()" [attr.y1]="tick.y" [attr.y2]="tick.y" />
        <text class="cp-trend-tick" [attr.x]="left - 6" [attr.y]="tick.y + 4" text-anchor="end">{{ tick.label }}</text>
        <text class="cp-trend-tick" [attr.x]="right() + 6" [attr.y]="tick.y + 4">%{{ tick.percent }}</text>
      }
      @if (chart().comprehension) { <path class="cp-trend-comprehension" [attr.d]="chart().comprehension" /> }
      @if (chart().wpm) { <path class="cp-trend-wpm" [attr.d]="chart().wpm" /> }
      @for (dot of chart().dots; track $index) {
        @if (dot.wpmY !== null) { <circle class="cp-trend-dot" [attr.cx]="dot.x" [attr.cy]="dot.wpmY" r="4" /> }
        @if (dot.compY !== null) { <rect class="cp-trend-square" [attr.x]="dot.x - 3.5" [attr.y]="dot.compY - 3.5" width="7" height="7" /> }
      }
      @for (dot of chart().dots; track $index) {
        @if (dot.showLabel) { <text class="cp-trend-tick" [attr.x]="dot.x" [attr.y]="bottom + 18" text-anchor="middle">{{ dot.label }}</text> }
      }
    </svg>
    <p class="cp-trend-legend"><span class="cp-legend-wpm">Okuma hızı (kelime/dk)</span><span class="cp-legend-comp">Anlama (%)</span></p>
    <details class="cp-trend-table"><summary>Sayıları tablo olarak göster</summary>
      <table><caption class="cp-sr-only">Ölçülen okuma sonuçları</caption>
        <thead><tr><th scope="col">Tarih</th><th scope="col">Hız (kelime/dk)</th><th scope="col">Anlama</th></tr></thead>
        <tbody>@for (point of points(); track $index) { <tr><td>{{ day(point.date) }}</td><td>{{ point.wpm ?? '—' }}</td><td>{{ pct(point.comprehension) }}</td></tr> }</tbody>
      </table>
    </details>`
})
export class ParentReadingTrendComponent {
  readonly points = input.required<ReadingPoint[]>();
  readonly childName = input('');
  // Drawn at the real width (like the exam trend chart) so the labels keep their size on a phone and a wide screen.
  readonly width = signal(560); readonly height = H; readonly left = LEFT; readonly bottom = BOTTOM;
  readonly right = computed(() => this.width() - 40);

  constructor() {
    const host = inject(ElementRef<HTMLElement>).nativeElement as HTMLElement;
    const destroyRef = inject(DestroyRef);
    afterNextRender(() => {
      if (typeof ResizeObserver === 'undefined') return;
      const observer = new ResizeObserver(entries => { const width = Math.round(entries[0]?.contentRect.width ?? 0); if (width > 200) this.width.set(width); });
      observer.observe(host); destroyRef.onDestroy(() => observer.disconnect());
    });
  }
  readonly day = shortDate;
  pct(value: number | null) { return value === null || value === undefined ? '—' : '%' + percentFormat.format(value); }

  readonly chart = computed(() => {
    const points = this.points();
    const speeds = points.map(p => p.wpm).filter((x): x is number => x !== null);
    const top = Math.max(100, Math.ceil((Math.max(0, ...speeds) * 1.15) / 50) * 50);
    const y = (value: number, max: number) => BOTTOM - (Math.max(0, Math.min(max, value)) / max) * (BOTTOM - TOP);
    const RIGHT = this.right();
    const x = (index: number) => points.length === 1 ? (LEFT + RIGHT) / 2 : LEFT + (index / (points.length - 1)) * (RIGHT - LEFT);
    const every = Math.max(1, Math.ceil(points.length / (this.width() < 480 ? 4 : 6)));
    const dots = points.map((p, i) => ({ x: round(x(i)), wpmY: p.wpm === null ? null : round(y(p.wpm, top)), compY: p.comprehension === null ? null : round(y(p.comprehension, 100)),
      label: shortDate(p.date), showLabel: i % every === 0 || i === points.length - 1 }));
    const path = (key: 'wpmY' | 'compY') => dots.filter(d => d[key] !== null).map((d, i) => `${i ? 'L' : 'M'}${d.x} ${d[key]}`).join(' ');
    const ticks = [0, .5, 1].map(share => ({ y: round(BOTTOM - share * (BOTTOM - TOP)), label: String(Math.round(top * share)), percent: Math.round(100 * share) }));
    return { dots, ticks, wpm: path('wpmY'), comprehension: path('compY') };
  });

  readonly summary = computed(() => {
    const speeds = this.points().filter(p => p.wpm !== null);
    const who = this.childName() || 'Çocuğun';
    if (!speeds.length) return 'Okuma hızı ve anlama grafiği: henüz ölçülen sonuç yok.';
    const first = speeds[0].wpm!, last = speeds.at(-1)!.wpm!;
    return speeds.length === 1 ? `Okuma hızı ve anlama grafiği. ${who} ilk ölçümde dakikada ${last} kelime okudu.`
      : `Okuma hızı ve anlama grafiği. ${who} ${speeds.length} ölçümde dakikada ${first} kelimeden ${last} kelimeye ${last >= first ? 'çıktı' : 'indi'}.`;
  });
}

function round(value: number) { return Math.round(value * 10) / 10; }
