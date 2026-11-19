import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { StaffAuthRecaptchaService } from '../../../../staff-portal/src/app/auth/staff-auth-recaptcha.service';
import { LegalAcceptance, MaxkampusApi, TeacherInvitationInfo } from '../core/api';
import { fieldErrors, problemCode, problemText } from '../core/problem';
import { Seo } from '../core/seo';
import { MaxkampusSession } from '../core/session.service';
import { AuthLayoutComponent } from './auth-layout.component';
import { GoogleButtonComponent } from './google-button.component';
import { GoogleSignup } from './google-signup';
import { LegalConsentComponent } from './legal-consent.component';
import { googleProblem } from './login.component';

export type RegistrationKind = 'institution' | 'teacher' | 'invited';

/** A Turkish phone number as people write it: ten digits after 0 or +90 (the service checks it again). */
export function phoneLooksValid(value: string): boolean {
  const digits = value.replace(/[\s().-]/g, '');
  return /^(\+90|90|0)?[2-5]\d{9}$/.test(digits) || /^\+\d{8,15}$/.test(digits);
}

export const PASSWORD_RULE = 'En az 8 karakter; büyük ve küçük harf, rakam ve bir işaret (örnek: !).';
export function passwordLooksValid(value: string): boolean {
  return value.length >= 8 && value.length <= 128 && /[A-Z]/.test(value) && /[a-z]/.test(value) && /\d/.test(value) && /[^A-Za-z0-9]/.test(value);
}

/**
 * /kayit: "Kurumum var" or "Özel ders veriyorum", then the form (name, e-mail, password, workspace, phone, consents).
 * A teacher who came with an invitation (?davet=) skips the choice. Google sign-up continues on /kayit/google.
 */
