import { Component, OnInit, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { MaxkampusApi, TeacherInvitationInfo } from '../core/api';
import { problemCode, problemText } from '../core/problem';
import { Seo } from '../core/seo';
import { MaxkampusSession } from '../core/session.service';
import { trDate } from '../core/text';
import { AuthLayoutComponent } from './auth-layout.component';
import { GoogleSignup } from './google-signup';

/** /davet/:kod: a teacher's invitation from an institution (the link in the invitation e-mail). */
@Component({
  selector: 'mk-invite',
  standalone: true,
  imports: [FormsModule, RouterLink, AuthLayoutComponent],
  template: `
    <mk-auth-layout [headline]="info() ? info()!.workspaceName + ' sizi davet etti' : 'Öğretmen daveti'">
      @if (loading()) { <p role="status"><span class="ds-spinner" aria-hidden="true"></span> Davet açılıyor</p> }
      @else if (!info()) {
        <div class="ds-auth__heading"><h1 class="ds-auth__title">Davet bulunamadı</h1></div>
        <p class="ds-alert" role="alert">Bu davet bağlantısı geçersiz. Kurumunuzdan daveti yeniden göndermesini isteyin.</p>
      } @else if (info()!.status !== 'Pending') {
        <div class="ds-auth__heading"><h1 class="ds-auth__title">{{ info()!.status === 'Accepted' ? 'Davet kabul edildi' : 'Davet geçerli değil' }}</h1></div>
        @if (info()!.status === 'Accepted') { <p>Bu davet kabul edildi. <a routerLink="/giris">Giriş yapıp</a> panele geçin.</p> }
        @else { <p class="ds-alert" role="alert">{{ info()!.status === 'Expired' ? 'Davetin süresi doldu.' : 'Davet geri alındı.' }} Kurumunuzdan daveti yeniden göndermesini isteyin.</p> }
      } @else {
        <div class="ds-auth__heading"><h1 class="ds-auth__title">{{ info()!.workspaceName }} sizi davet etti</h1>
          <p class="ds-auth__lead">Katıldığınızda yalnız size verilen dersleri, takvimi ve yoklamayı görürsünüz. Davet {{ info()!.email }} adresine gönderildi; {{ date(info()!.expiresAt) }} tarihine kadar geçerli.</p></div>
        @if (session.signedIn()) {
          @if (sameEmail()) {
            <form class="ds-auth__form" (ngSubmit)="accept()">
              <label class="ds-field"><span class="ds-field__label">Öğrencilerin ve kurumun göreceği adınız</span>
                <input class="ds-input" name="displayName" [(ngModel)]="displayName" maxlength="200" autocomplete="name"></label>
              @if (error()) { <p class="ds-alert" role="alert">{{ error() }}</p> }
              <button class="ds-btn ds-btn--primary ds-btn--large ds-btn--block" type="submit" [disabled]="busy()">Daveti kabul et</button>
            </form>
          } @else {
            <p class="ds-alert ds-alert--warning" role="alert">Şu an {{ session.email() }} ile girdiniz. Davet {{ info()!.email }} adresine gönderildi; çıkış yapıp o adresle girin.</p>
            <button type="button" class="ds-btn ds-btn--secondary ds-btn--block" (click)="signOut()">Çıkış yap</button>
          }
        } @else {
          <a class="ds-btn ds-btn--primary ds-btn--large ds-btn--block" routerLink="/kayit" [queryParams]="{ davet: kod() }">Hesap açıp katıl</a>
          <a class="ds-btn ds-btn--secondary ds-btn--block" routerLink="/giris" [queryParams]="{ donus: '/davet/' + kod() }">maxkampüs hesabımla giriş yap</a>
          <p class="ds-field__hint">maxkoç ya da Master Hızlı Okuma hesabınız varsa "Hesap açıp katıl" deyip o hesabın e-posta ve şifresini yazın.</p>
        }
      }
    </mk-auth-layout>
  `
})
export class InviteComponent implements OnInit {
  private readonly api = inject(MaxkampusApi);
  private readonly router = inject(Router);
  private readonly seo = inject(Seo);
  private readonly signup = inject(GoogleSignup);
  readonly session = inject(MaxkampusSession);
  readonly kod = input.required<string>();
  readonly info = signal<TeacherInvitationInfo | null>(null);
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly date = trDate;
  displayName = '';

  sameEmail(): boolean { return (this.session.email() ?? '').toLowerCase() === this.info()?.email.toLowerCase(); }

  async ngOnInit(): Promise<void> {
    this.seo.set({ title: 'Öğretmen daveti', path: '/davet', noindex: true });
    this.signup.rememberInvite(this.kod());
    await this.session.ensure().catch(() => false);
    this.info.set(await this.api.invitation(this.kod()).catch(() => null));
    this.displayName = this.session.displayName();
    this.loading.set(false);
  }

  async accept(): Promise<void> {
    if (!this.displayName.trim()) { this.error.set('Adınızı yazın.'); return; }
    this.busy.set(true); this.error.set('');
    try {
      await this.api.acceptInvitation(this.kod(), this.displayName.trim());
      this.signup.rememberInvite(null);
      await this.router.navigateByUrl('/panel');
    } catch (error) {
      this.error.set(problemCode(error) === 'Invitation.OtherEmail'
        ? 'Davet başka bir e-posta adresine gönderildi. O adresle giriş yapın.'
        : problemText(error, 'Davet kabul edilemedi. Sayfayı yenileyip yeniden deneyin.'));
    } finally { this.busy.set(false); }
  }

  async signOut(): Promise<void> { await this.session.signOut(); }
}
