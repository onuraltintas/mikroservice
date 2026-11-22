import { Component, computed, effect, input, signal } from '@angular/core';

// Yönetim paneli için bağımlılıksız, canlı ve okunur grafikler (21.11.2026). Renkler yalnız değişkenlerden gelir; her grafik
// değerlerini erişilebilir adında söyler; çubuklar sıfırdan başlar; hareket azaltmada hiçbir şey kımıldamaz.

export type AdminTone = 'info' | 'success' | 'violet' | 'orange' | 'honey' | 'danger';
export interface AdminPoint { label: string; value: number; }
export interface AdminPart { label: string; value: number; tone: AdminTone; }

const numberFormat = new Intl.NumberFormat('tr-TR', { maximumFractionDigits: 1 });
export const formatAdminValue = (value: number, percent = false) => (percent ? '%' : '') + numberFormat.format(value);

const reduceMotion = () => typeof window !== 'undefined' && typeof window.matchMedia === 'function'
  && window.matchMedia('(prefers-reduced-motion: reduce)').matches;

// Not: TONES her bileşenin stiline eklenerek tek parça yapılır. Aynı dosyada birebir aynı satır içi stil parçası
// birden çok bileşende durursa Angular'ın test derleyicisi (unit-test) testler bitince kapanmıyor (angular-cli #33317).
const TONES = `
  .tone-info { --tone: var(--ds-info); --tone-soft: var(--ds-info-soft); }
  .tone-success { --tone: var(--ds-success); --tone-soft: var(--ds-success-soft); }
  .tone-violet { --tone: var(--ds-violet); --tone-soft: var(--ds-violet-soft); }
  .tone-orange { --tone: var(--ds-orange); --tone-soft: var(--ds-orange-soft); }
  .tone-honey { --tone: var(--ds-warning); --tone-soft: var(--ds-warning-soft); }
  .tone-danger { --tone: var(--ds-danger); --tone-soft: var(--ds-danger-soft); }`;

/** Sayı kutucuğu: anlamının yumuşak renginde; sayı açılışta sayarak yükselir. */
@Component({
  selector: 'admin-stat', standalone: true,
  template: `
    <div [class]="'stat tone-' + tone()" [class.link]="!!hint()">
      <span class="label">{{ label() }}</span>
      <strong class="value">{{ shown() }}</strong>
      @if (hint()) { <span class="hint">{{ hint() }}</span> }
    </div>`,
  styles: [TONES + `
    :host { display: block; min-width: 0; }
    .stat { display: grid; gap: 4px; height: 100%; padding: 16px 18px; border-radius: 16px; background: var(--tone-soft); color: var(--ds-text);
      transition: transform .2s ease, box-shadow .2s ease; }
    :host(:hover) .stat.link { transform: translateY(-3px); box-shadow: var(--ds-shadow-hover, var(--ds-shadow)); }
    .label { color: var(--ds-text); font-size: .8125rem; font-weight: 600; }
    .value { color: var(--tone); font-size: 1.75rem; font-weight: 700; line-height: 1.1; font-variant-numeric: tabular-nums; }
    .hint { color: var(--ds-muted); font-size: .75rem; }
    @media (prefers-reduced-motion: reduce) { .stat { transition: none; } :host(:hover) .stat.link { transform: none; } }
  `]
})
export class AdminStatComponent {
  readonly label = input('');
  /** null shows a dash: the number is not known (not zero). */
  readonly value = input<number | null>(null);
  readonly tone = input<AdminTone>('info');
  readonly hint = input('');
  readonly percent = input(false);
  private readonly current = signal<number | null>(null);
  readonly shown = computed(() => { const v = this.current(); return v === null ? '—' : formatAdminValue(v, this.percent()); });

  constructor() {
    effect(onCleanup => {
      const target = this.value();
      if (target === null || !Number.isFinite(target)) { this.current.set(null); return; }
      if (reduceMotion() || target === 0 || typeof requestAnimationFrame === 'undefined') { this.current.set(target); return; }
      const start = performance.now(); const duration = 700; let frame = 0;
      const tick = (now: number) => {
        const t = Math.min(1, (now - start) / duration); const eased = 1 - Math.pow(1 - t, 3);
        this.current.set(t < 1 ? Math.round(target * eased * 10) / 10 : target);
        if (t < 1) frame = requestAnimationFrame(tick);
      };
      frame = requestAnimationFrame(tick);
      onCleanup(() => cancelAnimationFrame(frame));
    });
  }
}

