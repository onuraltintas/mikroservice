import { Component, Input, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';

/**
 * SpStatCardComponent — istatistik kartı.
 * İkon, büyük değer ve açıklama etiketi gösterir.
 * İsteğe bağlı trend (artış/düşüş) göstergesi.
 *
 * @example
 * <sp-stat-card icon="speed" label="Okuma Hızı" value="342" unit="kpm" />
 * <sp-stat-card icon="emoji_events" label="Başarı" value="87" unit="%" trend="+5" trendUp />
 */
@Component({
  selector: 'sp-stat-card',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="sp-stat">
      <!-- Icon -->
      <div class="sp-stat__icon-wrap" [class]="'sp-stat__icon-wrap--' + color">
        <span class="material-icons sp-stat__icon">{{ icon }}</span>
      </div>

      <!-- Value row -->
      <div class="sp-stat__body">
        <div class="sp-stat__value-row">
          <span class="sp-stat__value">{{ value }}</span>
          @if (unit) {
            <span class="sp-stat__unit">{{ unit }}</span>
          }
          @if (trend) {
            <span class="sp-stat__trend" [class.sp-stat__trend--up]="trendUp" [class.sp-stat__trend--down]="!trendUp">
              <span class="material-icons sp-stat__trend-icon">
                {{ trendUp ? 'trending_up' : 'trending_down' }}
              </span>
              {{ trend }}
            </span>
          }
        </div>
        <p class="sp-stat__label">{{ label }}</p>
        @if (sublabel) {
          <p class="sp-stat__sublabel">{{ sublabel }}</p>
        }
      </div>
    </div>
  `,
  styles: [`
    :host { display: block; }

    .sp-stat {
      display: flex;
      gap: 16px;
      align-items: flex-start;
    }

    .sp-stat__icon-wrap {
      width: 48px;
      height: 48px;
      border-radius: var(--sp-radius-md);
      display: flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
    }

    .sp-stat__icon-wrap--primary { background: var(--sp-primary-ghost); }
    .sp-stat__icon-wrap--success { background: var(--sp-success-light); }
    .sp-stat__icon-wrap--warning { background: var(--sp-warning-light); }
    .sp-stat__icon-wrap--xp      { background: var(--sp-xp-light); }
    .sp-stat__icon-wrap--streak  { background: var(--sp-streak-light); }
    .sp-stat__icon-wrap--neutral { background: var(--sp-surface-3); }

    .sp-stat__icon {
      font-size: 22px;
      color: var(--sp-icon-color, var(--sp-primary));
    }

    .sp-stat__icon-wrap--primary .sp-stat__icon { color: var(--sp-primary); }
    .sp-stat__icon-wrap--success .sp-stat__icon { color: var(--sp-success); }
    .sp-stat__icon-wrap--warning .sp-stat__icon { color: var(--sp-warning); }
    .sp-stat__icon-wrap--xp      .sp-stat__icon { color: var(--sp-xp); }
    .sp-stat__icon-wrap--streak  .sp-stat__icon { color: var(--sp-streak); }
    .sp-stat__icon-wrap--neutral .sp-stat__icon { color: var(--sp-text-2); }

    .sp-stat__body {
      flex: 1;
      min-width: 0;
    }

    .sp-stat__value-row {
      display: flex;
      align-items: baseline;
      gap: 4px;
      flex-wrap: wrap;
    }

    .sp-stat__value {
      font-family: var(--sp-font-sans);
      font-size: 1.75rem;
      font-weight: 800;
      color: var(--sp-text-1);
      line-height: 1;
      letter-spacing: -0.02em;
    }

    .sp-stat__unit {
      font-family: var(--sp-font-sans);
      font-size: 0.875rem;
      font-weight: 600;
      color: var(--sp-text-2);
    }

    .sp-stat__trend {
      display: inline-flex;
      align-items: center;
      gap: 2px;
      font-size: 0.75rem;
      font-weight: 600;
      padding: 1px 6px;
      border-radius: var(--sp-radius-full);
      margin-left: 4px;

      &--up   { background: var(--sp-success-light); color: var(--sp-success-dark); }
      &--down { background: var(--sp-danger-light);  color: var(--sp-danger); }
    }

    .sp-stat__trend-icon {
      font-size: 14px;
    }

    .sp-stat__label {
      font-size: 0.8125rem;
      font-weight: 500;
      color: var(--sp-text-2);
      margin: 4px 0 0;
    }

    .sp-stat__sublabel {
      font-size: 0.75rem;
      color: var(--sp-text-3);
      margin: 2px 0 0;
    }
  `]
})
export class SpStatCardComponent {
  @Input() icon = 'insights';
  @Input() label = '';
  @Input() value: string | number = '—';
  @Input() unit = '';
  @Input() sublabel = '';
  @Input() trend = '';
  @Input() trendUp = true;
  @Input() color: 'primary' | 'success' | 'warning' | 'xp' | 'streak' | 'neutral' = 'primary';
}