@Component({
  selector: 'mk-register',
  standalone: true,
  imports: [FormsModule, RouterLink, AuthLayoutComponent, GoogleButtonComponent, LegalConsentComponent],
  template: `
    <mk-auth-layout [headline]="invitation() ? invitation()!.workspaceName + ' sizi bekliyor' : 'Kurumunuzu birkaç dakikada açın'">
      @if (done(); as outcome) {
        <div class="ds-auth__heading"><h1 class="ds-auth__title">{{ outcome === 'sent' ? 'E-postanızı doğrulayın' : 'Hesabınız hazır' }}</h1></div>
        @if (outcome === 'sent') {
          <p>{{ email }} adresine bir bağlantı gönderdik. Bağlantıya tıklayınca hesabınız doğrulanır ve {{ kind() === 'invited' ? 'davetinize dönersiniz' : 'plan seçimine geçersiniz' }}.</p>
          <ul class="mk-muted tips"><li>E-posta birkaç dakika içinde gelir.</li><li>Gelmediyse istenmeyen e-posta klasörüne bakın.</li></ul>
          @if (resent()) { <p class="mk-success" role="status">Bağlantı yeniden gönderildi.</p> }
          <button type="button" class="ds-btn ds-btn--secondary" (click)="resend()" [disabled]="busy()">Bağlantıyı yeniden gönder</button>
        } @else {
          <p>Mevcut eduivme hesabınız maxkampüs'e eklendi. Aynı e-posta ve şifreyle giriş yapın.</p>
          <a class="ds-btn ds-btn--primary ds-btn--large ds-btn--block" routerLink="/giris" [queryParams]="kind() === 'invited' && davet() ? { donus: '/davet/' + davet() } : {}">Giriş yap</a>
        }
      } @else if (!kind()) {
        <div class="ds-auth__heading"><h1 class="ds-auth__title">Kayıt olun</h1>
          <p class="ds-auth__lead">Kayıttan sonra e-posta adresinizi doğrular, ardından planınızı seçersiniz.</p></div>
        <div class="kinds" role="group" aria-label="Hesap türü">
          <button type="button" class="kind" (click)="choose('institution')"><strong>Kurumum var</strong><span>Kurs, etüt merkezi ya da dil okulu; birden çok öğretmen.</span></button>
          <button type="button" class="kind" (click)="choose('teacher')"><strong>Özel ders veriyorum</strong><span>Kendi öğrencilerim için tek kişilik çalışma alanı.</span></button>
        </div>
        <p class="ds-divider">ya da</p>
        <mk-google-button (token)="google($event)" />
        @if (error()) { <p class="ds-alert" role="alert">{{ error() }}</p> }
        <div class="ds-auth__footer"><span>Hesabınız var mı? <a class="ds-link" routerLink="/giris">Giriş yapın</a></span>
          <span>Öğretmen daveti mi aldınız? E-postadaki bağlantıyı açın.</span></div>
      } @else {
        <div class="ds-auth__heading">
          @if (kind() !== 'invited') { <button type="button" class="ds-btn ds-btn--quiet ds-back-link" (click)="kind.set(null)">Hesap türünü değiştir</button> }
          <h1 class="ds-auth__title">{{ title() }}</h1>
          <p class="ds-auth__lead">{{ lead() }}</p>
        </div>
        <form class="ds-auth__form" (ngSubmit)="submit()" novalidate>
          <div class="ds-form-row">
            <label class="ds-field"><span class="ds-field__label">Adınız</span><input class="ds-input" name="firstName" [(ngModel)]="firstName" autocomplete="given-name" maxlength="100"></label>
            <label class="ds-field"><span class="ds-field__label">Soyadınız</span><input class="ds-input" name="lastName" [(ngModel)]="lastName" autocomplete="family-name" maxlength="100"></label>
          </div>
          @if (err('firstName') || err('lastName')) { <p class="ds-field__error">{{ err('firstName') || err('lastName') }}</p> }
          <label class="ds-field"><span class="ds-field__label">E-posta</span>
            <input class="ds-input" type="email" name="email" [(ngModel)]="email" autocomplete="email" [readonly]="kind() === 'invited' && !!invitation()" [attr.aria-invalid]="!!err('email')">
            @if (kind() === 'invited' && invitation()) { <span class="ds-field__hint">Davet bu adrese gönderildi.</span> }</label>
          @if (err('email')) { <p class="ds-field__error">{{ err('email') }}</p> }
          <label class="ds-field"><span class="ds-field__label">Şifre</span>
            <input class="ds-input" type="password" name="password" [(ngModel)]="password" autocomplete="new-password" [attr.aria-invalid]="!!err('password')">
            <span class="ds-field__hint">{{ passwordRule }} maxkoç ya da Master Hızlı Okuma hesabınız varsa onun şifresini yazın.</span></label>
          @if (err('password')) { <p class="ds-field__error">{{ err('password') }}</p> }
          @if (kind() !== 'invited') {
            <label class="ds-field"><span class="ds-field__label">{{ kind() === 'institution' ? 'Kurumunuzun adı' : 'Çalışma alanınızın adı (isteğe bağlı)' }}</span>
              <input class="ds-input" name="workspaceName" [(ngModel)]="workspaceName" maxlength="200" autocomplete="organization" [attr.aria-invalid]="!!err('workspaceName')">
              <span class="ds-field__hint">{{ kind() === 'institution' ? 'Velilere giden e-postalarda bu ad görünür.' : 'Boş bırakırsanız adınız kullanılır.' }}</span></label>
            @if (err('workspaceName')) { <p class="ds-field__error">{{ err('workspaceName') }}</p> }
          }
          <label class="ds-field"><span class="ds-field__label">Telefon</span>
            <input class="ds-input" type="tel" name="phone" [(ngModel)]="phone" autocomplete="tel" placeholder="0532 123 45 67" [attr.aria-invalid]="!!err('phone')">
            <span class="ds-field__hint">Kurulumda ve ödemede size ulaşabilmemiz için.</span></label>
          @if (err('phone')) { <p class="ds-field__error">{{ err('phone') }}</p> }
          @if (needsCode()) {
            <label class="ds-field"><span class="ds-field__label">Mevcut hesabınızın doğrulama kodu</span>
              <input class="ds-input ds-input--code" name="mfaCode" [(ngModel)]="mfaCode" inputmode="numeric" autocomplete="one-time-code" maxlength="6"></label>
          }
          <mk-legal-consent (accepted)="legal.set($event)" />
          @if (err('legal')) { <p class="ds-field__error">{{ err('legal') }}</p> }
          @if (kind() !== 'invited') {
            <label class="ds-check optin"><input type="checkbox" name="optIn" [(ngModel)]="newsletter"> <span>Yenilikler ve kampanyalar için e-posta almak istiyorum (isteğe bağlı).</span></label>
          }
          @if (error()) { <p class="ds-alert" role="alert">{{ error() }}</p> }
          <button class="ds-btn ds-btn--primary ds-btn--large ds-btn--block" type="submit" [disabled]="busy()">{{ busy() ? 'Kaydediliyor' : 'Hesabı aç' }}</button>
        </form>
        @if (kind() !== 'invited') { <p class="ds-divider">ya da</p><mk-google-button (token)="google($event)" /> }
      }
    </mk-auth-layout>
  `,
  styles: `
    .kinds { display: grid; gap: 12px; }
    .kind { display: flex; flex-direction: column; gap: 4px; align-items: flex-start; min-height: 72px; padding: 16px 18px; border: 1.5px solid var(--ds-line); border-radius: 12px; background: var(--ds-surface); color: var(--ds-text); font: inherit; text-align: left; cursor: pointer; }
    .kind:hover { border-color: var(--ds-primary); } .kind strong { font-size: 17px; } .kind span { color: var(--ds-muted); font-size: 14px; }
    .optin { align-items: flex-start; font-size: 14px; line-height: 1.5; } .optin input { flex: none; margin-top: 2px; }
    .tips { margin: 0; padding-left: 20px; }
  `
})
export class RegisterComponent implements OnInit {
  private readonly api = inject(MaxkampusApi);
  private readonly router = inject(Router);
  private readonly seo = inject(Seo);
  private readonly signup = inject(GoogleSignup);
  private readonly session = inject(MaxkampusSession);
  private readonly recaptcha = inject(StaffAuthRecaptchaService);
  /** The invitation code of a teacher (?davet=). */
  readonly davet = input<string | undefined>();
  readonly kind = signal<RegistrationKind | null>(null);
  readonly invitation = signal<TeacherInvitationInfo | null>(null);
  readonly legal = signal<LegalAcceptance[] | null>(null);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly errors = signal<Record<string, string>>({});
  readonly needsCode = signal(false);
  readonly done = signal<'sent' | 'notRequired' | null>(null);
  readonly resent = signal(false);
  readonly passwordRule = PASSWORD_RULE;
  readonly title = computed(() => this.kind() === 'institution' ? 'Kurumunuzu açın' : this.kind() === 'teacher' ? 'Özel ders alanınızı açın' : 'Öğretmen hesabınızı açın');
  readonly lead = computed(() => this.kind() === 'invited'
    ? `${this.invitation()?.workspaceName ?? 'Kurumunuz'} sizi öğretmen olarak davet etti. Hesap açınca daveti kabul edersiniz.`
    : 'Kayıttan sonra e-posta adresinizi doğrularsınız, ardından planınızı seçersiniz.');
  firstName = ''; lastName = ''; email = ''; password = ''; workspaceName = ''; phone = ''; mfaCode = ''; newsletter = false;

