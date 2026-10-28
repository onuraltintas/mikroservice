import { DatePipe, DOCUMENT } from '@angular/common';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Component, Input, OnInit, inject, signal } from '@angular/core';
import { Observable, finalize } from 'rxjs';

export type CoachingCalendarRole = 'teacher' | 'student';

export interface CoachingCalendarSubscriptionStatus {
  role: string;
  active: boolean;
  createdAt: string | null;
  lastUsedAt: string | null;
}

export interface CoachingCalendarSubscriptionLink {
  role: string;
  path: string;
  createdAt: string;
}

/** Roadmap O6: the personal, revocable iCalendar subscription link of a teacher or a student. */
export class CoachingCalendarSubscriptionApi {
  constructor(private readonly http: HttpClient, private readonly apiUrl: string) {}
  private get url() { return `${this.apiUrl}/calendar/subscription`; }
  private params(role: CoachingCalendarRole) { return { params: new HttpParams().set('role', role) }; }

  status(role: CoachingCalendarRole): Observable<CoachingCalendarSubscriptionStatus> {
    return this.http.get<CoachingCalendarSubscriptionStatus>(this.url, this.params(role));
  }
  create(role: CoachingCalendarRole): Observable<CoachingCalendarSubscriptionLink> {
    return this.http.post<CoachingCalendarSubscriptionLink>(this.url, {}, this.params(role));
  }
  revoke(role: CoachingCalendarRole): Observable<void> {
    return this.http.delete<void>(this.url, this.params(role));
  }
}

export interface CoachingCalendarLinks {
  https: string;
  webcal: string;
  google: string;
}

/** The absolute feed address plus the webcal:// and Google Calendar forms a calendar app subscribes to. */
export function coachingCalendarLinks(path: string, origin: string): CoachingCalendarLinks {
  const https = new URL(path, origin).toString();
  const webcal = https.replace(/^https?:/i, 'webcal:');
  return { https, webcal, google: `https://calendar.google.com/calendar/render?cid=${encodeURIComponent(webcal)}` };
}

@Component({
  selector: 'edu-coaching-calendar-subscription', standalone: true, imports: [DatePipe],
  styles: [`
    :host{display:block;border:1px solid var(--ui-border,#e2e8f0);border-radius:.75rem;padding:1rem;margin:1rem 0}
    h2{margin:0 0 .35rem;font-size:1rem;font-weight:600}
    p{margin:.3rem 0;font-size:.86rem;line-height:1.5}
    .muted{color:var(--ui-text-muted,#64748b);font-size:.8rem}
    .err{color:var(--ui-danger,#b91c1c)}
    .ok{color:var(--ui-success,#047857)}
    .warn{color:var(--ui-warning,#b45309);font-weight:600}
    .actions{display:flex;flex-wrap:wrap;gap:.45rem;margin-top:.6rem;align-items:center}
    button,a.button{min-height:2.2rem;border:1px solid var(--ui-border,#cbd5e1);border-radius:.5rem;background:transparent;color:inherit;padding:.3rem .75rem;font:inherit;font-size:.82rem;font-weight:600;cursor:pointer;text-decoration:none;display:inline-flex;align-items:center}
    button:disabled{opacity:.55;cursor:not-allowed}
    input{width:100%;min-width:0;border:1px solid var(--ui-border,#cbd5e1);border-radius:.45rem;padding:.45rem .55rem;font:inherit;font-size:.8rem;background:transparent;color:inherit}
    .link{display:grid;gap:.4rem;margin-top:.6rem}
  `],
  template: `
    <h2>Takvim aboneliği</h2>
    <p class="muted">{{ formal
      ? 'Seanslarınızı Google Takvim, Apple Takvim veya Outlook\\'a abone olarak ekleyin. Yeni, değişen ve iptal edilen seanslar takvim uygulamanız yenilendiğinde (genellikle birkaç saatte bir) güncellenir.'
      : 'Seanslarını Google Takvim, Apple Takvim veya Outlook\\'a abone olarak ekle. Yeni, değişen ve iptal edilen seanslar takvim uygulaman yenilendiğinde (genellikle birkaç saatte bir) güncellenir.' }}</p>
    @if (error()) { <p class="err" role="alert">{{ error() }}</p> }
    @if (message()) { <p class="ok" role="status">{{ message() }}</p> }
    @if (loading()) { <p class="muted" role="status">Takvim bağlantısı kontrol ediliyor…</p> }
    @else if (status(); as current) {
      @if (links(); as link) {
        <div class="link" data-testid="calendar-subscription-link">
          <label class="muted" for="calendar-feed-url-{{ role }}">Abonelik adresi</label>
          <input id="calendar-feed-url-{{ role }}" type="text" readonly [value]="link.https" (focus)="select($event)">
          <p class="warn">{{ formal
            ? 'Bu adresi paylaşmayın: adrese sahip olan herkes seans saatlerinizi görebilir. Adres yalnız şimdi gösterilir.'
            : 'Bu adresi paylaşma: adrese sahip olan herkes seans saatlerini görebilir. Adres yalnız şimdi gösterilir.' }}</p>
          <div class="actions">
            <button type="button" data-testid="calendar-copy" (click)="copy(link.https)">Adresi kopyala</button>
            <a class="button" data-testid="calendar-google" [href]="link.google" target="_blank" rel="noopener noreferrer">Google Takvim'e ekle</a>
            <a class="button" data-testid="calendar-webcal" [href]="link.webcal">Apple Takvim / Outlook ile aç</a>
          </div>
        </div>
      } @else if (current.active) {
        <p data-testid="calendar-subscription-active">Abonelik bağlantısı {{ current.createdAt | date:'dd.MM.yyyy HH:mm' }} tarihinde oluşturuldu.
          @if (current.lastUsedAt) { Son kullanım: {{ current.lastUsedAt | date:'dd.MM.yyyy HH:mm' }}. } @else { Henüz bir takvim uygulaması kullanmadı. }</p>
        <p class="muted">Güvenlik için adres yalnız oluşturulduğu anda gösterilir. {{ formal ? 'Kaybettiyseniz yenisini oluşturun' : 'Kaybettiysen yenisini oluştur' }}; eski adres çalışmayı bırakır.</p>
      }
      <div class="actions">
        @if (pending() === 'rotate') {
          <span>Eski adres çalışmayı bırakacak. Devam edilsin mi?</span>
          <button type="button" data-testid="calendar-rotate-confirm" [disabled]="busy()" (click)="create()">Evet, yeni adres oluştur</button>
          <button type="button" [disabled]="busy()" (click)="pending.set(null)">Vazgeç</button>
        } @else if (pending() === 'revoke') {
          <span>Takvim uygulamaları seansları artık alamayacak. Kapatılsın mı?</span>
          <button type="button" data-testid="calendar-revoke-confirm" [disabled]="busy()" (click)="revoke()">Evet, kapat</button>
          <button type="button" [disabled]="busy()" (click)="pending.set(null)">Vazgeç</button>
        } @else if (current.active) {
          <button type="button" data-testid="calendar-rotate" [disabled]="busy()" (click)="pending.set('rotate')">Yeni adres oluştur</button>
          <button type="button" data-testid="calendar-revoke" [disabled]="busy()" (click)="pending.set('revoke')">Aboneliği kapat</button>
        } @else {
          <button type="button" data-testid="calendar-create" [disabled]="busy()" (click)="create()">{{ busy() ? 'Oluşturuluyor…' : 'Abonelik adresi oluştur' }}</button>
        }
      </div>
    }
  `
})
export class CoachingCalendarSubscriptionComponent implements OnInit {
  private readonly document = inject(DOCUMENT);
  @Input({ required: true }) api!: CoachingCalendarSubscriptionApi;
  @Input({ required: true }) role!: CoachingCalendarRole;
  @Input() formal = false;
  /** Overrides the page origin, e.g. in tests. */
  @Input() origin: string | null = null;

