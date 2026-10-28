import { HttpClient } from '@angular/common/http';
import { Component, Input, OnInit, computed, signal } from '@angular/core';
import { Observable, finalize, map } from 'rxjs';

export interface NotificationPreference {
  category: string;
  label: string;
  inApp: boolean;
  email: boolean;
}

/** Roadmap O8: the signed-in user's notification choices per category. */
export class NotificationPreferencesApi {
  constructor(private readonly http: HttpClient, private readonly apiUrl: string) {}
  private get url() { return `${this.apiUrl}/notifications/preferences`; }
  list(): Observable<NotificationPreference[]> {
    return this.http.get<{ data: NotificationPreference[] }>(this.url).pipe(map(r => r.data));
  }
  save(preferences: Pick<NotificationPreference, 'category' | 'inApp' | 'email'>[]): Observable<NotificationPreference[]> {
    return this.http.put<{ data: NotificationPreference[] }>(this.url, { preferences }).pipe(map(r => r.data));
  }
}

@Component({
  selector: 'edu-notification-preferences', standalone: true,
  styles: [`
    :host{display:block;border:1px solid var(--ui-border,#e2e8f0);border-radius:.75rem;padding:1rem;margin:1rem 0}
    h2{margin:0 0 .35rem;font-size:1rem;font-weight:600}
    p{margin:.3rem 0;font-size:.86rem;line-height:1.5}
    .muted{color:var(--ui-text-muted,#64748b);font-size:.8rem}
    .err{color:var(--ui-danger,#b91c1c)} .ok{color:var(--ui-success,#047857)}
    ul{list-style:none;margin:.6rem 0 0;padding:0;display:grid}
    li{display:grid;grid-template-columns:minmax(0,1fr) auto auto;gap:.75rem;align-items:center;border-top:1px solid var(--ui-border,#e2e8f0);padding:.55rem 0;font-size:.86rem}
    label{display:inline-flex;gap:.35rem;align-items:center;white-space:nowrap}
    .actions{display:flex;flex-wrap:wrap;gap:.45rem;margin-top:.6rem}
    button{min-height:2.2rem;border:1px solid var(--ui-border,#cbd5e1);border-radius:.5rem;background:transparent;color:inherit;padding:.3rem .75rem;font:inherit;font-size:.82rem;font-weight:600;cursor:pointer}
    button:disabled{opacity:.55;cursor:not-allowed}
    @media (max-width:520px){li{grid-template-columns:1fr 1fr}li span.name{grid-column:1/-1}}
  `],
  template: `
    <h2>Bildirim tercihleri</h2>
    <p class="muted">{{ formal
      ? 'Hangi bildirimleri uygulamada ve e-postayla almak istediğinizi seçin. Önemli koçluk bildirimleri varsayılan olarak e-postayla da gönderilir.'
      : 'Hangi bildirimleri uygulamada ve e-postayla almak istediğini seç. Önemli koçluk bildirimleri varsayılan olarak e-postayla da gelir.' }}</p>
    @if (error()) { <p class="err" role="alert">{{ error() }}</p> }
    @if (message()) { <p class="ok" role="status">{{ message() }}</p> }
    @if (loading()) { <p class="muted" role="status">Tercihler yükleniyor…</p> }
    @else if (items().length) {
      <ul>
        @for (item of items(); track item.category) {
          <li [attr.data-testid]="'pref-' + item.category">
            <span class="name">{{ item.label }}</span>
            <label><input type="checkbox" [checked]="item.inApp" [disabled]="saving()" (change)="toggle(item.category, 'inApp', $event)" [attr.data-testid]="'pref-app-' + item.category">Uygulamada</label>
            <label><input type="checkbox" [checked]="item.email" [disabled]="saving()" (change)="toggle(item.category, 'email', $event)" [attr.data-testid]="'pref-email-' + item.category">E-posta</label>
          </li>
        }
      </ul>
      <div class="actions">
        <button type="button" data-testid="pref-save" [disabled]="!dirty() || saving()" (click)="save()">{{ saving() ? 'Kaydediliyor…' : 'Tercihleri kaydet' }}</button>
        @if (dirty()) { <button type="button" [disabled]="saving()" (click)="reset()">Vazgeç</button> }
      </div>
    } @else if (!error()) { <p class="muted">Tercih bulunamadı.</p> }
  `
})
export class NotificationPreferencesComponent implements OnInit {
  @Input({ required: true }) api!: NotificationPreferencesApi;
  @Input() formal = false;

  readonly items = signal<NotificationPreference[]>([]);
  private readonly saved = signal<NotificationPreference[]>([]);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly message = signal('');
  readonly dirty = computed(() => JSON.stringify(this.items()) !== JSON.stringify(this.saved()));

  ngOnInit(): void {
    this.api.list().pipe(finalize(() => this.loading.set(false))).subscribe({
      next: items => { this.items.set(items); this.saved.set(items); },
      error: () => this.error.set('Bildirim tercihleri yüklenemedi.')
    });
  }

  toggle(category: string, channel: 'inApp' | 'email', event: Event): void {
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
      return !old || old.inApp !== item.inApp || old.email !== item.email;
    }).map(({ category, inApp, email }) => ({ category, inApp, email }));
    this.saving.set(true); this.error.set(''); this.message.set('');
    this.api.save(changed).pipe(finalize(() => this.saving.set(false))).subscribe({
      next: items => { this.items.set(items); this.saved.set(items); this.message.set('Bildirim tercihlerin kaydedildi.'); },
      error: () => this.error.set('Tercihler kaydedilemedi. Seçimlerin korunuyor; yeniden deneyebilirsin.')
    });
  }
}
