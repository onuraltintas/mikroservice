import { ChangeDetectionStrategy, Component, DestroyRef, ElementRef, afterNextRender, computed, inject, input, signal } from '@angular/core';
import { StudentExamTrend } from '../../../core/services/coaching-student-exam-overview.service';
import { formatNet, trendChart } from './growth-model';

/**
 * T6: net trend of one exam scope as a small hand-drawn SVG line. The picture is labelled for screen readers and the
 * same numbers are listed in a visually hidden table, so nothing is shown by colour or position alone.
 */
@Component({
  selector: 'cs-exam-trend-chart',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <svg role="img" [attr.viewBox]="'0 0 ' + chart().width + ' ' + chart().height" [attr.aria-label]="label()" preserveAspectRatio="xMidYMid meet">
      @for (tick of chart().ticks; track tick.value) {
        <line class="grid" [attr.x1]="chart().left" [attr.x2]="chart().right" [attr.y1]="tick.y" [attr.y2]="tick.y" />
        <text class="tick" [attr.x]="chart().left - 6" [attr.y]="tick.y + 4" text-anchor="end">{{ tick.value }}</text>
      }
      @if (chart().area) { <path class="area" [attr.d]="chart().area" /> }
      <path class="line" [attr.d]="chart().line" />
      @for (dot of chart().dots; track dot.date + $index; let last = $last) {
        <circle data-testid="trend-dot" [attr.data-source]="dot.source" [class.last]="last" [attr.cx]="dot.x" [attr.cy]="dot.y" [attr.r]="last ? 5.5 : 4"><title>{{ dot.label }}</title></circle>
        @if (last) { <text class="value" [attr.x]="dot.x" [attr.y]="dot.y - 10" text-anchor="middle">{{ net(dot.net) }}</text> }
      }
    </svg>
    <table class="sr-only"><caption>{{ trend().examName }} net gidişatı</caption>
      <tr><th scope="col">Tarih ve kaynak</th><th scope="col">Net</th></tr>
      @for (dot of chart().dots; track dot.date + $index) { <tr><td>{{ dot.label }}</td><td>{{ net(dot.net) }}</td></tr> }
    </table>`,
  styles: [`:host{display:block}
    svg{display:block;width:100%;height:auto;overflow:visible}
    .tick,.value{font-family:inherit}
    .grid{stroke:var(--ui-border);stroke-width:1}
    .tick{fill:var(--ui-text-muted);font-size:10px;font-weight:600}
    .area{fill:var(--ui-brand-soft);opacity:.7}
    .line{fill:none;stroke:var(--ui-brand);stroke-width:2.5;stroke-linejoin:round;stroke-linecap:round}
    circle{fill:var(--ui-surface);stroke:var(--ui-brand);stroke-width:2}
    circle[data-source=Institution]{stroke:var(--ui-accent)}
    circle[data-source=Teacher]{stroke:var(--ui-info)}
    circle.last{fill:var(--ui-brand);stroke:var(--ui-surface);stroke-width:2.5}
    .value{fill:var(--ui-text);font-size:12px;font-weight:800}`]
})
export class ExamTrendChartComponent {
  readonly trend = input.required<StudentExamTrend>();
  /** V4: set on the parent page so the summary names the child. */
  readonly childName = input('');
  // Drawn at the real width so the labels keep their size on a phone and on a wide screen.
  private readonly width = signal(320);
  readonly chart = computed(() => trendChart(this.trend(), this.width(), this.width() < 480 ? 170 : 210, this.childName() || null));

  constructor() {
    const host = inject(ElementRef<HTMLElement>).nativeElement as HTMLElement;
    const destroyRef = inject(DestroyRef);
    afterNextRender(() => {
      if (typeof ResizeObserver === 'undefined') return;
      const observer = new ResizeObserver(entries => { const width = Math.round(entries[0]?.contentRect.width ?? 0); if (width > 200) this.width.set(width); });
      observer.observe(host); destroyRef.onDestroy(() => observer.disconnect());
    });
  }
  readonly label = computed(() => `${this.trend().examName} net gidişatı. ${this.chart().summary}`);
  net(value: number) { return formatNet(value); }
}