  readonly status = signal<CoachingCalendarSubscriptionStatus | null>(null);
  readonly links = signal<CoachingCalendarLinks | null>(null);
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly pending = signal<'rotate' | 'revoke' | null>(null);
  readonly error = signal('');
  readonly message = signal('');

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading.set(true); this.error.set('');
    this.api.status(this.role).pipe(finalize(() => this.loading.set(false))).subscribe({
      next: status => this.status.set(status),
      error: () => this.error.set('Takvim aboneliği bilgisi alınamadı.')
    });
  }

  create(): void {
    if (this.busy()) return;
    this.busy.set(true); this.error.set(''); this.message.set('');
    this.api.create(this.role).pipe(finalize(() => this.busy.set(false))).subscribe({
      next: link => {
        const origin = this.origin ?? this.document?.location?.origin ?? '';
        this.links.set(coachingCalendarLinks(link.path, origin));
        this.status.set({ role: link.role, active: true, createdAt: link.createdAt, lastUsedAt: null });
        this.pending.set(null);
        this.message.set('Abonelik adresi oluşturuldu.');
      },
      error: () => this.error.set('Abonelik adresi oluşturulamadı. Lütfen tekrar deneyin.')
    });
  }

  revoke(): void {
    if (this.busy()) return;
    this.busy.set(true); this.error.set(''); this.message.set('');
    this.api.revoke(this.role).pipe(finalize(() => this.busy.set(false))).subscribe({
      next: () => {
        this.links.set(null);
        this.status.update(current => current ? { ...current, active: false, createdAt: null, lastUsedAt: null } : current);
        this.pending.set(null);
        this.message.set('Takvim aboneliği kapatıldı.');
      },
      error: () => this.error.set('Abonelik kapatılamadı. Lütfen tekrar deneyin.')
    });
  }

  async copy(value: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(value);
      this.message.set('Adres kopyalandı.');
    } catch {
      this.message.set('');
      this.error.set('Adres kopyalanamadı; kutudaki adresi seçip kopyalayın.');
    }
  }

  select(event: Event): void {
    (event.target as HTMLInputElement | null)?.select?.();
  }
}
