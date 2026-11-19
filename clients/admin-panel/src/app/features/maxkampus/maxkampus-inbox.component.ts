import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { EduivmeSiteService, SiteContactMessage, SiteSubscriber, SubscriberStatus } from '../../core/services/eduivme-site.service';
import { ToasterService } from '../../core/services/toaster.service';

type InboxView = 'messages' | 'subscribers';
type MessageFilter = 'open' | 'answered' | null;

/**
 * maxkampüs (Kurs Yönetimi) › İletişim mesajları and › Bülten aboneleri. Both hold personal data the visitors gave
 * for one purpose: messages are answered by e-mail and then deleted (by hand or after 180 days), and the newsletter
 * list leaves this screen only as confirmed addresses with each reader's own way out.
 */
@Component({
  selector: 'app-maxkampus-inbox',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: '../eduivme-site/eduivme-site-admin.component.scss',
  template: `
    <main class="site-admin" aria-labelledby="inbox-title">
      <header class="site-admin__head">
        <div>
          <p class="site-admin__eyebrow">maxkampüs (Kurs Yönetimi)</p>
          <h1 id="inbox-title">{{ view === 'messages' ? 'İletişim mesajları' : 'Bülten aboneleri' }}</h1>
          <p class="site-admin__lead">{{ view === 'messages'
            ? 'maxkampus.com iletişim formundan gelen mesajlar. Yanıtladığınız mesajı işaretleyin; yanıtlanan mesajlar 180 gün, yanıtlanmayanlar bir yıl sonra kendiliğinden silinir.'
            : 'Onay e-postasındaki bağlantıya tıklayan adresler bültene kayıtlıdır. Listeyi indirdiğinizde her adresin kendi ayrılma bağlantısı da gelir; bülteni gönderirken bu bağlantıyı e-postaya ekleyin.' }}</p>
        </div>
        @if (view === 'subscribers') {
          <button type="button" class="ds-btn ds-btn--secondary" (click)="download()" [disabled]="busy()">Onaylı adresleri indir (CSV)</button>
        }
      </header>

      <div class="choices" role="group" [attr.aria-label]="view === 'messages' ? 'Mesajları süz' : 'Aboneleri süz'">
        @for (option of filters(); track option.label) {
          <button type="button" class="page-tab" [attr.aria-pressed]="filter() === option.value" (click)="choose(option.value)">{{ option.label }}</button>
        }
      </div>

      @if (loading()) {
        <p class="site-admin__muted" role="status"><span class="ds-spinner" aria-hidden="true"></span> Yükleniyor</p>
      } @else if (loadError()) {
        <div class="ds-alert" role="alert"><p>{{ loadError() }}</p><button type="button" class="ds-btn ds-btn--secondary" (click)="load()">Yeniden dene</button></div>
      } @else if (view === 'messages') {
        <section class="card" aria-label="Mesajlar">
          <p class="site-admin__muted" role="status">{{ total() }} mesaj</p>
          @if (!messages().length) { <p class="site-admin__muted">Bu süzgeçte mesaj yok.</p> }
          <ul>
            @for (item of messages(); track item.id) {
              <li class="row message">
                <span class="row__main">
                  <strong>{{ item.name }}{{ item.organization ? ', ' + item.organization : '' }}</strong>
                  <span class="site-admin__muted"><a [href]="'mailto:' + item.email">{{ item.email }}</a>{{ item.phone ? ', ' + item.phone : '' }}, {{ time(item.createdAt) }}</span>
                  <span class="message__text">{{ item.message }}</span>
                </span>
                <span class="pill pill--{{ item.answeredAt ? 'success' : 'warning' }}">{{ item.answeredAt ? 'Yanıtlandı' : 'Yanıt bekliyor' }}</span>
                <button type="button" class="ds-btn ds-btn--quiet" (click)="answered(item, !item.answeredAt)">{{ item.answeredAt ? 'Yanıtlanmadı yap' : 'Yanıtlandı' }}<span class="ds-visually-hidden"> ({{ item.name }})</span></button>
                <button type="button" class="ds-btn ds-btn--quiet editor__delete" (click)="removeMessage(item)">{{ confirming() === item.id ? 'Silmeyi onayla' : 'Sil' }}<span class="ds-visually-hidden"> ({{ item.name }})</span></button>
              </li>
            }
          </ul>
          @if (total() > messages().length) {
            <button type="button" class="ds-btn ds-btn--secondary" (click)="more()">Daha fazla göster</button>
          }
        </section>
      } @else {
        <section class="card" aria-label="Aboneler">
          <p class="site-admin__muted" role="status">{{ total() }} adres</p>
          @if (!subscribers().length) { <p class="site-admin__muted">Bu süzgeçte adres yok.</p> }
          <ul>
            @for (item of subscribers(); track item.id) {
              <li class="row">
                <span class="row__main">
                  <strong>{{ item.email }}</strong>
                  <span class="site-admin__muted">{{ statusLabel(item.status) }}, {{ time(item.confirmedAt ?? item.createdAt) }}{{ item.source ? ', ' + item.source : '' }}</span>
                </span>
                <button type="button" class="ds-btn ds-btn--quiet editor__delete" (click)="removeSubscriber(item)">{{ confirming() === item.id ? 'Silmeyi onayla' : 'Sil' }}<span class="ds-visually-hidden"> ({{ item.email }})</span></button>
              </li>
            }
          </ul>
          @if (total() > subscribers().length) {
            <button type="button" class="ds-btn ds-btn--secondary" (click)="more()">Daha fazla göster</button>
          }
        </section>
      }
    </main>
  `,
  styles: [`
    .message { align-items: flex-start; flex-wrap: wrap; }
    .message .row__main > span.message__text { white-space: pre-wrap; overflow: visible; color: var(--ds-text); line-height: 1.5; }
    .row__main a { color: var(--ds-primary); }
  `]
})
export class MaxkampusInboxComponent implements OnInit {
  private readonly api = inject(EduivmeSiteService);
  private readonly toaster = inject(ToasterService);
  readonly view: InboxView = inject(ActivatedRoute).snapshot.data['inbox'] === 'subscribers' ? 'subscribers' : 'messages';

