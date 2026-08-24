import { Component, Input, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';

/**
 * SpSkeletonComponent — yükleme durumu için animasyonlu placeholder.
 *
 * @example
 * <sp-skeleton />                          <!-- full width line -->
 * <sp-skeleton variant="circle" size="48px" />
 * <sp-skeleton variant="rect" height="120px" />
 * <sp-skeleton width="60%" />
 */
@Component({
  selector: 'sp-skeleton',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: ``,
  host: {
    '[class]': 'hostClass',
    '[style.width]': 'width',
    '[style.height]': 'computedHeight',
    '[style.border-radius]': 'borderRadius',
  },
  styles: [`
    :host {
      display: block;
      background: var(--sp-surface-3);
      position: relative;
      overflow: hidden;

      &::after {
        content: '';
        position: absolute;
        inset: 0;
        background: linear-gradient(
          90deg,
          transparent 0%,
          rgba(255, 255, 255, 0.6) 50%,
          transparent 100%
        );
        transform: translateX(-100%);
        animation: sp-shimmer 1.6s ease-in-out infinite;
      }
    }

    @keyframes sp-shimmer {
      to { transform: translateX(100%); }
    }
  `]
})
export class SpSkeletonComponent {
  @Input() variant: 'text' | 'circle' | 'rect' = 'text';
  @Input() width = '100%';
  @Input() height = '';
  @Input() size = '';  // shortcut for circle width+height

  get computedHeight(): string {
    if (this.height) return this.height;
    if (this.size)   return this.size;
    if (this.variant === 'text')   return '16px';
    if (this.variant === 'circle') return this.width;
    return '80px';
  }

  get borderRadius(): string {
    if (this.variant === 'circle') return '50%';
    if (this.variant === 'text')   return '4px';
    return '12px';
  }

  get hostClass(): string {
    return `sp-skeleton sp-skeleton--${this.variant}`;
  }
}