/** Sütun grafiği: değerler sütunların üstünde, her değer erişilebilir adda; sütunlar sırayla sıfırdan büyür. */
@Component({
  selector: 'admin-bars', standalone: true,
  template: `
    <figure [class]="'bars tone-' + tone()" role="img" [attr.aria-label]="label()">
      @if (title()) { <figcaption aria-hidden="true">{{ title() }}</figcaption> }
      <div class="plot" aria-hidden="true" [class.dense]="view().length > 12">
        @for (point of view(); track $index) {
          <div class="col" [class.peak]="point.peak" [class.last]="$last" [style.--h]="point.height" [style.--i]="$index" [attr.title]="point.label + ': ' + point.text">
            <span class="val">{{ point.text }}</span><span class="bar"></span><span class="lab" [class.hide]="!point.showLabel">{{ point.label }}</span>
          </div>
        } @empty { <p class="none">Henüz veri yok.</p> }
      </div>
    </figure>`,
  styles: [TONES + `
    :host { display: block; min-width: 0; }
    .bars { margin: 0; }
    figcaption { margin: 0 0 .5rem; color: var(--ds-text); font-size: .875rem; font-weight: 700; }
    .plot { display: flex; align-items: stretch; gap: 6px; height: var(--admin-bars-height, 160px); padding: 12px 14px 0 10px; border-radius: 14px; background: var(--tone-soft); overflow-x: auto; }
    .col { display: grid; grid-template-rows: auto 1fr auto; grid-template-columns: minmax(0, 1fr); flex: 1 0 22px; min-width: 22px; justify-items: center; }
    .plot.dense { gap: 3px; } .plot.dense .col { flex-basis: 10px; min-width: 10px; }
    .val { grid-row: 1; align-self: end; margin-bottom: 3px; color: var(--ds-text); font-size: .75rem; font-weight: 700; font-variant-numeric: tabular-nums; white-space: nowrap; }
    .plot.dense .val { display: none; } .plot.dense .col.peak .val, .plot.dense .col.last .val { display: block; }
    .bar { grid-row: 2; align-self: end; width: 100%; max-width: 36px; height: calc(var(--h) * 1%); min-height: 3px; border-radius: 8px 8px 3px 3px;
      background: var(--tone); opacity: .7; transform-origin: bottom; transition: opacity .2s ease, transform .2s ease;
      animation: admin-bar-grow .75s cubic-bezier(.2, .8, .2, 1) both; animation-delay: calc(var(--i) * 40ms); }
    .col.peak .bar, .col.last .bar, .col:hover .bar { opacity: 1; }
    .col:hover .bar { transform: scaleX(1.08); }
    .lab { grid-row: 3; max-width: 100%; height: 1.7em; padding-top: 4px; overflow: hidden; color: var(--ds-muted); font-size: .75rem; text-overflow: ellipsis; white-space: nowrap; }
    .plot.dense .lab { justify-self: start; overflow: visible; }
    .lab.hide { visibility: hidden; }
    .none { margin: auto; color: var(--ds-muted); font-size: .875rem; }
    @keyframes admin-bar-grow { from { transform: scaleY(0); } }
    @media (prefers-reduced-motion: reduce) { .bar { animation: none; transition: none; } }
  `]
})
export class AdminBarsComponent {
  readonly points = input<AdminPoint[]>([]);
  readonly title = input('');
  readonly tone = input<AdminTone>('info');
  readonly percent = input(false);
  readonly view = computed(() => {
    const points = this.points(); const max = Math.max(0, ...points.map(p => p.value)); const step = Math.max(1, Math.ceil(points.length / 8));
    return points.map((p, i) => ({ label: p.label, text: formatAdminValue(p.value, this.percent()),
      height: max > 0 ? Math.max(0, p.value) / max * 100 : 0, peak: max > 0 && p.value === max, showLabel: i % step === 0 }));
  });
  readonly label = computed(() => `${this.title() || 'Grafik'}: ` + (this.view().map(p => `${p.label} ${p.text}`).join(', ') || 'veri yok'));
}

