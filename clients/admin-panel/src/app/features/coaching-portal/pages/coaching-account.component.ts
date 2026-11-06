import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { getApiErrorMessage } from '../../../core/http/api-error-message';
import { IdentityService, UserProfileDto } from '../../../core/services/identity.service';
import { isPasswordPolicySatisfied, PASSWORD_MAX_LENGTH } from '../../../core/validators/password.validator';
import { PasswordRequirementsComponent } from '../../../shared/components/password-requirements/password-requirements.component';
import { coachingFeedback } from '../coaching-feedback';
import { coachingDate } from '../coaching-labels';

type FormName = 'name' | 'email' | 'password';
type FieldName = 'firstName' | 'lastName' | 'newEmail' | 'emailPassword' | 'emailMfaCode' | 'currentPassword' | 'newPassword' | 'confirmPassword';
const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const MFA_CODE = /^\d{6}$/;
const voice = { voice: 'sen' as const };

/**
 * E5 (#22, live test 06.11.2026): Profil ve ayarlar › Hesabım for students, and the parent's Hesabım page. The name,
 * the e-mail address (changed only through a link sent to the new address) and the password, through the shared
 * Identity endpoints the management profile page and the staff portal also use. An account made with Google sign-in
 * has no password: it is told so and may ask for a password link. Forms follow the E4 #24 pattern: novalidate, field
 * errors next to the field (aria-invalid, aria-describedby) and in a summary, every attempt clears the last message.
 */