  readonly messages = signal<SiteContactMessage[]>([]);
  readonly subscribers = signal<SiteSubscriber[]>([]);
  readonly total = signal(0);
  readonly loading = signal(true);
  readonly loadError = signal('');
  readonly busy = signal(false);
  readonly confirming = signal<string | null>(null);
  readonly filter = signal<string | null>(this.view === 'messages' ? 'open' : 'Confirmed');

  filters(): { value: string | null; label: string }[] {
    return this.view === 'messages'
      ? [{ value: 'open', label: 'Yanıt bekleyen' }, { value: 'answered', label: 'Yanıtlanan' }, { value: null, label: 'Hepsi' }]
      : [{ value: 'Confirmed', label: 'Onaylı' }, { value: 'Pending', label: 'Onay bekleyen' }, { value: 'Unsubscribed', label: 'Ayrılan' }, { value: null, label: 'Hepsi' }];
  }

  ngOnInit(): void { void this.load(); }

  choose(value: string | null): void {
    this.filter.set(value);
    void this.load();
  }

  async load(append = false): Promise<void> {
    if (!append) this.loading.set(true);
    this.loadError.set('');
    this.confirming.set(null);
    try {
      if (this.view === 'messages') {
        const skip = append ? this.messages().length : 0;
        const page = await firstValueFrom(this.api.getMessages(this.filter() as MessageFilter, skip, 50));
        this.messages.set(append ? [...this.messages(), ...page.items] : page.items);
        this.total.set(page.total);
      } else {
        const skip = append ? this.subscribers().length : 0;
        const page = await firstValueFrom(this.api.getSubscribers(this.filter() as SubscriberStatus | null, skip, 100));
        this.subscribers.set(append ? [...this.subscribers(), ...page.items] : page.items);
        this.total.set(page.total);
      }
    } catch {
      this.loadError.set(this.view === 'messages' ? 'Mesajlar yüklenemedi. Yeniden deneyin.' : 'Aboneler yüklenemedi. Yeniden deneyin.');
    } finally {
      this.loading.set(false);
    }
  }

  more(): void { void this.load(true); }

  async answered(item: SiteContactMessage, answered: boolean): Promise<void> {
    try {
      await firstValueFrom(this.api.markAnswered(item.id, answered));
      this.toaster.success(answered ? 'Mesaj yanıtlandı olarak işaretlendi.' : 'Mesaj yanıt bekliyor olarak işaretlendi.');
      await this.load();
    } catch (error) {
      this.toaster.error(this.message(error, 'Mesaj güncellenemedi.'));
    }
  }

  async removeMessage(item: SiteContactMessage): Promise<void> {
    if (this.confirming() !== item.id) { this.confirming.set(item.id); return; }
    try {
      await firstValueFrom(this.api.deleteMessage(item.id));
      this.toaster.success('Mesaj silindi.');
      await this.load();
    } catch (error) {
      this.toaster.error(this.message(error, 'Mesaj silinemedi.'));
    }
  }

  async removeSubscriber(item: SiteSubscriber): Promise<void> {
    if (this.confirming() !== item.id) { this.confirming.set(item.id); return; }
    try {
      await firstValueFrom(this.api.deleteSubscriber(item.id));
      this.toaster.success('Adres silindi.');
      await this.load();
    } catch (error) {
      this.toaster.error(this.message(error, 'Adres silinemedi.'));
    }
  }

  async download(): Promise<void> {
    this.busy.set(true);
    try {
      const blob = await firstValueFrom(this.api.exportSubscribers());
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = 'maxkampus-bulten-aboneleri.csv';
      link.click();
      URL.revokeObjectURL(url);
    } catch (error) {
      this.toaster.error(this.message(error, 'Liste indirilemedi.'));
    } finally {
      this.busy.set(false);
    }
  }

  statusLabel(status: SubscriberStatus): string {
    return status === 'Confirmed' ? 'Onaylı' : status === 'Pending' ? 'Onay bekliyor' : 'Ayrıldı';
  }

  time(value: string): string {
    const date = new Date(/[zZ]|[+-]\d\d:?\d\d$/.test(value) ? value : value + 'Z');
    if (Number.isNaN(date.getTime())) return value;
    return new Intl.DateTimeFormat('tr-TR', { day: 'numeric', month: 'long', year: 'numeric', hour: '2-digit', minute: '2-digit', timeZone: 'Europe/Istanbul' })
      .format(date).replace(',', '');
  }

  private message(error: unknown, fallback: string): string {
    if (error instanceof HttpErrorResponse && typeof error.error?.detail === 'string' && error.error.detail) return error.error.detail;
    return fallback;
  }
}
