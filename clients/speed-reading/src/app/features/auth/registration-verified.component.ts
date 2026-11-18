import { ChangeDetectionStrategy, Component, ElementRef, afterNextRender, input, viewChild } from '@angular/core';
import { RouterLink } from '@angular/router';

/**
 * Paket A: the address was already verified, so registration needs no e-mail. Replaces the hidden form;
 * focus moves to the heading so it is not lost, and the person can sign in right away.
 */
@Component({
  selector: 'app-registration-verified',
  standalone: true,
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="verified" role="status" aria-live="polite">
      <h3 #heading tabindex="-1">Kayıt tamamlandı</h3>
      <p>{{ message() }}</p>
      <a class="ds-btn ds-btn--primary ds-btn--large ds-btn--block" routerLink="/auth/login" [queryParams]="loginQueryParams()">Giriş yap</a>
    </section>
  `,
  styles: [`
    .verified { display: flex; flex-direction: column; gap: 12px; padding: 18px; border: 1px solid currentColor; border-radius: var(--ds-radius);
      color: var(--ds-success); background: var(--ds-success-soft); line-height: 1.6; }
    h3 { margin: 0; color: var(--ds-text); font-size: 20px; font-weight: 600; border-radius: 4px; }
    h3:focus { outline: 3px solid var(--ds-focus, currentColor); outline-offset: 4px; }
    p { margin: 0; color: var(--ds-text); }
  `]
})
export class RegistrationVerifiedComponent {
  readonly message = input.required<string>();
  readonly loginQueryParams = input<Record<string, string>>({});
  private readonly heading = viewChild.required<ElementRef<HTMLElement>>('heading');

  constructor() {
    afterNextRender(() => this.heading().nativeElement.focus());
  }
}
