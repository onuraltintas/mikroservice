import { ChangeDetectionStrategy, Component, ElementRef, afterNextRender, input, viewChild } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

/**
 * Paket A: the address was already verified, so registration needs no e-mail. Replaces the hidden form;
 * focus moves to the heading so it is not lost, and the person can sign in right away.
 */
@Component({
  selector: 'app-registration-verified',
  standalone: true,
  imports: [RouterLink, MatButtonModule, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="success-msg verified-success" role="status" aria-live="polite">
      <mat-icon aria-hidden="true">check_circle</mat-icon>
      <h3 #heading tabindex="-1">Kayıt tamamlandı</h3>
      <p>{{ message() }}</p>
      <a mat-raised-button color="primary" routerLink="/auth/login" [queryParams]="loginQueryParams()">Giriş yap</a>
    </section>
  `,
  styles: [`
    .verified-success { display: flex; flex-wrap: wrap; align-items: center; gap: .5rem; padding: .85rem; border-radius: .75rem;
      color: var(--ui-success); background: var(--ui-success-soft); line-height: 1.6; }
    h3 { margin: 0; font-size: 1rem; outline: none; }
    p, a { flex-basis: 100%; margin: 0; }
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
