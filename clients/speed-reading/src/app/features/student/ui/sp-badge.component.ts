import { Component, Input, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';

/**
 * SpBadgeComponent — renkli durum etiketi.
 *
 * @example
 * <sp-badge>Yeni</sp-badge>
 * <sp-badge color="success">Tamamlandı</sp-badge>
 * <sp-badge color="warning" [dot]="true">Bekliyor</sp-badge>
 */
@Component({
  selector: 'sp-badge',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (dot) {
      <span class="sp-badge__dot" [style.background]="dotColor"></span>
    }
    <ng-content />
  `,
  host: { '[class]': 'hostClass' },
  styles: [`
    :host {
      display: inline-flex;
      align-items: center;
      gap: 5px;
      padding: 2px 10px;
      border-radius: var(--sp-radius-full);
      font-family: var(--sp-font-sans);
      font-size: 0.6875rem;
      font-weight: 600;
      letter-spacing: 0.02em;
      white-space: nowrap;
    }

    :host(.sp-badge--primary) {
      background: var(--sp-primary-ghost);
      color: var(--sp-primary-dark);
    }
    :host(.sp-badge--success) {
      background: var(--sp-success-light);
      color: var(--sp-success-dark);
    }
    :host(.sp-badge--warning) {
      background: var(--sp-warning-light);
      color: var(--sp-warning-dark);
    }
    :host(.sp-badge--danger) {
      background: var(--sp-danger-light);
      color: var(--sp-danger);
    }
    :host(.sp-badge--xp) {
      background: var(--sp-xp-light);
      color: var(--sp-warning-dark);
    }
    :host(.sp-badge--neutral) {
      background: var(--sp-surface-3);
      color: var(--sp-text-2);
    }
    :host(.sp-badge--solid) {
      background: var(--sp-primary);
      color: #fff;
    }

    :host(.sp-badge--lg) {
      font-size: 0.75rem;
      padding: 4px 12px;
    }

    .sp-badge__dot {
      width: 6px;
      height: 6px;
      border-radius: 50%;
      flex-shrink: 0;
    }
  `]
})
export class SpBadgeComponent {
  @Input() color: 'primary' | 'success' | 'warning' | 'danger' | 'xp' | 'neutral' | 'solid' = 'primary';
  @Input() size: 'sm' | 'lg' = 'sm';
  @Input() dot = false;

  private readonly dotColorMap: Record<string, string> = {
    primary: 'var(--sp-primary)',
    success: 'var(--sp-success)',
    warning: 'var(--sp-warning)',
    danger:  'var(--sp-danger)',
    xp:      'var(--sp-xp)',
    neutral: 'var(--sp-text-3)',
    solid:   '#fff',
  };

  get hostClass(): string {
    const cls = ['sp-badge', `sp-badge--${this.color}`];
    if (this.size === 'lg') cls.push('sp-badge--lg');
    return cls.join(' ');
  }

  get dotColor(): string {
    return this.dotColorMap[this.color] ?? 'currentColor';
  }
}