/** Eğilim çizgisi: dolgulu alan, son nokta vurgulu; çizgi açılışta çizilerek gelir. */
@Component({
  selector: 'admin-trend', standalone: true,
  template: `
    <figure [class]="'trend tone-' + tone()" role="img" [attr.aria-label]="label()">
      @if (title()) { <figcaption aria-hidden="true">{{ title() }} @if (last(); as l) { <strong>{{ l }}</strong> }</figcaption> }
      @if (shape(); as s) {
        <div class="plot">
        <svg viewBox="0 0 300 100" preserveAspectRatio="none" aria-hidden="true">
          @for (y of [25, 50, 75]; track y) { <line class="grid" x1="0" x2="300" [attr.y1]="y" [attr.y2]="y" /> }
          <path class="area" [attr.d]="s.area" />
          <path class="line" [attr.d]="s.line" />
        </svg>
        <span class="dot" aria-hidden="true" [style.left.%]="s.lastX / 3" [style.top.%]="s.lastY"></span>
        </div>
        <div class="axis" aria-hidden="true"><span>{{ s.first }}</span><span>{{ s.lastLabel }}</span></div>
      } @else { <p class="none">Henüz veri yok.</p> }
    </figure>`,
  styles: [TONES + `
    :host { display: block; min-width: 0; }
    .trend { position: relative; margin: 0; }
    figcaption { display: flex; justify-content: space-between; gap: 8px; margin: 0 0 .5rem; color: var(--ds-text); font-size: .875rem; font-weight: 700; }
    figcaption strong { color: var(--tone); font-variant-numeric: tabular-nums; }
    svg { display: block; width: 100%; height: var(--admin-trend-height, 120px); overflow: visible; }
    .grid { stroke: var(--ds-line); stroke-width: 1; vector-effect: non-scaling-stroke; }
    .area { fill: var(--tone); opacity: .12; animation: admin-trend-fade .9s ease both; }
    .line { fill: none; stroke: var(--tone); stroke-width: 2.5; stroke-linecap: round; stroke-linejoin: round; vector-effect: non-scaling-stroke; }
    svg { animation: admin-trend-draw 1.1s cubic-bezier(.2, .8, .2, 1) both; }
    .dot { position: absolute; width: 12px; height: 12px; margin: -6px 0 0 -6px; border: 3px solid var(--ds-surface); border-radius: 50%; background: var(--tone);
      box-shadow: 0 0 0 2px var(--tone); animation: admin-trend-dot .4s 1s ease both; }
    .plot { position: relative; }
    .axis { display: flex; justify-content: space-between; margin-top: 6px; color: var(--ds-muted); font-size: .75rem; }
    .none { margin: 0; padding: 24px 0; color: var(--ds-muted); font-size: .875rem; text-align: center; }
    @keyframes admin-trend-draw { from { clip-path: inset(0 100% 0 0); } to { clip-path: inset(0 0 0 0); } }
    @keyframes admin-trend-fade { from { opacity: 0; } }
    @keyframes admin-trend-dot { from { transform: scale(0); } }
    @media (prefers-reduced-motion: reduce) { svg, .area, .dot { animation: none; } }
  `]
})
export class AdminTrendComponent {
  readonly points = input<AdminPoint[]>([]);
  readonly title = input('');
  readonly tone = input<AdminTone>('info');
  readonly percent = input(false);
  readonly last = computed(() => { const p = this.points(); return p.length ? formatAdminValue(p[p.length - 1].value, this.percent()) : ''; });
  readonly shape = computed(() => {
    const p = this.points(); if (p.length < 2) return null;
    const max = Math.max(1, ...p.map(x => x.value)); const min = 0;
    const xs = p.map((_, i) => i / (p.length - 1) * 300);
    const ys = p.map(x => 92 - (Math.max(min, x.value) - min) / (max - min) * 84);
    const line = xs.map((x, i) => `${i ? 'L' : 'M'}${x.toFixed(1)},${ys[i].toFixed(1)}`).join(' ');
    return { line, area: `${line} L300,100 L0,100 Z`, lastX: xs[xs.length - 1], lastY: ys[ys.length - 1], first: p[0].label, lastLabel: p[p.length - 1].label };
  });
  readonly label = computed(() => `${this.title() || 'Eğilim'}: ` + (this.points().map(p => `${p.label} ${formatAdminValue(p.value, this.percent())}`).join(', ') || 'veri yok'));
}

