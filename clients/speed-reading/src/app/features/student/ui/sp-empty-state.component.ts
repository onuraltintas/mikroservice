import { Component, Input, Output, EventEmitter, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';

/**
 * SpEmptyStateComponent — boş durum gösterimi.
 *
 * @example
 * <sp-empty-state
 *   icon="fitness_center"
 *   title="Henüz egzersiz yok"
 *   description="İlk egzersizini tamamla, istatistiklerin burada görünecek."
 *   actionLabel="Egzersize Başla"
 *   (action)="startExercise()" />
 */
@Component({
  selector: 'sp-empty-state',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="sp-empty">
      <!-- Illustration / Icon -->
      <div class="sp-empty__icon-wrap" [class]="'sp-empty__icon-wrap--' + color">
        <span class="material-icons sp-empty__icon">{{ icon }}</span>
      </div>

      <!-- Text -->
      <div class="sp-empty__text">
        <h3 class="sp-empty__title">{{ title }}</h3>
        @if (description) {
          <p class="sp-empty__desc">{{ description }}</p>
        }
      </div>

      <!-- Action button -->
      @if (actionLabel) {
        <button class="sp-empty__action" (click)="action.emit()">
          {{ actionLabel }}
        </button>
      }

      <!-- Extra slot -->
      <ng-content />
    </div>
  `,
  styles: [`
    :host { display: block; }

    .sp-empty {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      gap: 16px;
      padding: 48px 24px;
      text-align: center;
    }

    .sp-empty__icon-wrap {
      width: 72px;
      height: 72px;
      border-radius: var(--sp-radius-xl);
      display: flex;
      align-items: center;
      justify-content: center;
    }

    .sp-empty__icon-wrap--primary { background: var(--sp-primary-ghost); }
    .sp-empty__icon-wrap--success { background: var(--sp-success-light); }
    .sp-empty__icon-wrap--warning { background: var(--sp-warning-light); }
    .sp-empty__icon-wrap--neutral { background: var(--sp-surface-3); }

    .sp-empty__icon {
      font-size: 32px;
    }

    .sp-empty__icon-wrap--primary .sp-empty__icon { color: var(--sp-primary); }
    .sp-empty__icon-wrap--success .sp-empty__icon { color: var(--sp-success); }
    .sp-empty__icon-wrap--warning .sp-empty__icon { color: var(--sp-warning); }
    .sp-empty__icon-wrap--neutral .sp-empty__icon { color: var(--sp-text-3); }

    .sp-empty__text {
      max-width: 320px;
    }

    .sp-empty__title {
      font-family: var(--sp-font-sans);
      font-size: 1rem;
      font-weight: 700;
      color: var(--sp-text-1);
      margin: 0 0 8px;
    }

    .sp-empty__desc {
      font-size: 0.875rem;
      color: var(--sp-text-2);
      line-height: 1.5;
      margin: 0;
    }

    .sp-empty__action {
      height: 40px;
      padding: 0 20px;
      border-radius: var(--sp-radius-sm);
      border: none;
      background: var(--sp-primary);
      color: #fff;
      font-family: var(--sp-font-sans);
      font-size: 0.875rem;
      font-weight: 600;
      cursor: pointer;
      transition: background var(--sp-transition-fast), box-shadow var(--sp-transition-fast);
      margin-top: 4px;

      &:hover {
        background: var(--sp-primary-dark);
        box-shadow: 0 4px 12px rgba(99, 102, 241, 0.35);
      }
    }
  `]
})
export class SpEmptyStateComponent {
  @Input() icon = 'inbox';
  @Input() title = 'Henüz veri yok';
  @Input() description = '';
  @Input() actionLabel = '';
  @Input() color: 'primary' | 'success' | 'warning' | 'neutral' = 'neutral';

  @Output() action = new EventEmitter<void>();
}