@Component({
  selector: 'app-coaching-account', standalone: true,
  imports: [FormsModule, RouterLink, PasswordRequirementsComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div class="cs-account" aria-labelledby="account-title">
    <header>
      <h1 id="account-title">Hesabım</h1>
      <p>Adın, e-posta adresin ve şifren.@if (!isParent) { Okul ve sınıf bilgilerin Profilim'de. }</p>
    </header>
    @if (loadError()) { <p class="ca-alert" role="alert">{{ loadError() }} <button type="button" (click)="load()">Yeniden dene</button></p> }
    @else if (!profile()) { <p role="status">Hesap bilgilerin yükleniyor…</p> }
    @if (profile(); as me) {
      <section aria-labelledby="account-name-title">
        <h2 id="account-name-title">Ad ve soyad</h2>
        <p>{{ isParent ? 'Bağlı olduğun hesaplar ve öğretmenler seni bu adla görür.' : 'Öğretmenlerin ve bağlı hesaplar seni bu adla görür.' }}</p>
        @if (summary('name'); as text) { <div class="ca-alert" role="alert" data-testid="name-form-summary">{{ text }}</div> }
        @if (status('name'); as text) { <p class="ca-success" role="status" data-testid="name-form-status">{{ text }}</p> }
        <form novalidate data-testid="name-form" (ngSubmit)="saveName()">
          <fieldset [disabled]="busy() === 'name'">
            <label>Ad <input name="firstName" autocomplete="given-name" maxlength="100" [(ngModel)]="firstName" [attr.aria-invalid]="invalid('firstName')" [attr.aria-describedby]="describedBy('firstName')">
              @if (fieldError('firstName'); as message) { <small class="ca-field-error" id="account-firstName-error">{{ message }}</small> }</label>
            <label>Soyad <input name="lastName" autocomplete="family-name" maxlength="100" [(ngModel)]="lastName" [attr.aria-invalid]="invalid('lastName')" [attr.aria-describedby]="describedBy('lastName')">
              @if (fieldError('lastName'); as message) { <small class="ca-field-error" id="account-lastName-error">{{ message }}</small> }</label>
            <button type="submit">{{ busy() === 'name' ? 'Kaydediliyor…' : 'Adımı kaydet' }}</button>
          </fieldset>
        </form>
      </section>

      <section aria-labelledby="account-email-title">
        <h2 id="account-email-title">E-posta adresi</h2>
        <p>Giriş yaptığın ve bildirimlerin geldiği adres: <strong>{{ me.email }}</strong></p>
        @if (summary('email'); as text) { <div class="ca-alert" role="alert" data-testid="email-form-summary">{{ text }}</div> }
        @if (status('email'); as text) { <p class="ca-success" role="status" data-testid="email-form-status">{{ text }}</p> }
        @if (me.pendingEmail) {
          <div class="ca-notice" data-testid="pending-email">
            <p><strong>{{ me.pendingEmail }}</strong> adresinin onayı bekleniyor. Bu adrese gönderdiğimiz bağlantıyı aç ve onayla; o zamana kadar e-posta adresin değişmez.@if (me.pendingEmailExpiresAt) { Bağlantı {{ date(me.pendingEmailExpiresAt) }} tarihine kadar geçerli. }</p>
            <button type="button" class="ca-secondary" data-testid="cancel-email-change" [disabled]="busy() === 'email'" (click)="cancelEmailChange()">Değişikliği iptal et</button>
          </div>
        }
        @if (me.hasPassword !== false) {
          <form novalidate data-testid="email-form" (ngSubmit)="requestEmailChange()">
            <fieldset [disabled]="busy() === 'email'">
              <label>Yeni e-posta adresi <input name="newEmail" type="email" autocomplete="email" maxlength="255" [(ngModel)]="newEmail" [attr.aria-invalid]="invalid('newEmail')" [attr.aria-describedby]="describedBy('newEmail')">
                @if (fieldError('newEmail'); as message) { <small class="ca-field-error" id="account-newEmail-error">{{ message }}</small> }</label>
              <label>Mevcut şifren <input name="emailPassword" type="password" autocomplete="current-password" maxlength="128" [(ngModel)]="emailPassword" [attr.aria-invalid]="invalid('emailPassword')" [attr.aria-describedby]="describedBy('emailPassword')">
                @if (fieldError('emailPassword'); as message) { <small class="ca-field-error" id="account-emailPassword-error">{{ message }}</small> }</label>
              @if (me.mfaEnabled) {
                <label>Doğrulama kodu <input name="emailMfaCode" inputmode="numeric" autocomplete="one-time-code" maxlength="6" [(ngModel)]="emailMfaCode" [attr.aria-invalid]="invalid('emailMfaCode')" [attr.aria-describedby]="mfaCodeDescribedBy()">
                  <small class="ca-hint" id="account-emailMfaCode-hint">İki adımlı doğrulama açık: doğrulama uygulamandaki 6 haneli kodu yaz.</small>
                  @if (fieldError('emailMfaCode'); as message) { <small class="ca-field-error" id="account-emailMfaCode-error">{{ message }}</small> }</label>
              }
              <button type="submit">{{ busy() === 'email' ? 'Gönderiliyor…' : 'Doğrulama bağlantısı gönder' }}</button>
            </fieldset>
          </form>
        } @else {
          <p class="ca-notice">Hesabın için henüz bir şifre belirlenmemiş. E-posta adresini değiştirmek için önce aşağıdan bir şifre belirle.</p>
        }
      </section>

      <section aria-labelledby="account-password-title">
        <h2 id="account-password-title">Şifre</h2>
        @if (me.hasPassword !== false) {
          <p>Şifreni değiştirdiğinde bütün cihazlardaki oturumların kapanır.</p>
          @if (summary('password'); as text) { <div class="ca-alert" role="alert" data-testid="password-form-summary">{{ text }}</div> }
          @if (status('password'); as text) {
            <div class="ca-success" role="status" data-testid="password-form-status">{{ text }}
              <button type="button" class="ca-secondary" data-testid="sign-in-again" (click)="signInAgain()">Yeniden giriş yap</button></div>
          }
          <form novalidate data-testid="password-form" (ngSubmit)="changePassword()">
            <fieldset [disabled]="busy() === 'password'">
              <label>Mevcut şifren <input name="currentPassword" type="password" autocomplete="current-password" maxlength="128" [(ngModel)]="currentPassword" [attr.aria-invalid]="invalid('currentPassword')" [attr.aria-describedby]="describedBy('currentPassword')">
                @if (fieldError('currentPassword'); as message) { <small class="ca-field-error" id="account-currentPassword-error">{{ message }}</small> }</label>
              <label>Yeni şifre <input name="newPassword" type="password" autocomplete="new-password" [attr.maxlength]="maxPassword" [(ngModel)]="newPassword" [attr.aria-invalid]="invalid('newPassword')" [attr.aria-describedby]="passwordDescribedBy()">
                <app-password-requirements [password]="newPassword" describedById="account-password-rules" />
                @if (fieldError('newPassword'); as message) { <small class="ca-field-error" id="account-newPassword-error">{{ message }}</small> }</label>
              <label>Yeni şifre (tekrar) <input name="confirmPassword" type="password" autocomplete="new-password" [attr.maxlength]="maxPassword" [(ngModel)]="confirmPassword" [attr.aria-invalid]="invalid('confirmPassword')" [attr.aria-describedby]="describedBy('confirmPassword')">
                @if (fieldError('confirmPassword'); as message) { <small class="ca-field-error" id="account-confirmPassword-error">{{ message }}</small> }</label>
              <button type="submit">{{ busy() === 'password' ? 'Değiştiriliyor…' : 'Şifremi değiştir' }}</button>
            </fieldset>
          </form>
        } @else {
          <p>Hesabın için henüz bir şifre belirlenmemiş (örneğin Google ile kaydolduysan). İstersen <strong>{{ me.email }}</strong> adresine şifre belirleme bağlantısı gönderelim.</p>
          @if (summary('password'); as text) { <div class="ca-alert" role="alert">{{ text }}</div> }
          @if (status('password'); as text) { <p class="ca-success" role="status" data-testid="password-setup-status">{{ text }}</p> }
          <button type="button" data-testid="request-password-setup" [disabled]="busy() === 'password'" (click)="requestPasswordSetup()">Şifre belirleme bağlantısı gönder</button>
        }
      </section>

      @if (me.mfaEnabled) {
        <section aria-labelledby="account-mfa-title" data-testid="mfa-status">
          <h2 id="account-mfa-title">İki adımlı doğrulama</h2>
          <p>İki adımlı doğrulama açık: girişte doğrulama uygulamandaki kod da istenir. Telefonunu kaybettiysen <a routerLink="/auth/support">destek</a> sayfasından bize yaz.</p>
        </section>
      }
    }
  </div>`,
  styles: [`
    form, fieldset { display: grid; gap: var(--coaching-space-3, 12px); margin: 0; padding: 0; border: 0; min-width: 0; }
    label { display: grid; gap: 4px; font-weight: 650; color: var(--ui-text); }
    input { min-height: 44px; padding: 0 12px; border: 1px solid var(--ui-border); border-radius: var(--coaching-radius-control, 10px); background: var(--ui-surface); color: var(--ui-text); font: inherit; font-weight: 400; }
    input[aria-invalid="true"] { border-color: var(--ui-danger); }
    button { justify-self: start; min-height: 44px; padding: 0 var(--coaching-space-5, 20px); border: 1px solid var(--ui-brand); border-radius: var(--coaching-radius-control, 10px); background: var(--ui-brand); color: var(--ui-brand-contrast); font-weight: 700; cursor: pointer; }
    button:disabled { opacity: .55; cursor: not-allowed; }
    .ca-secondary { min-height: 40px; background: transparent; color: var(--ui-brand-text, var(--ui-brand)); }
    .ca-field-error { color: var(--ui-danger); font-weight: 600; }
    .ca-hint { color: var(--ui-text-muted); font-weight: 400; }
    .ca-alert, .ca-success, .ca-notice { display: grid; gap: 8px; margin: 0 0 12px; padding: 12px 16px; border-radius: var(--coaching-radius-control, 10px); }
    .ca-alert { background: var(--ui-danger-soft); color: var(--ui-danger); font-weight: 650; }
    .ca-success { background: var(--ui-success-soft); color: var(--ui-success); font-weight: 650; }
    .ca-notice { border: 1px solid var(--ui-border); background: var(--ui-surface-muted); color: var(--ui-text); }
    .ca-notice p { margin: 0; }
  `]
})
export class CoachingAccountComponent implements OnInit {
  private readonly identity = inject(IdentityService);
  private readonly auth = inject(AuthService);
  /** Review L8: the parent's page has no student copy (route data audience: 'parent'). */
  readonly isParent = inject(ActivatedRoute, { optional: true })?.snapshot?.data?.['audience'] === 'parent';

  readonly profile = signal<UserProfileDto | null>(null);
  readonly loadError = signal<string | null>(null);
  readonly busy = signal<FormName | null>(null);
  readonly fieldErrors = signal<Partial<Record<FieldName, string>>>({});
  readonly summaries = signal<Partial<Record<FormName, string>>>({});
  readonly statuses = signal<Partial<Record<FormName, string>>>({});
  readonly maxPassword = PASSWORD_MAX_LENGTH;

  firstName = '';
  lastName = '';
  newEmail = '';
  emailPassword = '';
  emailMfaCode = '';
  currentPassword = '';
  newPassword = '';
  confirmPassword = '';

  private readonly lastError = signal<string | null>(null);
  private readonly lastStatus = signal<string | null>(null);

  constructor() { coachingFeedback(this.lastStatus, this.lastError); }

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loadError.set(null);
    this.identity.getMyProfile().subscribe({
      next: profile => { this.profile.set(profile); this.firstName = profile.firstName ?? ''; this.lastName = profile.lastName ?? ''; },
      error: error => this.loadError.set(getApiErrorMessage(error, 'Hesap bilgilerin yüklenemedi. Sayfayı yenileyip yeniden dene.', voice))
    });
  }

  date(value: string) { return coachingDate(value); }
  fieldError(name: FieldName) { return this.fieldErrors()[name] ?? null; }
  invalid(name: FieldName) { return this.fieldErrors()[name] ? 'true' : null; }
  describedBy(name: FieldName) { return this.fieldErrors()[name] ? `account-${name}-error` : null; }
  mfaCodeDescribedBy() { return ['account-emailMfaCode-hint', this.describedBy('emailMfaCode')].filter(Boolean).join(' '); }
  passwordDescribedBy() { return ['account-password-rules', this.describedBy('newPassword')].filter(Boolean).join(' '); }
  summary(form: FormName) { return this.summaries()[form] ?? null; }
  status(form: FormName) { return this.statuses()[form] ?? null; }

  saveName(): void {
    this.begin('name', ['firstName', 'lastName']);
    const firstName = this.firstName.trim(), lastName = this.lastName.trim();
    const errors: Partial<Record<FieldName, string>> = {};
    // Review L7: the server's rules — not blank, at most 100 characters.
    if (!firstName) errors.firstName = 'Adını yaz.';
    else if (firstName.length > 100) errors.firstName = 'Adın en fazla 100 karakter olabilir.';
    if (!lastName) errors.lastName = 'Soyadını yaz.';
    else if (lastName.length > 100) errors.lastName = 'Soyadın en fazla 100 karakter olabilir.';
    if (this.reject('name', errors, 'Adın kaydedilmedi.')) return;
    this.busy.set('name');
    this.identity.updateMyProfile({ firstName, lastName }).pipe(finalize(() => this.busy.set(null))).subscribe({
      next: () => {
        this.profile.update(p => p ? { ...p, firstName, lastName, fullName: `${firstName} ${lastName}` } : p);
        this.firstName = firstName; this.lastName = lastName;
        // The header reads the name from the session token: a renewed session shows the new name.
        void this.auth.refreshSession();
        this.succeed('name', 'Adın ve soyadın kaydedildi.');
      },
      error: error => this.fail('name', getApiErrorMessage(error, 'Adın kaydedilemedi. Bağlantını kontrol edip yeniden dene.', voice))
    });
  }

  requestEmailChange(): void {
    this.begin('email', ['newEmail', 'emailPassword', 'emailMfaCode']);
    const newEmail = this.newEmail.trim().toLowerCase();
    const errors: Partial<Record<FieldName, string>> = {};
    if (!newEmail) errors.newEmail = 'Yeni e-posta adresini yaz.';
    else if (!EMAIL.test(newEmail)) errors.newEmail = 'Yeni adres için geçerli bir e-posta adresi yaz (ör. ad@ornek.com).';
    else if (newEmail === this.profile()?.email.toLowerCase()) errors.newEmail = 'Bu zaten şu anki e-posta adresin.';
    if (!this.emailPassword) errors.emailPassword = 'Mevcut şifreni yaz.';
    // Paket F L6: with two-step verification on, the authenticator code is asked too.
    const mfaEnabled = this.profile()?.mfaEnabled === true;
    const mfaCode = this.emailMfaCode.replace(/\s/g, '');
    if (mfaEnabled && !MFA_CODE.test(mfaCode)) errors.emailMfaCode = 'Doğrulama uygulamandaki 6 haneli kodu yaz.';
    if (this.reject('email', errors, 'Doğrulama bağlantısı gönderilmedi.')) return;
    this.busy.set('email');
    this.identity.requestEmailChange(this.emailPassword, newEmail, mfaEnabled ? mfaCode : undefined).pipe(finalize(() => this.busy.set(null))).subscribe({
      next: result => {
        this.profile.update(p => p ? { ...p, pendingEmail: result?.pendingEmail ?? newEmail, pendingEmailExpiresAt: result?.expiresAt ?? null } : p);
        this.newEmail = ''; this.emailPassword = ''; this.emailMfaCode = '';
        this.succeed('email', `Doğrulama bağlantısını ${result?.pendingEmail ?? newEmail} adresine gönderdik. Bağlantıyı açıp onaylayana kadar e-posta adresin değişmez.`);
      },
      error: error => this.apiFail('email', error, 'Doğrulama bağlantısı gönderilemedi. Yeniden dene.', {
        'Auth.InvalidCredentials': 'emailPassword', 'Account.SameEmail': 'newEmail', 'Account.EmailInUse': 'newEmail',
        'Account.MfaCodeRequired': 'emailMfaCode', 'Auth.InvalidMfaCode': 'emailMfaCode', 'Auth.MfaLocked': 'emailMfaCode' })
    });
  }

  cancelEmailChange(): void {
    this.begin('email', []);
    this.busy.set('email');
    this.identity.cancelEmailChange().pipe(finalize(() => this.busy.set(null))).subscribe({
      next: () => {
        this.profile.update(p => p ? { ...p, pendingEmail: null, pendingEmailExpiresAt: null } : p);
        this.succeed('email', 'E-posta değişikliği iptal edildi; gönderdiğimiz bağlantı artık çalışmaz.');
      },
      error: error => this.fail('email', getApiErrorMessage(error, 'Değişiklik iptal edilemedi. Yeniden dene.', voice))
    });
  }

  changePassword(): void {
    this.begin('password', ['currentPassword', 'newPassword', 'confirmPassword']);
    const errors: Partial<Record<FieldName, string>> = {};
    if (!this.currentPassword) errors.currentPassword = 'Mevcut şifreni yaz.';
    if (!isPasswordPolicySatisfied(this.newPassword))
      errors.newPassword = 'En az 8 karakter (en fazla 128); büyük harf, küçük harf, rakam ve özel karakter olmalı.';
    if (!this.confirmPassword || this.confirmPassword !== this.newPassword) errors.confirmPassword = 'Yeni şifreyi aynı şekilde tekrar yaz.';
    if (this.reject('password', errors, 'Şifren değiştirilmedi.')) return;
    this.busy.set('password');
    this.identity.changeMyPassword({ currentPassword: this.currentPassword, newPassword: this.newPassword })
      .pipe(finalize(() => this.busy.set(null))).subscribe({
        next: () => {
          this.currentPassword = ''; this.newPassword = ''; this.confirmPassword = '';
          this.succeed('password', 'Şifren değiştirildi. Güvenliğin için bütün oturumların kapatıldı; yeni şifrenle yeniden giriş yap.');
        },
        error: error => this.apiFail('password', error, 'Şifren değiştirilemedi. Yeniden dene.', {
          'Auth.InvalidCredentials': 'currentPassword' })
      });
  }

  requestPasswordSetup(): void {
    const email = this.profile()?.email;
    if (!email) return;
    this.begin('password', []);
    this.busy.set('password');
    this.identity.forgotPassword(email).pipe(finalize(() => this.busy.set(null))).subscribe({
      next: () => this.succeed('password', `Şifre belirleme bağlantısını ${email} adresine gönderdik. Bağlantı 2 saat geçerli.`),
      error: error => this.fail('password', getApiErrorMessage(error, 'Bağlantı gönderilemedi. Biraz sonra yeniden dene.', voice))
    });
  }

  signInAgain(): void { void this.auth.logout(); }

  /** E4 #24: every attempt starts without the previous message of that form. */
  private begin(form: FormName, fields: FieldName[]) {
    this.summaries.update(s => ({ ...s, [form]: undefined }));
    this.statuses.update(s => ({ ...s, [form]: undefined }));
    this.fieldErrors.update(errors => Object.fromEntries(Object.entries(errors).filter(([key]) => !fields.includes(key as FieldName))));
  }

  private reject(form: FormName, errors: Partial<Record<FieldName, string>>, lead: string): boolean {
    const messages = Object.values(errors);
    if (!messages.length) return false;
    this.fieldErrors.update(current => ({ ...current, ...errors }));
    this.fail(form, `${lead} ${messages.join(' ')}`);
    return true;
  }

  private apiFail(form: FormName, error: unknown, fallback: string, fieldsByCode: Record<string, FieldName>) {
    const message = getApiErrorMessage(error, fallback, voice);
    const body = (error as { error?: { error?: { code?: string }; Error?: { code?: string }; code?: string } })?.error;
    const code = body?.error?.code ?? body?.Error?.code ?? body?.code;
    const field = code ? fieldsByCode[code] : undefined;
    if (field) this.fieldErrors.update(current => ({ ...current, [field]: message }));
    this.fail(form, message);
  }

  private fail(form: FormName, message: string) {
    this.summaries.update(s => ({ ...s, [form]: message }));
    this.lastError.set(message);
  }

  private succeed(form: FormName, message: string) {
    this.statuses.update(s => ({ ...s, [form]: message }));
    this.lastStatus.set(message);
  }
}
