import { Component, OnInit, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MaxkampusApi } from '../core/api';
import { problemCode, problemText } from '../core/problem';
import { Seo } from '../core/seo';
import { AuthLayoutComponent } from './auth-layout.component';
import { GoogleSignup } from './google-signup';
import { PASSWORD_RULE, passwordLooksValid } from './register.component';

/** /sifremi-unuttum: a reset link to the address (the answer is the same whether the account exists or not). */
@Component({
  selector: 'mk-forgot',
  standalone: true,
  imports: [FormsModule, RouterLink, AuthLayoutComponent],
  template: `
    <mk-auth-layout headline="Şifrenizi yenileyin">
      <a class="ds-back-link ds-link" routerLink="/giris">Girişe dön</a>
      <div class="ds-auth__heading"><h1 class="ds-auth__title">Şifremi unuttum</h1>
        <p class="ds-auth__lead">Hesabınızın e-posta adresini yazın; şifre yenileme bağlantısı gönderelim.</p></div>
      @if (sent()) {
        <p class="mk-success" role="status">Bu adrese kayıtlı bir hesap varsa yenileme bağlantısı birkaç dakika içinde gelir. Gelmediyse istenmeyen e-posta klasörüne bakın.</p>
      } @else {
        <form class="ds-auth__form" (ngSubmit)="send()">
          <label class="ds-field"><span class="ds-field__label">E-posta</span><input class="ds-input" type="email" name="email" [(ngModel)]="email" autocomplete="email"></label>
          @if (error()) { <p class="ds-alert" role="alert">{{ error() }}</p> }
          <button class="ds-btn ds-btn--primary ds-btn--large ds-btn--block" type="submit" [disabled]="busy()">Bağlantı gönder</button>
        </form>
      }
    </mk-auth-layout>
  `
})
export class ForgotComponent implements OnInit {
  private readonly api = inject(MaxkampusApi);
  private readonly seo = inject(Seo);
  readonly busy = signal(false);
  readonly sent = signal(false);
  readonly error = signal('');
  email = '';
  ngOnInit(): void { this.seo.set({ title: 'Şifremi unuttum', path: '/sifremi-unuttum', noindex: true }); }
  async send(): Promise<void> {
    if (!this.email.includes('@')) { this.error.set('Geçerli bir e-posta adresi yazın.'); return; }
    this.busy.set(true); this.error.set('');
    try { await this.api.forgotPassword(this.email.trim()); this.sent.set(true); }
    catch (error) { this.error.set(problemText(error, 'Bağlantı gönderilemedi. Biraz sonra yeniden deneyin.')); }
    finally { this.busy.set(false); }
  }
}

