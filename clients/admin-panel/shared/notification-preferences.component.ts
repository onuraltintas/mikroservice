import { HttpClient } from '@angular/common/http';
import { Component, Input, OnInit, computed, signal } from '@angular/core';
import { Observable, finalize, map } from 'rxjs';

export interface NotificationPreference {
  category: string;
  label: string;
  inApp: boolean;
  email: boolean;
  /** Roadmap O9: false for billing notices that are always e-mailed. */
  emailEditable?: boolean;
  /** Roadmap O9: whether SMS / WhatsApp may be chosen for this category. */
  textAvailable?: boolean;
  sms?: boolean;
  whatsApp?: boolean;
}

export interface CommercialConsent {
  channel: 'Sms' | 'Email' | 'Call' | string;
  label: string;
  granted: boolean;
  iysStatus: 'NotRecorded' | 'Pending' | 'Synced' | string;
  changedAt: string | null;
}

type Channel = 'inApp' | 'email' | 'sms' | 'whatsApp';

/** Roadmap O8 / O9: the signed-in user's notification choices per category and commercial-message consent. */
export class NotificationPreferencesApi {
  constructor(private readonly http: HttpClient, private readonly apiUrl: string) {}
  private get url() { return `${this.apiUrl}/notifications/preferences`; }
  private get consentUrl() { return `${this.apiUrl}/notifications/commercial-consents`; }
  list(): Observable<NotificationPreference[]> {
    return this.http.get<{ data: NotificationPreference[] }>(this.url).pipe(map(r => r.data));
  }
  save(preferences: Pick<NotificationPreference, 'category' | 'inApp' | 'email' | 'sms' | 'whatsApp'>[]): Observable<NotificationPreference[]> {
    return this.http.put<{ data: NotificationPreference[] }>(this.url, { preferences }).pipe(map(r => r.data));
  }
  consents(): Observable<CommercialConsent[]> {
    return this.http.get<{ data: CommercialConsent[] }>(this.consentUrl).pipe(map(r => r.data));
  }
  saveConsents(consents: Pick<CommercialConsent, 'channel' | 'granted'>[]): Observable<CommercialConsent[]> {
    return this.http.put<{ data: CommercialConsent[] }>(this.consentUrl, { consents }).pipe(map(r => r.data));
  }
}

@Component({
  selector: 'edu-notification-preferences', standalone: true,
  styles: [`
    :host{display:block;border:1px solid var(--ui-border,#e2e8f0);border-radius:.75rem;padding:1rem;margin:1rem 0}
    h2{margin:0 0 .35rem;font-size:1rem;font-weight:600}
    h3{margin:1rem 0 .25rem;font-size:.92rem;font-weight:600}
    p{margin:.3rem 0;font-size:.86rem;line-height:1.5}
    .muted{color:var(--ui-text-muted,#64748b);font-size:.8rem}
    .err{color:var(--ui-danger,#b91c1c)} .ok{color:var(--ui-success,#047857)}
    ul{list-style:none;margin:.6rem 0 0;padding:0;display:grid}
    li{display:flex;flex-wrap:wrap;gap:.4rem .9rem;align-items:center;border-top:1px solid var(--ui-border,#e2e8f0);padding:.55rem 0;font-size:.86rem}
    li .name{flex:1 1 14rem;min-width:0}
    label{display:inline-flex;gap:.35rem;align-items:center;white-space:nowrap}
    .tag{border-radius:999px;padding:.05rem .5rem;font-size:.72rem;font-weight:700;background:var(--ui-surface-muted,#f1f5f9)}
    .actions{display:flex;flex-wrap:wrap;gap:.45rem;margin-top:.6rem}
    button{min-height:2.2rem;border:1px solid var(--ui-border,#cbd5e1);border-radius:.5rem;background:transparent;color:inherit;padding:.3rem .75rem;font:inherit;font-size:.82rem;font-weight:600;cursor:pointer}
    button:disabled{opacity:.55;cursor:not-allowed}
  `],
  template: `
    <h2>Bildirim tercihleri</h2>
    <p class="muted">{{ formal
      ? 'Hangi bildirimleri uygulamada ve e-postayla almak istediğinizi seçin. Önemli koçluk bildirimleri varsayılan olarak e-postayla da gönderilir.'
      : 'Hangi bildirimleri uygulamada ve e-postayla almak istediğini seç. Önemli koçluk bildirimleri varsayılan olarak e-postayla da gelir.' }}</p>
    @if (anyText()) { <p class="muted" data-testid="text-note">SMS ve WhatsApp gönderimi henüz açılmadı. {{ formal ? 'Seçiminiz kaydedilir; gönderim açıldığında onaylı telefon numaranıza yalnız seçtiğiniz hatırlatmalar gider.' : 'Seçimin kaydedilir; gönderim açıldığında onaylı telefon numarana yalnız seçtiğin hatırlatmalar gider.' }}</p> }
    @if (error()) { <p class="err" role="alert">{{ error() }}</p> }
    @if (message()) { <p class="ok" role="status">{{ message() }}</p> }
    @if (loading()) { <p class="muted" role="status">Tercihler yükleniyor…</p> }
    @else if (items().length) {
      <ul>
        @for (item of items(); track item.category) {
          <li [attr.data-testid]="'pref-' + item.category">
            <span class="name">{{ item.label }}</span>
            <label><input type="checkbox" [checked]="item.inApp" [disabled]="saving()" (change)="toggle(item.category, 'inApp', $event)" [attr.data-testid]="'pref-app-' + item.category">Uygulamada</label>
            <label><input type="checkbox" [checked]="item.email" [disabled]="saving() || item.emailEditable === false" (change)="toggle(item.category, 'email', $event)" [attr.data-testid]="'pref-email-' + item.category">E-posta</label>
            @if (item.textAvailable) {
              <label><input type="checkbox" [checked]="!!item.sms" [disabled]="saving()" (change)="toggle(item.category, 'sms', $event)" [attr.data-testid]="'pref-sms-' + item.category">SMS</label>
              <label><input type="checkbox" [checked]="!!item.whatsApp" [disabled]="saving()" (change)="toggle(item.category, 'whatsApp', $event)" [attr.data-testid]="'pref-whatsapp-' + item.category">WhatsApp</label>
            }
          </li>
        }
      </ul>
      <div class="actions">
        <button type="button" data-testid="pref-save" [disabled]="!dirty() || saving()" (click)="save()">{{ saving() ? 'Kaydediliyor…' : 'Tercihleri kaydet' }}</button>
        @if (dirty()) { <button type="button" [disabled]="saving()" (click)="reset()">Vazgeç</button> }
      </div>
    } @else if (!error()) { <p class="muted">Tercih bulunamadı.</p> }

    @if (consents().length) {
      <h3>Kampanya ve duyuru izinleri</h3>
      <p class="muted">Bunlar ticari elektronik ileti izinleridir ve İleti Yönetim Sistemi'ne (İYS) kaydedilir. Seans hatırlatması gibi bilgilendirme iletileri bu izne bağlı değildir. İYS bağlantısı açılana kadar seçimin burada saklanır.</p>
      @if (consentError()) { <p class="err" role="alert">{{ consentError() }}</p> }
      <ul>
        @for (consent of consents(); track consent.channel) {
          <li [attr.data-testid]="'consent-' + consent.channel">
            <label class="name"><input type="checkbox" [checked]="consent.granted" [disabled]="savingConsent()" (change)="changeConsent(consent, $event)" [attr.data-testid]="'consent-box-' + consent.channel">{{ consent.label }}</label>
            @if (consent.iysStatus === 'Pending') { <span class="tag">İYS'ye aktarılmayı bekliyor</span> }
            @if (consent.iysStatus === 'Synced') { <span class="tag">İYS'ye kaydedildi</span> }
          </li>
        }
      </ul>
    }
  `
})
export class NotificationPreferencesComponent implements OnInit {
  @Input({ required: true }) api!: NotificationPreferencesApi;
  @Input() formal = false;