  async ngOnInit(): Promise<void> {
    this.seo.set({ title: 'Kayıt olun', path: '/kayit', description: 'maxkampüs\'te kurumunuzu ya da özel ders alanınızı açın.' });
    const code = this.davet();
    if (code) {
      this.signup.rememberInvite(code);
      this.kind.set('invited');
      const info = await this.api.invitation(code).catch(() => null);
      this.invitation.set(info);
      if (info) this.email = info.email;
    }
  }

  choose(kind: RegistrationKind): void { this.kind.set(kind); this.error.set(''); }
  err(field: string): string { return this.errors()[field] ?? ''; }

  async submit(): Promise<void> {
    const kind = this.kind();
    if (!kind) return;
    const errors: Record<string, string> = {};
    if (!this.firstName.trim()) errors['firstName'] = 'Adınızı yazın.';
    if (!this.lastName.trim()) errors['lastName'] = 'Soyadınızı yazın.';
    if (!this.email.includes('@')) errors['email'] = 'Geçerli bir e-posta adresi yazın.';
    if (!this.password) errors['password'] = 'Şifrenizi yazın.';
    if (kind === 'institution' && !this.workspaceName.trim()) errors['workspaceName'] = 'Kurumunuzun adını yazın.';
    if (!phoneLooksValid(this.phone)) errors['phone'] = 'Telefon numaranızı yazın (örnek: 0532 123 45 67).';
    if (!this.legal()) errors['legal'] = 'Devam etmek için metinleri okuyup onaylayın.';
    this.errors.set(errors);
    this.error.set('');
    if (Object.keys(errors).length) return;
    this.busy.set(true);
    try {
      const outcome = await this.api.register({
        kind, email: this.email.trim(), password: this.password, firstName: this.firstName.trim(), lastName: this.lastName.trim(),
        phone: this.phone.trim(), workspaceName: kind === 'invited' ? null : this.workspaceName.trim() || null,
        legalAcceptances: this.legal()!, mfaCode: this.needsCode() ? this.mfaCode.trim() : null
      });
      if (this.newsletter) void this.subscribe();
      this.done.set(outcome.verificationEmail);
    } catch (error) {
      const code = problemCode(error);
      if (code === 'Auth.ExistingAccountMfaRequired') this.needsCode.set(true);
      if (code === 'Validation.Password') this.errors.set({ password: problemText(error, PASSWORD_RULE) });
      else this.errors.set(fieldErrors(error));
      this.error.set(code === 'Identity.UserExists'
        ? 'Bu e-postayla maxkampüs hesabınız var. Giriş yapın; şifrenizi hatırlamıyorsanız yenileyin.'
        : problemText(error, 'Hesap açılamadı. Bilgilerinizi kontrol edip yeniden deneyin.'));
    } finally {
      this.busy.set(false);
    }
  }

