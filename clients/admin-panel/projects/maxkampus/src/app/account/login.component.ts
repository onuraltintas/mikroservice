import { Component, OnInit, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { GooglePending, MaxkampusApi } from '../core/api';
import { problemCode, problemText } from '../core/problem';
import { Seo } from '../core/seo';
import { MaxkampusSession, SessionResponse } from '../core/session.service';
import { AuthLayoutComponent } from './auth-layout.component';
import { GoogleButtonComponent } from './google-button.component';
import { GoogleSignup } from './google-signup';

type Stage = 'form' | 'verify' | 'setup' | 'recovery-codes';

/** /giris: e-mail and password or Google; two-step verification as on the other eduivme products. */
@Component({
  selector: 'mk-login',
  standalone: true,
  imports: [FormsModule, RouterLink, AuthLayoutComponent, GoogleButtonComponent],
  template: `
    <mk-auth-layout>
      @switch (stage()) {
        @case ('form') {
          <div class="ds-auth__heading"><h1 class="ds-auth__title">Giriş yapın</h1>
            <p class="ds-auth__lead">maxkoç ya da Master Hızlı Okuma hesabınız varsa aynı e-posta ve şifreyle maxkampüs'e katılabilirsiniz: <a routerLink="/kayit">Kayıt olun</a>.</p></div>
          @if (notice()) { <p class="mk-success" role="status">{{ notice() }}</p> }
          <form class="ds-auth__form" (ngSubmit)="login()">
            <label class="ds-field"><span class="ds-field__label">E-posta</span>
              <input class="ds-input" type="email" name="email" [(ngModel)]="email" autocomplete="username" required></label>
            <div class="ds-field">
              <span class="ds-field__row"><label for="password">Şifre</label><a class="ds-link" routerLink="/sifremi-unuttum">Şifremi unuttum</a></span>
              <input class="ds-input" id="password" type="password" name="password" [(ngModel)]="password" autocomplete="current-password" required>
            </div>
            <label class="ds-check"><input type="checkbox" name="remember" [(ngModel)]="remember"> Beni hatırla</label>
            @if (error()) { <p class="ds-alert" role="alert">{{ error() }}</p> }
            <button class="ds-btn ds-btn--primary ds-btn--large ds-btn--block" type="submit" [disabled]="busy()">{{ busy() ? 'Giriş yapılıyor' : 'Giriş yap' }}</button>
          </form>
          <p class="ds-divider">ya da</p>
          <mk-google-button (token)="google($event)" />
          <div class="ds-auth__footer"><p>Hesabınız yok mu? <a class="ds-link" routerLink="/kayit">Kurumunuzu açın</a></p></div>
        }
        @case ('verify') {
          <div class="ds-auth__heading"><h1 class="ds-auth__title">Girişinizi doğrulayın</h1>
            <p class="ds-auth__lead">Doğrulama uygulamanızdaki altı haneli kodu yazın.</p></div>
          <form class="ds-auth__form" (ngSubmit)="verify()">
            <label class="ds-field"><span class="ds-field__label">Altı haneli kod</span>
              <input class="ds-input ds-input--code" name="code" [(ngModel)]="code" inputmode="numeric" autocomplete="one-time-code" maxlength="6"></label>
            @if (useRecovery()) {
              <label class="ds-field"><span class="ds-field__label">Tek kullanımlık kurtarma kodu</span>
                <input class="ds-input" name="recovery" [(ngModel)]="recovery" autocomplete="one-time-code"></label>
            } @else {
              <button type="button" class="ds-btn ds-btn--quiet" (click)="useRecovery.set(true)">Kurtarma koduyla gir</button>
            }
            @if (error()) { <p class="ds-alert" role="alert">{{ error() }}</p> }
            <button class="ds-btn ds-btn--primary ds-btn--large ds-btn--block" type="submit" [disabled]="busy()">Doğrula ve devam et</button>
          </form>
        }
        @case ('setup') {
          <div class="ds-auth__heading"><h1 class="ds-auth__title">İki adımlı doğrulamayı kurun</h1>
            <p class="ds-auth__lead">Hesabınız için iki adımlı doğrulama gerekiyor. Doğrulama uygulamanıza (Google Authenticator, Microsoft Authenticator gibi) bu anahtarı ekleyin, sonra uygulamanın gösterdiği kodu yazın.</p></div>
          <div class="ds-field"><span class="ds-field__label">Kurulum anahtarı</span><code class="ds-code">{{ secret() }}</code></div>
          <form class="ds-auth__form" (ngSubmit)="enable()">
            <label class="ds-field"><span class="ds-field__label">Altı haneli kod</span>
              <input class="ds-input ds-input--code" name="code" [(ngModel)]="code" inputmode="numeric" autocomplete="one-time-code" maxlength="6"></label>
            @if (error()) { <p class="ds-alert" role="alert">{{ error() }}</p> }
            <button class="ds-btn ds-btn--primary ds-btn--large ds-btn--block" type="submit" [disabled]="busy()">Kur ve devam et</button>
          </form>
        }
        @case ('recovery-codes') {
          <div class="ds-auth__heading"><h1 class="ds-auth__title">Kurtarma kodlarınız</h1>
            <p class="ds-auth__lead">Telefonunuza ulaşamazsanız bu kodlarla girersiniz. Her kod bir kez kullanılır; güvenli bir yere kaydedin.</p></div>
          <ul class="ds-code-list">@for (item of recoveryCodes(); track item) { <li><code>{{ item }}</code></li> }</ul>
          <button type="button" class="ds-btn ds-btn--primary ds-btn--large ds-btn--block" (click)="continue()">Kaydettim, devam et</button>
        }
      }
    </mk-auth-layout>
  `
})
export class LoginComponent implements OnInit {
  private readonly api = inject(MaxkampusApi);
  private readonly session = inject(MaxkampusSession);
  private readonly router = inject(Router);
  private readonly seo = inject(Seo);
  private readonly googleSignup = inject(GoogleSignup);
  /** Where to go after sign-in (?donus=/davet/…); only paths inside maxkampus.com. */
  readonly donus = input<string | undefined>();
  readonly stage = signal<Stage>('form');
  readonly busy = signal(false);
  readonly error = signal('');
  readonly notice = signal('');
  readonly useRecovery = signal(false);
  readonly secret = signal('');
  readonly recoveryCodes = signal<string[]>([]);
  email = ''; password = ''; remember = false; code = ''; recovery = '';
  private challenge = '';
  private setupToken = '';

  async ngOnInit(): Promise<void> {
    this.seo.set({ title: 'Giriş yapın', path: '/giris', noindex: true });
    if (await this.session.ensure()) await this.continue();
  }

  async login(): Promise<void> {
    if (!this.email.includes('@') || !this.password) { this.error.set('E-posta adresinizi ve şifrenizi yazın.'); return; }
    await this.run(() => this.api.login(this.email.trim(), this.password, this.remember));
  }

  async google(idToken: string): Promise<void> {
    this.busy.set(true);
    this.error.set('');
    try {
      const answer = await this.api.google(idToken);
      if (answer.registrationToken) {
        // A Google account without maxkampüs: the registration form comes first.
        this.googleSignup.start(answer.registrationToken);
        await this.router.navigateByUrl('/kayit/google');
        return;
      }
      await this.handle(answer);
    } catch (error) {
      this.error.set(googleProblem(error));
    } finally {
      this.busy.set(false);
    }
  }

  async verify(): Promise<void> {
    const code = this.code.trim();
    const recovery = this.recovery.trim();
    if (!/^\d{6}$/.test(code) && !recovery) { this.error.set('Altı haneli kodu ya da bir kurtarma kodunu yazın.'); return; }
    await this.run(() => this.api.verifyMfa(this.challenge, recovery ? null : code, recovery || null));
  }

  async enable(): Promise<void> {
    if (!/^\d{6}$/.test(this.code.trim())) { this.error.set('Uygulamadaki altı haneli kodu yazın.'); return; }
    await this.run(() => this.api.enableMfa(this.challenge, this.setupToken, this.code.trim()));
  }

  async continue(): Promise<void> {
    const target = this.donus();
    await this.router.navigateByUrl(target && target.startsWith('/') && !target.startsWith('//') ? target : '/panel');
  }

  private async run(call: () => Promise<SessionResponse>): Promise<void> {
    this.busy.set(true);
    this.error.set('');
    try { await this.handle(await call()); }
    catch (error) { this.error.set(loginProblem(error)); }
    finally { this.busy.set(false); }
  }

  private async handle(answer: SessionResponse & Partial<GooglePending>): Promise<void> {
    this.code = '';
    if (answer.requiresMfa && answer.mfaChallengeToken) {
      this.challenge = answer.mfaChallengeToken;
      if (answer.mfaEnrollmentRequired) {
        const setup = await this.api.startMfaSetup(this.challenge);
        this.secret.set(setup.secret);
        this.setupToken = setup.setupToken;
        this.stage.set('setup');
      } else {
        this.stage.set('verify');
      }
      return;
    }
    if (!this.session.apply(answer)) { this.error.set('Oturum açılamadı. Yeniden deneyin.'); return; }
    if (answer.recoveryCodes?.length) { this.recoveryCodes.set(answer.recoveryCodes); this.stage.set('recovery-codes'); return; }
    await this.continue();
  }
}

export function loginProblem(error: unknown): string {
  switch (problemCode(error)) {
    case 'Auth.NoRoleForProduct':
    case 'Auth.ProductAccessDenied':
      return 'Bu hesap maxkampüs\'e kayıtlı değil. maxkoç ya da Master Hızlı Okuma hesabınızla katılmak için Kayıt ol sayfasında aynı e-posta ve şifreyi kullanın.';
    case 'Auth.EmailNotConfirmed':
      return 'E-posta adresiniz henüz doğrulanmadı. Gelen kutunuzdaki bağlantıya tıklayın; e-posta gelmediyse Kayıt ol sayfasından yeniden isteyin.';
    default:
      return problemText(error, 'Giriş yapılamadı. E-posta adresinizi ve şifrenizi kontrol edin.');
  }
}

export function googleProblem(error: unknown): string {
  switch (problemCode(error)) {
    case 'Auth.NoRoleForProduct':
    case 'Auth.ProductAccessDenied':
      return 'Bu Google hesabının e-postasıyla bir eduivme hesabı var. maxkampüs\'e katılmak için Kayıt ol sayfasında o hesabın e-posta ve şifresini kullanın.';
    case 'Auth.ExternalLoginLinkRequired':
      return 'Bu e-postayla şifreli bir hesabınız var. E-posta ve şifrenizle giriş yapın.';
    default:
      return problemText(error, 'Google ile giriş yapılamadı. Yeniden deneyin.');
  }
}
