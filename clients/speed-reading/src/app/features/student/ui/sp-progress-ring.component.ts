import { Component, Input, computed, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';

/**
 * SpProgressRingComponent — SVG tabanlı dairesel ilerleme göstergesi.
 *
 * @example
 * <sp-progress-ring [value]="75" />
 * <sp-progress-ring [value]="42" size="lg" color="success" label="Başarı" />
 */
@Component({
  selector: 'sp-progress-ring',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="sp-ring" [style.width.px]="diameter" [style.height.px]="diameter">
      <svg
        [attr.width]="diameter"
        [attr.height]="diameter"
        [attr.viewBox]="'0 0 ' + diameter + ' ' + diameter"
        fill="none"
        aria-hidden="true">

        <!-- Track -->
        <circle
          [attr.cx]="center"
          [attr.cy]="center"
          [attr.r]="radius"
          class="sp-ring__track"
          fill="none"
          [attr.stroke-width]="strokeWidth" />

        <!-- Progress -->
        <circle
          [attr.cx]="center"
          [attr.cy]="center"
          [attr.r]="radius"
          class="sp-ring__fill"
          fill="none"
          [attr.stroke-width]="strokeWidth"
          [attr.stroke]="strokeColor"
          [attr.stroke-dasharray]="circumference"
          [attr.stroke-dashoffset]="dashOffset"
          stroke-linecap="round"
          [attr.transform]="svgTransform" />
      </svg>

      <!-- Center content -->
      <div class="sp-ring__center">
        @if (showValue) {
          <span class="sp-ring__value" [style.font-size.px]="valueFontSize">
            {{ clampedValue }}%
          </span>
        }
        <ng-content />
      </div>
    </div>

    @if (label) {
      <p class="sp-ring__label">{{ label }}</p>
    }
  `,
  styles: [`
    :host {
      display: inline-flex;
      flex-direction: column;
      align-items: center;
      gap: 8px;
    }

    .sp-ring {
      position: relative;
      flex-shrink: 0;
    }

    .sp-ring svg {
      position: absolute;
      inset: 0;
    }

    .sp-ring__track {
      stroke: var(--sp-surface-3);
    }

    .sp-ring__fill {
      transition: stroke-dashoffset 0.6s cubic-bezier(0.4, 0, 0.2, 1);
    }

    .sp-ring__center {
      position: absolute;
      inset: 0;
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
    }

    .sp-ring__value {
      font-family: var(--sp-font-sans);
      font-weight: 700;
      color: var(--sp-text-1);
      line-height: 1;
    }

    .sp-ring__label {
      font-family: var(--sp-font-sans);
      font-size: 0.75rem;
      font-weight: 500;
      color: var(--sp-text-2);
      margin: 0;
      text-align: center;
    }
  `]
})
export class SpProgressRingComponent {
  @Input() value = 0;
  @Input() size: 'xs' | 'sm' | 'md' | 'lg' | 'xl' = 'md';
  @Input() color: 'primary' | 'success' | 'warning' | 'danger' | 'xp' = 'primary';
  @Input() showValue = true;
  @Input() label = '';

  private readonly sizeMap = {
    xs: { d: 48,  stroke: 4  },
    sm: { d: 64,  stroke: 5  },
    md: { d: 80,  stroke: 6  },
    lg: { d: 100, stroke: 7  },
    xl: { d: 120, stroke: 8  },
  };

  private readonly colorMap: Record<string, string> = {
    primary: 'var(--sp-primary)',
    success: 'var(--sp-success)',
    warning: 'var(--sp-warning)',
    danger:  'var(--sp-danger)',
    xp:      'var(--sp-xp)',
  };

  get clampedValue(): number {
    return Math.min(100, Math.max(0, Math.round(this.value)));
  }

  get diameter(): number { return this.sizeMap[this.size].d; }
  get strokeWidth(): number { return this.sizeMap[this.size].stroke; }
  get center(): number { return this.diameter / 2; }
  get radius(): number { return this.center - this.strokeWidth / 2; }
  get circumference(): number { return 2 * Math.PI * this.radius; }
  get dashOffset(): number { return this.circumference * (1 - this.clampedValue / 100); }
  get strokeColor(): string { return this.colorMap[this.color] ?? this.colorMap['primary']; }
  get valueFontSize(): number { return Math.round(this.diameter * 0.2); }
  get svgTransform(): string { return `rotate(-90 ${this.center} ${this.center})`; }
}
