import { Component, OnInit, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MaxkampusApi } from '../core/api';
import { problemText } from '../core/problem';
import { Seo } from '../core/seo';

/**
 * /bulten/onay and /bulten/ayril: the links in newsletter e-mails. The visitor clicks the button here, so a mail
 * scanner that opens links does not subscribe or unsubscribe anyone.
 */
@Component({
  selector: 'mk-newsletter-result',
  standalone: true,
  imports: [RouterLink],
  template: `
    <section class="mk-section"><div class="mk-narrow">
      <h1>{{ leaving() ? 'Bültenden ayrılın' : 'Aboneliğinizi onaylayın' }}</h1>
      @if (done()) {
        <p class="mk-success" role="status">{{ leaving() ? 'Bültenden ayrıldınız; size bülten gönderilmeyecek.' : 'Aboneliğiniz onaylandı. Yeni yazılar ve özellikler e-postanıza gelecek.' }}</p>
        <p><a routerLink="/">Ana sayfaya dönün</a></p>
      } @else if (!t()) {
        <p class="ds-alert" role="alert">Bağlantı eksik. E-postadaki bağlantıyı yeniden açın.</p>
      } @else {
        <p>{{ leaving() ? 'maxkampüs bülteninden ayrılmak için aşağıdaki düğmeye tıklayın.' : 'maxkampüs bültenine abone olmak için aşağıdaki düğmeye tıklayın.' }}</p>
        @if (error()) { <p class="ds-alert" role="alert">{{ error() }}</p> }
        <button type="button" class="ds-btn ds-btn--primary ds-btn--large" (click)="run()" [disabled]="busy()">
          {{ busy() ? 'Bekleyin' : leaving() ? 'Bültenden ayrıl' : 'Aboneliği onayla' }}</button>
      }
    </div></section>
  `,
  styles: `h1 { margin: 0 0 16px; font-size: 32px; }`
})
export class NewsletterResultComponent implements OnInit {
  private readonly api = inject(MaxkampusApi);
  private readonly seo = inject(Seo);
  readonly t = input<string | undefined>();
  readonly mode = input<'onay' | 'ayril'>('onay');
  readonly busy = signal(false);
  readonly done = signal(false);
  readonly error = signal('');
  leaving(): boolean { return this.mode() === 'ayril'; }

  ngOnInit(): void { this.seo.set({ title: this.leaving() ? 'Bültenden ayrılın' : 'Bülten onayı', path: `/bulten/${this.mode()}`, noindex: true }); }

  async run(): Promise<void> {
    const token = this.t();
    if (!token) return;
    this.busy.set(true);
    this.error.set('');
    try {
      await (this.leaving() ? this.api.unsubscribe(token) : this.api.confirmSubscription(token));
      this.done.set(true);
    } catch (error) {
      this.error.set(problemText(error, 'İşlem yapılamadı. Biraz sonra yeniden deneyin.'));
    } finally {
      this.busy.set(false);
    }
  }
}
