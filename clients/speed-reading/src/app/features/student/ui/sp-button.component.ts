import { Component, Input, Output, EventEmitter, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';

/**
 * SpButtonComponent — temel buton bileşeni.
 * Hem buton hem link olarak kullanılabilir.
 *
 * @example
 * <sp-button (clicked)="doSomething()">Başla</sp-button>
 * <sp-button variant="secondary" size="sm">İptal</sp-button>
 * <sp-button variant="ghost" icon="arrow_back">Geri</sp-button>
 * <sp-button [href]="'/student/dashboard'">Dashboard</sp-button>
 * <sp-button [loading]="true">Yükleniyor</sp-button>
 */
@Component({
  selector: 'sp-button',
  standalone: true,
  imports: [CommonModule, RouterModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (href) {
      <a [routerLink]="href" [class]="btnClass" [attr.aria-disabled]="disabled || null">
        <ng-container *ngTemplateOutlet="content" />
      </a>
    } @else {
      <button
        [class]="btnClass"
        [type]="type"
        [disabled]="disabled || loading"
        (click)="onClick($event)"
        [attr.aria-busy]="loading || null">
        <ng-container *ngTemplateOutlet="content" />
      </button>
    }

    <ng-template #content>
      @if (loading) {
        <span class="sp-btn__spinner" aria-hidden="true"></span>
      } @else if (icon && iconPosition === 'left') {
        <span class="material-icons sp-btn__icon">{{ icon }}</span>
      }
      <span><ng-content /></span>
      @if (!loading && icon && iconPosition === 'right') {
        <span class="material-icons sp-btn__icon">{{ icon }}</span>
      }
    </ng-template>
  `,
  styles: [`
    :host { display: inline-flex; }

    .sp-btn {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      gap: 6px;
      border: none;
      cursor: pointer;
      font-family: var(--sp-font-sans);
      font-weight: 600;
      white-space: nowrap;
      text-decoration: none;
      transition:
        background  var(--sp-transition-fast),
        box-shadow  var(--sp-transition-fast),
        transform   var(--sp-transition-fast),
        color       var(--sp-transition-fast);

      &:active { transform: translateY(1px); }
      &:disabled { opacity: 0.55; cursor: not-allowed; pointer-events: none; }
    }

    // Sizes
    .sp-btn--xs  { height: 28px; padding: 0 10px; font-size: 0.75rem;  border-radius: var(--sp-radius-sm); }
    .sp-btn--sm  { height: 34px; padding: 0 14px; font-size: 0.8125rem; border-radius: var(--sp-radius-sm); }
    .sp-btn--md  { height: 40px; padding: 0 20px; font-size: 0.875rem;  border-radius: var(--sp-radius-sm); }
    .sp-btn--lg  { height: 48px; padding: 0 28px; font-size: 1rem;      border-radius: var(--sp-radius-md); }

    // Variants
    .sp-btn--primary {
      background: var(--sp-primary);
      color: #fff;
      box-shadow: 0 1px 3px rgba(99, 102, 241, 0.3);
      &:hover:not(:disabled) {
        background: var(--sp-primary-dark);
        box-shadow: 0 4px 12px rgba(99, 102, 241, 0.4);
        transform: translateY(-1px);
      }
    }

    .sp-btn--secondary {
      background: var(--sp-surface-3);
      color: var(--sp-text-1);
      border: 1px solid var(--sp-border-strong);
      &:hover:not(:disabled) {
        background: var(--sp-surface-4);
        box-shadow: var(--sp-shadow-sm);
      }
    }

    .sp-btn--ghost {
      background: transparent;
      color: var(--sp-primary);
      border: 1px solid var(--sp-primary);
      &:hover:not(:disabled) {
        background: var(--sp-primary-ghost);
      }
    }

    .sp-btn--text {
      background: transparent;
      color: var(--sp-primary);
      padding-left: 4px;
      padding-right: 4px;
      &:hover:not(:disabled) {
        background: var(--sp-primary-ghost);
        text-decoration: none;
      }
    }

    .sp-btn--danger {
      background: var(--sp-danger);
      color: #fff;
      &:hover:not(:disabled) {
        filter: brightness(0.92);
        box-shadow: 0 4px 12px rgba(244, 63, 94, 0.3);
      }
    }

    // Full width
    .sp-btn--full { width: 100%; }

    // Icon-only
    .sp-btn--icon-only {
      padding: 0;
      &.sp-btn--xs  { width: 28px; }
      &.sp-btn--sm  { width: 34px; }
      &.sp-btn--md  { width: 40px; }
      &.sp-btn--lg  { width: 48px; }
    }

    .sp-btn__icon { font-size: 18px; flex-shrink: 0; }
    .sp-btn--lg .sp-btn__icon { font-size: 20px; }

    // Spinner
    .sp-btn__spinner {
      width: 16px;
      height: 16px;
      border: 2px solid rgba(255,255,255,0.3);
      border-top-color: currentColor;
      border-radius: 50%;
      animation: sp-spin 0.7s linear infinite;
    }

    @keyframes sp-spin {
      to { transform: rotate(360deg); }
    }
  `]
})
export class SpButtonComponent {
  @Input() variant: 'primary' | 'secondary' | 'ghost' | 'text' | 'danger' = 'primary';
  @Input() size: 'xs' | 'sm' | 'md' | 'lg' = 'md';
  @Input() icon = '';
  @Input() iconPosition: 'left' | 'right' = 'left';
  @Input() iconOnly = false;
  @Input() loading = false;
  @Input() disabled = false;
  @Input() fullWidth = false;
  @Input() href = '';
  @Input() type: 'button' | 'submit' | 'reset' = 'button';

  @Output() clicked = new EventEmitter<MouseEvent>();

  get btnClass(): string {
    const cls = ['sp-btn', `sp-btn--${this.variant}`, `sp-btn--${this.size}`];
    if (this.fullWidth) cls.push('sp-btn--full');
    if (this.iconOnly)  cls.push('sp-btn--icon-only');
    return cls.join(' ');
  }

  onClick(event: MouseEvent): void {
    if (!this.disabled && !this.loading) {
      this.clicked.emit(event);
    }
  }
}