/** /sifre-yenile?token=&email=: the link of the reset e-mail. */
@Component({
  selector: 'mk-reset',
  standalone: true,
  imports: [FormsModule, RouterLink, AuthLayoutComponent],
  template: `
    <mk-auth-layout headline="Şifrenizi yenileyin">
      <div class="ds-auth__heading"><h1 class="ds-auth__title">Yeni şifre belirleyin</h1>
        @if (email()) { <p class="ds-auth__lead">{{ email() }} hesabı için.</p> }</div>
      @if (done()) {
        <p class="mk-success" role="status">Şifreniz yenilendi. Yeni şifrenizle giriş yapabilirsiniz.</p>
        <a class="ds-btn ds-btn--primary ds-btn--large ds-btn--block" routerLink="/giris">Giriş yap</a>
      } @else if (!token() || !email()) {
        <p class="ds-alert" role="alert">Bağlantı eksik ya da bozuk. <a routerLink="/sifremi-unuttum">Yeni bir bağlantı isteyin</a>.</p>
      } @else {
        <form class="ds-auth__form" (ngSubmit)="save()">
          <label class="ds-field"><span class="ds-field__label">Yeni şifre</span>
            <input class="ds-input" type="password" name="password" [(ngModel)]="password" autocomplete="new-password"><span class="ds-field__hint">{{ rule }}</span></label>
          <label class="ds-field"><span class="ds-field__label">Yeni şifre (tekrar)</span>
            <input class="ds-input" type="password" name="again" [(ngModel)]="again" autocomplete="new-password"></label>
          @if (error()) { <p class="ds-alert" role="alert">{{ error() }} @if (expired()) { <a routerLink="/sifremi-unuttum">Yeni bağlantı isteyin</a> }</p> }
          <button class="ds-btn ds-btn--primary ds-btn--large ds-btn--block" type="submit" [disabled]="busy()">Şifreyi kaydet</button>
        </form>
      }
    </mk-auth-layout>
  `
})
export class ResetComponent implements OnInit {
  private readonly api = inject(MaxkampusApi);
  private readonly seo = inject(Seo);
  readonly token = input<string | undefined>();
  readonly email = input<string | undefined>();
  readonly busy = signal(false);
  readonly done = signal(false);
  readonly error = signal('');
  readonly expired = signal(false);
  readonly rule = PASSWORD_RULE;
  password = ''; again = '';
  ngOnInit(): void { this.seo.set({ title: 'Yeni şifre', path: '/sifre-yenile', noindex: true }); }
  async save(): Promise<void> {
    this.error.set(''); this.expired.set(false);
    if (!passwordLooksValid(this.password)) { this.error.set(PASSWORD_RULE); return; }
    if (this.password !== this.again) { this.error.set('İki şifre aynı değil.'); return; }
    this.busy.set(true);
    try { await this.api.resetPassword(this.email()!, this.token()!, this.password); this.done.set(true); }
    catch (error) {
      this.expired.set(problemCode(error) === 'ResetPassword.InvalidRequest');
      this.error.set(problemText(error, 'Şifre yenilenemedi.'));
    } finally { this.busy.set(false); }
  }
}

/** /eposta-dogrula?userId=&token=: the verification link; then sign-in (and the invitation, if one is waiting). */
@Component({
  selector: 'mk-verify',
  standalone: true,
  imports: [RouterLink, AuthLayoutComponent],
  template: `
    <mk-auth-layout headline="E-posta adresinizi doğrulayın">
      <div class="ds-auth__heading"><h1 class="ds-auth__title">E-posta doğrulama</h1></div>
      @if (state() === 'working') { <p role="status"><span class="ds-spinner" aria-hidden="true"></span> Doğrulanıyor</p> }
      @else if (state() === 'ok') {
        <p class="mk-success" role="status">{{ already() ? 'Adresiniz daha önce doğrulanmış.' : 'E-posta adresiniz doğrulandı.' }} Giriş yapıp {{ invite ? 'davetinizi kabul edebilirsiniz' : 'planınızı seçebilirsiniz' }}.</p>
        <a class="ds-btn ds-btn--primary ds-btn--large ds-btn--block" routerLink="/giris" [queryParams]="invite ? { donus: '/davet/' + invite } : { donus: '/plan' }">Giriş yap</a>
      } @else {
        <p class="ds-alert" role="alert">{{ message() }}</p>
        <a class="ds-link" routerLink="/kayit">Kayıt sayfasından yeni bağlantı isteyin</a>
      }
    </mk-auth-layout>
  `
})
export class VerifyEmailComponent implements OnInit {
  private readonly api = inject(MaxkampusApi);
  private readonly seo = inject(Seo);
  readonly userId = input<string | undefined>();
  readonly token = input<string | undefined>();
  readonly state = signal<'working' | 'ok' | 'failed'>('working');
  readonly already = signal(false);
  readonly message = signal('');
  readonly invite = inject(GoogleSignup).invite();

  async ngOnInit(): Promise<void> {
    this.seo.set({ title: 'E-posta doğrulama', path: '/eposta-dogrula', noindex: true });
    if (!this.userId() || !this.token()) { this.message.set('Bağlantı eksik ya da bozuk.'); this.state.set('failed'); return; }
    try {
      const answer = await this.api.confirmEmail(this.userId()!, this.token()!);
      this.already.set(answer.status === 'alreadyConfirmed');
      this.state.set('ok');
    } catch (error) {
      this.message.set(problemCode(error) === 'User.TokenExpired'
        ? 'Bağlantının süresi dolmuş. Yeni bir doğrulama bağlantısı isteyin.'
        : problemText(error, 'Bağlantı geçersiz ya da süresi dolmuş.'));
      this.state.set('failed');
    }
  }
}
