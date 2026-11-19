import { Component, OnInit, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { LegalAcceptance, MaxkampusApi } from '../core/api';
import { problemCode, problemText } from '../core/problem';
import { Seo } from '../core/seo';
import { MaxkampusSession } from '../core/session.service';
import { AuthLayoutComponent } from './auth-layout.component';
import { GoogleSignup } from './google-signup';
import { LegalConsentComponent } from './legal-consent.component';
import { RegistrationKind, phoneLooksValid } from './register.component';

/**
 * /kayit/google: after Google, a new account says what it is (institution or private lessons, or the invitation it
 * came with), its workspace and phone, and accepts the texts. Identity creates nothing before this step.
 */
@Component({
  selector: 'mk-google-finish',
  standalone: true,
  imports: [FormsModule, RouterLink, AuthLayoutComponent, LegalConsentComponent],
  template: `
    <mk-auth-layout headline="Kaydınızı tamamlayın">
      <div class="ds-auth__heading"><h1 class="ds-auth__title">Kaydınızı tamamlayın</h1>
        <p class="ds-auth__lead">Google hesabınızla devam ediyorsunuz. Adınız ve e-postanız Google'dan gelir.</p></div>
      @if (!token()) {
        <p class="ds-alert" role="alert">Kayıt işleminizin süresi doldu. <a routerLink="/kayit">Kayıt sayfasına dönüp</a> Google ile yeniden devam edin.</p>
      } @else {
        <form class="ds-auth__form" (ngSubmit)="submit()" novalidate>
          @if (!invite) {
            <fieldset class="ds-fieldset kinds"><legend>Hesap türü</legend>
              <label class="ds-check"><input type="radio" name="kind" value="institution" [(ngModel)]="kind"> Kurumum var</label>
              <label class="ds-check"><input type="radio" name="kind" value="teacher" [(ngModel)]="kind"> Özel ders veriyorum</label>
            </fieldset>
            <label class="ds-field"><span class="ds-field__label">{{ kind === 'institution' ? 'Kurumunuzun adı' : 'Çalışma alanınızın adı (isteğe bağlı)' }}</span>
              <input class="ds-input" name="workspaceName" [(ngModel)]="workspaceName" maxlength="200" autocomplete="organization"></label>
          } @else {
            <p class="mk-notice">Öğretmen davetinizle devam ediyorsunuz. Davet, Google hesabınızın e-postasına gönderilmiş olmalı.</p>
          }
          <label class="ds-field"><span class="ds-field__label">Telefon</span>
            <input class="ds-input" type="tel" name="phone" [(ngModel)]="phone" autocomplete="tel" placeholder="0532 123 45 67"></label>
          <mk-legal-consent (accepted)="legal.set($event)" />
          @if (error()) { <p class="ds-alert" role="alert">{{ error() }}</p> }
          <button class="ds-btn ds-btn--primary ds-btn--large ds-btn--block" type="submit" [disabled]="busy()">{{ busy() ? 'Kaydediliyor' : 'Hesabı aç' }}</button>
        </form>
      }
    </mk-auth-layout>
  `,
  styles: `.kinds { padding-bottom: 8px; }`
})
export class GoogleFinishComponent implements OnInit {
  private readonly api = inject(MaxkampusApi);
  private readonly session = inject(MaxkampusSession);
  private readonly router = inject(Router);
  private readonly seo = inject(Seo);
  private readonly signup = inject(GoogleSignup);
  readonly tur = input<string | undefined>();
  readonly token = signal<string | null>(null);
  readonly legal = signal<LegalAcceptance[] | null>(null);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly invite = this.signup.invite();
  kind: RegistrationKind = 'institution';
  workspaceName = '';
  phone = '';

  ngOnInit(): void {
    this.seo.set({ title: 'Kaydınızı tamamlayın', path: '/kayit/google', noindex: true });
    this.token.set(this.signup.token());
    if (this.tur() === 'teacher') this.kind = 'teacher';
    if (this.invite) this.kind = 'invited';
  }

  async submit(): Promise<void> {
    this.error.set('');
    if (this.kind === 'institution' && !this.workspaceName.trim()) { this.error.set('Kurumunuzun adını yazın.'); return; }
    if (!phoneLooksValid(this.phone)) { this.error.set('Telefon numaranızı yazın (örnek: 0532 123 45 67).'); return; }
    if (!this.legal()) { this.error.set('Devam etmek için metinleri okuyup onaylayın.'); return; }
    this.busy.set(true);
    try {
      const answer = await this.api.completeGoogle({ registrationToken: this.token()!, kind: this.kind, phone: this.phone.trim(),
        workspaceName: this.kind === 'invited' ? null : this.workspaceName.trim() || null, legalAcceptances: this.legal()! });
      this.signup.clear();
      if (answer.requiresMfa || !this.session.apply(answer)) { await this.router.navigateByUrl('/giris'); return; }
      await this.router.navigateByUrl(this.invite ? `/davet/${this.invite}` : '/panel');
    } catch (error) {
      if (problemCode(error) === 'Auth.GoogleRegistrationExpired') { this.signup.clear(); this.token.set(null); }
      this.error.set(problemText(error, 'Hesap açılamadı. Bilgilerinizi kontrol edip yeniden deneyin.'));
    } finally {
      this.busy.set(false);
    }
  }
}