/** Halka: bir bütünün parçaları; ortada toplam, yanında sayılarıyla açıklama; dilimler sırayla çizilir. */
@Component({
  selector: 'admin-ring', standalone: true,
  template: `
    <figure class="donut" role="img" [attr.aria-label]="label()">
      @if (title()) { <figcaption aria-hidden="true">{{ title() }}</figcaption> }
      <div class="body" aria-hidden="true">
        <svg viewBox="0 0 120 120">
          <circle class="track" cx="60" cy="60" r="46" />
          @for (s of slices(); track s.label) {
            <circle [class]="'slice tone-' + s.tone" cx="60" cy="60" r="46" pathLength="100" [attr.stroke-dasharray]="s.dash" [attr.stroke-dashoffset]="s.offset" [style.--i]="$index" />
          }
          <text x="60" y="58" class="total">{{ total() }}</text><text x="60" y="76" class="unit">{{ unit() }}</text>
        </svg>
        <ul>
          @for (s of slices(); track s.label) { <li><i [class]="'tone-' + s.tone"></i>{{ s.label }} <strong>{{ s.text }}</strong> <small>%{{ s.share }}</small></li> }
        </ul>
      </div>
    </figure>`,
  styles: [TONES + `
    :host { display: block; min-width: 0; }
    .donut { margin: 0; }
    figcaption { margin: 0 0 .5rem; color: var(--ds-text); font-size: .875rem; font-weight: 700; }
    .body { display: flex; align-items: center; gap: 18px; flex-wrap: wrap; }
    svg { width: 132px; height: 132px; flex: 0 0 132px; transform: rotate(-90deg); }
    .track { fill: none; stroke: var(--ds-soft); stroke-width: 14; }
    .slice { fill: none; stroke: var(--tone); stroke-width: 14; animation: admin-ring-in .8s cubic-bezier(.2, .8, .2, 1) both; animation-delay: calc(var(--i) * 120ms); }
    text { transform: rotate(90deg); transform-origin: 60px 60px; text-anchor: middle; fill: var(--ds-text); }
    .total { font-size: 22px; font-weight: 700; } .unit { font-size: 10px; fill: var(--ds-muted); }
    ul { display: grid; gap: 6px; margin: 0; padding: 0; list-style: none; color: var(--ds-muted); font-size: .8125rem; }
    li { display: flex; align-items: center; gap: 8px; }
    li i { width: 10px; height: 10px; border-radius: 3px; background: var(--tone); }
    strong { color: var(--ds-text); font-variant-numeric: tabular-nums; }
    @keyframes admin-ring-in { from { stroke-dasharray: 0 100; } }
    @media (prefers-reduced-motion: reduce) { .slice { animation: none; } }
  `]
})
export class AdminRingComponent {
  readonly parts = input<AdminPart[]>([]);
  readonly title = input('');
  readonly unit = input('toplam');
  readonly totalValue = computed(() => this.parts().reduce((sum, p) => sum + Math.max(0, p.value), 0));
  readonly total = computed(() => formatAdminValue(this.totalValue()));
  readonly slices = computed(() => {
    const total = this.totalValue(); let used = 0;
    return this.parts().map(p => {
      const share = total > 0 ? Math.max(0, p.value) / total * 100 : 0;
      const slice = { label: p.label, tone: p.tone, text: formatAdminValue(p.value), share: Math.round(share),
        dash: `${Math.max(0, share - (share > 2 ? 1.2 : 0))} ${100 - share}`, offset: -used };
      used += share; return slice;
    });
  });
  readonly label = computed(() => `${this.title() || 'Dağılım'}: ` + (this.slices().map(s => `${s.label} ${s.text}`).join(', ') || 'veri yok'));
}

/** Tek bir oran çubuğu (ör. ödenen / toplam). */
@Component({
  selector: 'admin-meter', standalone: true,
  template: `<span [class]="'meter tone-' + tone()" role="img" [attr.aria-label]="label() || (value() + ' / ' + max())"><i [style.width.%]="share()"></i></span>`,
  styles: [TONES + `
    :host { display: block; min-width: 3rem; }
    .meter { display: block; height: 8px; border-radius: 999px; background: var(--tone-soft); overflow: hidden; }
    i { display: block; height: 100%; border-radius: inherit; background: var(--tone); transform-origin: left; animation: admin-meter-fill .8s cubic-bezier(.2, .8, .2, 1) both; }
    @keyframes admin-meter-fill { from { transform: scaleX(0); } }
    @media (prefers-reduced-motion: reduce) { i { animation: none; } }
  `]
})
export class AdminMeterComponent {
  readonly value = input(0);
  readonly max = input(0);
  readonly tone = input<AdminTone>('info');
  readonly label = input('');
  readonly share = computed(() => this.max() > 0 ? Math.min(100, Math.max(0, this.value() / this.max() * 100)) : 0);
}
