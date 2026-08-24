import { Component, Input, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';

/**
 * SpCardComponent — temel kart bileşeni.
 * Tailwind utility'leri yerine CSS değişkenlerini kullanır.
 * Tüm içeriği ng-content ile alır.
 *
 * @example
 * <sp-card>...</sp-card>
 * <sp-card variant="ghost">...</sp-card>
 * <sp-card [noPadding]="true">...</sp-card>
 */
@Component({
  selector: 'sp-card',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    '[class]': 'hostClass',
  },
  template: `<ng-content />`,
  styles: [`
    :host {
      display: block;
      background: var(--sp-surface);
      border: 1px solid var(--sp-border);
      border-radius: var(--sp-radius-card);
      box-shadow: var(--sp-shadow-card);
      padding: var(--card-padding, 24px);
      transition: box-shadow var(--sp-transition-fast), transform var(--sp-transition-fast);
    }

    :host(.sp-card--hover):hover {
      box-shadow: var(--sp-shadow-hover);
      transform: translateY(-1px);
    }

    :host(.sp-card--ghost) {
      background: var(--sp-primary-ghost);
      border-color: rgba(99, 102, 241, 0.2);
      box-shadow: none;
    }

    :host(.sp-card--flat) {
      box-shadow: none;
    }

    :host(.sp-card--no-padding) {
      --card-padding: 0;
    }
  `]
})
export class SpCardComponent {
  @Input() variant: 'default' | 'ghost' | 'flat' = 'default';
  @Input() hover = false;
  @Input() noPadding = false;

  get hostClass(): string {
    const classes = ['sp-card'];
    if (this.variant !== 'default') classes.push(`sp-card--${this.variant}`);
    if (this.hover) classes.push('sp-card--hover');
    if (this.noPadding) classes.push('sp-card--no-padding');
    return classes.join(' ');
  }
}
