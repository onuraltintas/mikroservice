import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/auth/auth.service';
import { getApiErrorMessage } from '../../../core/http/api-error-message';
import { IdentityService } from '../../../core/services/identity.service';

/**
 * E5 (#22): the page the e-mail change link opens (PublicAppUrlOptions.EmailChangeConfirmationPath on the Coaching
 * site, or the admin panel for management accounts). The change is applied only when the button is pressed, so a mail
 * scanner that opens the link changes nothing. Reads `userId` and `token` (tests/E2E/email-verification-links).
 */
@Component({
  selector: 'app-confirm-email-change',
  standalone: true,
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <main class="confirm-change" aria-labelledby="confirm-change-title">
      <section>
        <h1 id="confirm-change-title">Yeni e-posta adresini onayla</h1>
        @if (state() === 'done') {
          <p role="status">E-posta adresin değiştirildi. Bundan sonra giriş yaparken ve bildirimlerde yeni adresin kullanılacak.</p>
          @if (signedIn()) { <a routerLink="/coaching-portal/account/settings/account">Hesabım'a dön</a> }
          @else { <a routerLink="/auth/login">Giriş yap</a> }
        } @else if (!userId || !token) {
          <p role="alert">Bağlantı eksik görünüyor. E-postadaki bağlantının tamamını aç ya da Hesabım sayfasından yeni bir bağlantı iste.</p>
          <a routerLink="/auth/login">Giriş sayfasına dön</a>
        } @else {
          <p>Hesabının e-posta adresini bu bağlantının gönderildiği adresle değiştirmek için onayla. Onaylamazsan adresin değişmez.</p>
          @if (error()) { <p role="alert">{{ error() }}</p> }
          <button type="button" data-testid="confirm-email-change" [disabled]="state() === 'working'" (click)="confirm()">
            {{ state() === 'working' ? 'Onaylanıyor…' : 'Yeni adresimi onayla' }}</button>
        }
      </section>
    </main>`,
  styles: [`
    .confirm-change { min-height: 100vh; display: grid; place-items: center; padding: 16px; background: #f8fafc; color: #111827; }
    section { width: 100%; max-width: 440px; display: grid; gap: 16px; padding: 32px 24px; border: 1px solid #e5e7eb; border-radius: 16px; background: #fff; }
    h1 { margin: 0; font-size: 24px; }
    p { margin: 0; line-height: 1.5; }
    [role="alert"] { padding: 12px 16px; border-radius: 10px; background: #fef2f2; color: #b91c1c; font-weight: 600; }
    [role="status"] { padding: 12px 16px; border-radius: 10px; background: #f0fdf4; color: #15803d; font-weight: 600; }
    button { min-height: 44px; border: 0; border-radius: 10px; background: #4f46e5; color: #fff; font-weight: 700; cursor: pointer; }
    button:disabled { opacity: .6; cursor: not-allowed; }
    a { color: #4338ca; font-weight: 650; }
  `]
})
export class ConfirmEmailChangeComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly identity = inject(IdentityService);
  private readonly auth = inject(AuthService);

  readonly userId = this.route.snapshot.queryParamMap.get('userId');
  readonly token = this.route.snapshot.queryParamMap.get('token');
  readonly state = signal<'ready' | 'working' | 'done'>('ready');
  readonly error = signal<string | null>(null);
  readonly signedIn = () => !!this.auth.userProfile();

  confirm(): void {
    if (!this.userId || !this.token || this.state() === 'working') return;
    this.state.set('working');
    this.error.set(null);
    this.identity.confirmEmailChange(this.userId, this.token).subscribe({
      next: () => {
        this.state.set('done');
        // A signed-in session renews so the account menu shows the new address.
        if (this.signedIn()) void this.auth.refreshSession();
      },
      error: error => {
        this.state.set('ready');
        this.error.set(getApiErrorMessage(error, 'E-posta adresin değiştirilemedi. Hesabım sayfasından yeni bir bağlantı iste.', { voice: 'sen' }));
      }
    });
  }
}
