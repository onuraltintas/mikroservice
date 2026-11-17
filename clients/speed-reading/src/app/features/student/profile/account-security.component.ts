import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { finalize } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { getErrorMessage } from '../../../core/utils/error-message';
import { isPasswordPolicySatisfied } from '../../../shared/validators/password.validator';

interface MyAccount { email: string; pendingEmail?: string | null; pendingEmailExpiresAt?: string | null; mfaEnabled?: boolean; hasPassword?: boolean }

/**
 * Live test 16.11.2026 (#56), user decision 17.11.2026: the Hızlı Okuma student changes their password and e-mail address here,
 * through the same account endpoints as Koçluk. A new address changes only after the link sent to it is confirmed.
 */
@Component({
  selector: 'app-account-security',
  standalone: true,
  imports: [FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="sp-card account-security" aria-labelledby="account-email-title">
      <div class="sp-card-content">
        <h2 id="account-email-title" class="sp-card-title">E-posta adresi</h2>
        @if (account(); as me) {
          <p>Şu anki adresin: <strong>{{ me.email }}</strong></p>
          @if (me.pendingEmail) {
            <p role="status" data-testid="pending-email"><strong>{{ me.pendingEmail }}</strong> adresinin onayı bekleniyor. Bu adrese gönderdiğimiz bağlantıyı açıp onayla; o zamana kadar e-posta adresin değişmez.</p>
            <button type="button" class="sp-btn" [disabled]="busy()" (click)="cancelEmail()">Değişikliği iptal et</button>
          } @else {
            <form (ngSubmit)="changeEmail()">
              <div class="sp-field"><label for="new-email">Yeni e-posta adresi</label><input id="new-email" name="newEmail" type="email" autocomplete="email" maxlength="255" [(ngModel)]="newEmail"></div>
              <div class="sp-field"><label for="email-password">Mevcut şifren</label><input id="email-password" name="emailPassword" type="password" autocomplete="current-password" maxlength="128" [(ngModel)]="emailPassword"></div>
              @if (me.mfaEnabled) {
                <div class="sp-field"><label for="email-mfa">Doğrulama kodu</label><input id="email-mfa" name="emailMfa" inputmode="numeric" autocomplete="one-time-code" maxlength="6" [(ngModel)]="emailMfa"></div>
              }
              <button type="submit" class="sp-btn sp-btn-primary" [disabled]="busy()">Doğrulama bağlantısı gönder</button>
            </form>
          }
        }
        @if (emailMessage(); as m) { <p [attr.role]="m.error ? 'alert' : 'status'" [class.sp-error]="m.error" data-testid="email-message">{{ m.text }}</p> }
      </div>
    </section>
    <section class="sp-card account-security" aria-labelledby="account-password-title">
      <div class="sp-card-content">
        <h2 id="account-password-title" class="sp-card-title">Şifre</h2>
        <form (ngSubmit)="changePassword()">
          <div class="sp-field"><label for="current-password">Mevcut şifren</label><input id="current-password" name="currentPassword" type="password" autocomplete="current-password" maxlength="128" [(ngModel)]="currentPassword"></div>
          <div class="sp-field"><label for="new-password">Yeni şifre</label><input id="new-password" name="newPassword" type="password" autocomplete="new-password" maxlength="128" [(ngModel)]="newPassword">
            <div class="sp-hint">En az 8 karakter; büyük ve küçük harf, rakam ve özel karakter içermeli.</div></div>
          <div class="sp-field"><label for="confirm-password">Yeni şifre (tekrar)</label><input id="confirm-password" name="confirmPassword" type="password" autocomplete="new-password" maxlength="128" [(ngModel)]="confirmPassword"></div>
          <button type="submit" class="sp-btn sp-btn-primary" [disabled]="busy()">Şifreyi değiştir</button>
        </form>
        @if (passwordMessage(); as m) { <p [attr.role]="m.error ? 'alert' : 'status'" [class.sp-error]="m.error" data-testid="password-message">{{ m.text }}</p> }
      </div>
    </section>`,
  styles: [`.account-security { margin-top: 1.25rem; } .account-security form { display: grid; gap: .75rem; max-width: 28rem; } .account-security h2 { margin: 0 0 .75rem; }`]
})
export class AccountSecurityComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/users/me`;
  readonly account = signal<MyAccount | null>(null);
  readonly busy = signal(false);
  readonly emailMessage = signal<{ text: string; error: boolean } | null>(null);
  readonly passwordMessage = signal<{ text: string; error: boolean } | null>(null);
  newEmail = ''; emailPassword = ''; emailMfa = '';
  currentPassword = ''; newPassword = ''; confirmPassword = '';

  ngOnInit(): void {
    this.http.get<MyAccount>(this.base).subscribe({ next: me => this.account.set(me), error: () => this.account.set(null) });
  }

  changeEmail(): void {
    const email = this.newEmail.trim().toLowerCase();
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) { this.emailMessage.set({ text: 'Geçerli bir e-posta adresi yaz (ör. ad@ornek.com).', error: true }); return; }
    if (email === this.account()?.email.toLowerCase()) { this.emailMessage.set({ text: 'Bu zaten şu anki e-posta adresin.', error: true }); return; }
    if (!this.emailPassword) { this.emailMessage.set({ text: 'Mevcut şifreni yaz.', error: true }); return; }
    const mfa = this.emailMfa.replace(/\s/g, '');
    this.busy.set(true);
    this.http.post<{ pendingEmail?: string; expiresAt?: string }>(`${this.base}/email-change`,
      mfa ? { currentPassword: this.emailPassword, newEmail: email, mfaCode: mfa } : { currentPassword: this.emailPassword, newEmail: email })
      .pipe(finalize(() => this.busy.set(false))).subscribe({
        next: result => {
          this.account.update(me => me ? { ...me, pendingEmail: result?.pendingEmail ?? email, pendingEmailExpiresAt: result?.expiresAt ?? null } : me);
          this.emailPassword = ''; this.emailMfa = ''; this.newEmail = '';
          this.emailMessage.set({ text: `Doğrulama bağlantısını ${result?.pendingEmail ?? email} adresine gönderdik. Bağlantıyı açıp onaylayana kadar e-posta adresin değişmez.`, error: false });
        },
        error: error => {
          if (error?.error?.code === 'Account.MfaCodeRequired') this.account.update(me => me ? { ...me, mfaEnabled: true } : me);
          this.emailMessage.set({ text: getErrorMessage(error, 'E-posta değişikliği başlatılamadı. Yeniden dene.', { voice: 'sen' }), error: true });
        }
      });
  }

  cancelEmail(): void {
    this.busy.set(true);
    this.http.delete<void>(`${this.base}/email-change`).pipe(finalize(() => this.busy.set(false))).subscribe({
      next: () => {
        this.account.update(me => me ? { ...me, pendingEmail: null, pendingEmailExpiresAt: null } : me);
        this.emailMessage.set({ text: 'E-posta değişikliği iptal edildi; gönderdiğimiz bağlantı artık çalışmaz.', error: false });
      },
      error: error => this.emailMessage.set({ text: getErrorMessage(error, 'Değişiklik iptal edilemedi. Yeniden dene.', { voice: 'sen' }), error: true })
    });
  }

  changePassword(): void {
    if (!this.currentPassword) { this.passwordMessage.set({ text: 'Mevcut şifreni yaz.', error: true }); return; }
    if (!isPasswordPolicySatisfied(this.newPassword)) { this.passwordMessage.set({ text: 'Yeni şifre kurallara uymuyor: en az 8 karakter; büyük ve küçük harf, rakam ve özel karakter içermeli.', error: true }); return; }
    if (this.newPassword !== this.confirmPassword) { this.passwordMessage.set({ text: 'Yeni şifreler birbirini tutmuyor.', error: true }); return; }
    this.busy.set(true);
    this.http.post(`${this.base}/change-password`, { currentPassword: this.currentPassword, newPassword: this.newPassword })
      .pipe(finalize(() => this.busy.set(false))).subscribe({
        next: () => {
          this.currentPassword = ''; this.newPassword = ''; this.confirmPassword = '';
          this.passwordMessage.set({ text: 'Şifren değiştirildi.', error: false });
        },
        error: error => this.passwordMessage.set({ text: getErrorMessage(error, 'Şifre değiştirilemedi. Yeniden dene.', { voice: 'sen' }), error: true })
      });
  }
}