  async resend(): Promise<void> {
    this.busy.set(true);
    try { await this.api.resendVerification(this.email.trim()); this.resent.set(true); }
    catch (error) { this.error.set(problemText(error, 'Bağlantı gönderilemedi. Biraz sonra yeniden deneyin.')); }
    finally { this.busy.set(false); }
  }

  async google(idToken: string): Promise<void> {
    this.busy.set(true);
    this.error.set('');
    try {
      const answer = await this.api.google(idToken);
      if (answer.registrationToken) {
        this.signup.start(answer.registrationToken);
        await this.router.navigate(['/kayit/google'], { queryParams: this.kind() && this.kind() !== 'invited' ? { tur: this.kind() } : {} });
        return;
      }
      // The Google account already has maxkampüs: this is a sign-in.
      if (this.session.apply(answer)) await this.router.navigateByUrl('/panel');
      else this.error.set('Bu Google hesabıyla maxkampüs hesabınız var. Giriş sayfasından Google ile devam edin.');
    } catch (error) {
      this.error.set(googleProblem(error));
    } finally {
      this.busy.set(false);
    }
  }

  private async subscribe(): Promise<void> {
    try {
      const captchaToken = await this.recaptcha.createToken('newsletter_subscribe').catch(() => null);
      await this.api.subscribe({ email: this.email.trim(), consent: true, captchaToken, source: 'kayit' });
    } catch { /* the registration does not depend on the newsletter */ }
  }
}