  readonly items = signal<NotificationPreference[]>([]);
  private readonly saved = signal<NotificationPreference[]>([]);
  readonly consents = signal<CommercialConsent[]>([]);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly savingConsent = signal(false);
  readonly error = signal('');
  readonly consentError = signal('');
  readonly message = signal('');
  readonly dirty = computed(() => JSON.stringify(this.items()) !== JSON.stringify(this.saved()));
  readonly anyText = computed(() => this.items().some(item => item.textAvailable));

  ngOnInit(): void {
    this.api.list().pipe(finalize(() => this.loading.set(false))).subscribe({
      next: items => { this.items.set(items); this.saved.set(items); },
      error: () => this.error.set('Bildirim tercihleri yüklenemedi.')
    });
    this.api.consents?.().subscribe({ next: consents => this.consents.set(consents), error: () => this.consents.set([]) });
  }

  toggle(category: string, channel: Channel, event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    this.message.set('');
    this.items.update(items => items.map(item => item.category === category ? { ...item, [channel]: checked } : item));
  }

  reset(): void { this.items.set(this.saved()); this.message.set(''); }

  save(): void {
    if (!this.dirty() || this.saving()) return;
    const before = new Map(this.saved().map(item => [item.category, item]));
    const changed = this.items().filter(item => {
      const old = before.get(item.category);
      return !old || old.inApp !== item.inApp || old.email !== item.email || !!old.sms !== !!item.sms || !!old.whatsApp !== !!item.whatsApp;
    }).map(({ category, inApp, email, sms, whatsApp }) => ({ category, inApp, email, sms: !!sms, whatsApp: !!whatsApp }));
    this.saving.set(true); this.error.set(''); this.message.set('');
    this.api.save(changed).pipe(finalize(() => this.saving.set(false))).subscribe({
      next: items => { this.items.set(items); this.saved.set(items); this.message.set('Bildirim tercihlerin kaydedildi.'); },
      error: () => this.error.set('Tercihler kaydedilemedi. Seçimlerin korunuyor; yeniden deneyebilirsin.')
    });
  }

  /** Each consent box is saved on its own, so the record shows when that permission changed. */
  changeConsent(consent: CommercialConsent, event: Event): void {
    const input = event.target as HTMLInputElement; const granted = input.checked;
    this.savingConsent.set(true); this.consentError.set('');
    this.api.saveConsents([{ channel: consent.channel, granted }]).pipe(finalize(() => this.savingConsent.set(false))).subscribe({
      next: consents => this.consents.set(consents),
      error: () => { input.checked = !granted; this.consentError.set('İzin kaydedilemedi. Lütfen tekrar dene.'); }
    });
  }
}
