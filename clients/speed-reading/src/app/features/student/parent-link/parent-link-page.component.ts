import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ToasterService } from '../../../core/services/toaster.service';
import { SpButtonComponent, SpCardComponent, SpEmptyStateComponent } from '../ui';
import { ParentLink, ParentLinkCode, ParentLinkService, productVisible } from './parent-link.service';

const RELATIONSHIPS: Record<string, string> = { Mother: 'Anne', Father: 'Baba', Guardian: 'Vasi', Other: 'Yakın' };

/**
 * V2 (parent panel, decision 01.11.2026): the student links a parent with a one-time code, approves the parent's request,
 * removes the link and decides whether the parent sees the speed-reading progress (one link, each product on/off).
 */
@Component({
  selector: 'app-parent-link-page',
  standalone: true,
  imports: [SpCardComponent, SpButtonComponent, SpEmptyStateComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="plp">
      <header class="plp__header">
        <h1 class="plp__title">Veli bağlantısı</h1>
        <p class="plp__lead">Velin, senin onayınla hızlı okuma ilerlemeni takip edebilir. Bağlantıyı ve neyi göreceğini sen yönetirsin.</p>
      </header>

      <p class="plp__live" role="status" aria-live="polite">{{ status() }}</p>
      @if (error()) {
        <p class="plp__error" role="alert">{{ error() }}</p>
      }

      <sp-card>
        <section aria-labelledby="plp-sees">
          <h2 id="plp-sees" class="plp__h2">Velin neleri görür?</h2>
          <ul class="plp__list">
            <li>Haftalık çalışma süreni ve çalıştığın günleri</li>
            <li>Dakikada kaç kelime okuduğunu (okuma hızını) ve anlama oranını, zaman içindeki değişimiyle</li>
            <li>Programındaki ilerlemeni ve son egzersiz sonuçlarını</li>
            <li>Seviyeni, XP'ni, serini ve rozetlerini</li>
          </ul>
          <p class="plp__note">Velin hiçbir şeyi değiştiremez. Hızlı okuma görünürlüğünü kapatabilir ya da bağlantıyı istediğin zaman kaldırabilirsin.</p>
        </section>
      </sp-card>

      <sp-card>
        <section aria-labelledby="plp-code">
          <h2 id="plp-code" class="plp__h2">Bağlantı kodu</h2>
          <p class="plp__text">Kodu velinle paylaş. Velin kodu veli panelinde girer; gelen isteği burada onaylarsın.</p>
          @if (code(); as issued) {
            <div class="plp__code">
              <span class="plp__code-label">Kodun</span>
              <strong class="plp__code-value" data-testid="parent-code">{{ issued.code }}</strong>
              <span class="plp__code-expiry" data-testid="parent-code-expiry">{{ dateTime(issued.expiresAt) }} tarihine kadar geçerli, bir kez kullanılabilir.</span>
            </div>
          }
          <sp-button data-testid="create-code" [variant]="code() ? 'secondary' : 'primary'" icon="key" [disabled]="busy()"
            (clicked)="createCode()">{{ code() ? 'Yeni kod oluştur' : 'Veli kodu oluştur' }}</sp-button>
        </section>
      </sp-card>

      <sp-card>
        <section aria-labelledby="plp-links">
          <h2 id="plp-links" class="plp__h2">Bağlantıların</h2>
          @if (loaded() && links().length === 0) {
            <div data-testid="parent-links-empty">
              <sp-empty-state icon="family_restroom" title="Henüz veli bağlantın yok"
                description="Bir kod oluşturup velinle paylaştığında isteği burada görürsün." />
            </div>
          }
          <ul class="plp__links">
            @for (link of links(); track link.id) {
              <li class="plp__link">
                <div class="plp__who">
                  <strong>{{ link.parentName }}</strong>
                  <span class="plp__meta">{{ relationship(link.relationship) }} ·
                    {{ link.status === 'Pending' ? 'Onayını bekliyor' : date(link.verifiedAt) + ' tarihinden beri bağlı' }}</span>
                </div>
                @if (link.status === 'Verified') {
                  <div class="plp__switch">
                    <input type="checkbox" role="switch" class="plp__toggle" data-testid="speed-reading-visibility"
                      [id]="'plp-sr-' + link.id" [checked]="speedReadingVisible(link)" [disabled]="busy()"
                      [attr.aria-describedby]="'plp-sr-hint-' + link.id" (change)="toggle(link, $event)" />
                    <label [for]="'plp-sr-' + link.id">Hızlı okuma ilerlememi görsün</label>
                    <span class="plp__hint" [id]="'plp-sr-hint-' + link.id">{{ speedReadingVisible(link) ? 'Açık' : 'Kapalı' }}</span>
                  </div>
                }
                <div class="plp__actions">
                  @if (link.status === 'Pending') {
                    <sp-button data-testid="approve-link" size="sm" [disabled]="busy()" (clicked)="approve(link)">Onayla</sp-button>
                  }
                  <sp-button data-testid="revoke-link" size="sm" variant="secondary" [disabled]="busy()" (clicked)="revoke(link)">
                    {{ link.status === 'Pending' ? 'Reddet' : 'Bağlantıyı kaldır' }}</sp-button>
                </div>
              </li>
            }
          </ul>
        </section>
      </sp-card>
    </div>
  `,
  styles: [`
    .plp { max-width: 760px; margin: 0 auto; padding: 24px 16px 48px; display: grid; gap: 16px; color: var(--sp-text-1); }
    .plp__title { font-size: 1.5rem; font-weight: 700; margin: 0; }
    .plp__lead, .plp__text, .plp__note { color: var(--sp-text-2); margin: 6px 0 0; line-height: 1.5; }
    .plp__note { font-size: .875rem; margin-top: 12px; }
    .plp__h2 { font-size: 1.0625rem; font-weight: 600; margin: 0 0 8px; }
    .plp__list { margin: 0; padding-left: 20px; display: grid; gap: 4px; color: var(--sp-text-2); }
    .plp__live:empty { display: none; }
    .plp__live { margin: 0; padding: 10px 14px; border-radius: var(--sp-radius-md); background: var(--sp-success-light); color: var(--sp-success-text); }
    .plp__error { margin: 0; padding: 10px 14px; border-radius: var(--sp-radius-md); background: var(--sp-danger-light); color: var(--sp-danger); }
    .plp__code { display: grid; gap: 4px; margin: 12px 0; padding: 14px 16px; border-radius: var(--sp-radius-md); background: var(--sp-primary-ghost); }
    .plp__code-label, .plp__code-expiry { font-size: .875rem; color: var(--sp-text-2); }
    .plp__code-value { font-family: var(--sp-font-mono); font-size: 1.5rem; letter-spacing: .15em; color: var(--sp-text-1); }
    .plp__text + sp-button, .plp__code + sp-button { margin-top: 12px; }
    .plp__links { list-style: none; margin: 0; padding: 0; }
    .plp__link { display: grid; gap: 10px; padding: 14px 0; border-top: 1px solid var(--sp-border); }
    .plp__link:first-child { border-top: 0; }
    .plp__who { display: grid; gap: 2px; }
    .plp__meta, .plp__hint { font-size: .875rem; color: var(--sp-text-3); }
    .plp__switch { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; }
    .plp__switch label { font-weight: 500; cursor: pointer; }
    .plp__toggle { appearance: none; width: 44px; height: 24px; border-radius: 999px; background: var(--sp-surface-4); border: 1px solid var(--sp-border-strong);
      position: relative; cursor: pointer; flex: none; transition: background var(--sp-transition-fast); margin: 0; }
    .plp__toggle::after { content: ''; position: absolute; top: 2px; left: 2px; width: 18px; height: 18px; border-radius: 50%;
      background: var(--sp-surface); box-shadow: var(--sp-shadow-xs); transition: transform var(--sp-transition-fast); }
    .plp__toggle:checked { background: var(--sp-primary); border-color: var(--sp-primary); }
    .plp__toggle:checked::after { transform: translateX(20px); }
    .plp__toggle:focus-visible { outline: 2px solid var(--sp-border-focus); outline-offset: 2px; }
    .plp__toggle:disabled { opacity: .6; cursor: progress; }
    .plp__actions { display: flex; gap: 8px; flex-wrap: wrap; }
    @media (min-width: 640px) {
      .plp__link { grid-template-columns: 1fr auto; align-items: center; }
      .plp__switch { grid-column: 1 / -1; }
      .plp__actions { grid-column: 2; grid-row: 1; }
    }
  `]
})
export class ParentLinkPageComponent implements OnInit {
  private readonly service = inject(ParentLinkService);
  private readonly toaster = inject(ToasterService);

  readonly links = signal<ParentLink[]>([]);
  readonly loaded = signal(false);
  readonly code = signal<ParentLinkCode | null>(null);
  readonly busy = signal(false);
  readonly status = signal('');
  readonly error = signal<string | null>(null);

  ngOnInit(): void {
    void this.load();
  }

  relationship(value: string): string { return RELATIONSHIPS[value] ?? 'Veli'; }

  speedReadingVisible(link: ParentLink): boolean { return productVisible(link, 'SpeedReading'); }

  date(value: string | null): string {
    return value ? new Intl.DateTimeFormat('tr-TR', { day: 'numeric', month: 'long', year: 'numeric' }).format(new Date(value)) : '';
  }

  dateTime(value: string): string {
    return new Intl.DateTimeFormat('tr-TR', { day: 'numeric', month: 'long', year: 'numeric', hour: '2-digit', minute: '2-digit' })
      .format(new Date(value));
  }

  async createCode(): Promise<void> {
    await this.run(async () => {
      const issued = await firstValueFrom(this.service.createCode());
      this.code.set(issued);
      this.status.set(`Kod oluşturuldu: ${issued.code}. Velinle paylaşabilirsin.`);
    }, 'Veli kodu oluşturulamadı.');
  }

  async approve(link: ParentLink): Promise<void> {
    if (!await this.toaster.confirm(`${link.parentName} hızlı okuma ilerlemeni görebilsin mi? Bu bağlantıyı istediğin zaman kaldırabilirsin.`,
      { title: 'Bağlantıyı onayla', confirmText: 'Onayla', cancelText: 'Vazgeç' })) return;
    await this.run(async () => {
      await firstValueFrom(this.service.approve(link.id));
      this.status.set(`${link.parentName} ile bağlantı onaylandı.`);
      await this.load();
    }, 'Bağlantı onaylanamadı.');
  }

  async revoke(link: ParentLink): Promise<void> {
    const pending = link.status === 'Pending';
    if (!await this.toaster.confirm(pending
      ? `${link.parentName} adlı velinin bağlantı isteği reddedilsin mi?`
      : `${link.parentName} ile veli bağlantısı kaldırılsın mı? Velin artık hiçbir bilgini göremez.`,
      { title: pending ? 'İsteği reddet' : 'Bağlantıyı kaldır', confirmText: pending ? 'Reddet' : 'Kaldır', cancelText: 'Vazgeç' })) return;
    await this.run(async () => {
      await firstValueFrom(this.service.revoke(link.id));
      this.status.set(pending ? `${link.parentName} adlı velinin isteği reddedildi.` : `${link.parentName} ile bağlantı kaldırıldı.`);
      await this.load();
    }, pending ? 'İstek reddedilemedi.' : 'Bağlantı kaldırılamadı.');
  }

  async toggle(link: ParentLink, event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const previous = this.speedReadingVisible(link);
    const next = input.checked;
    const ok = await this.run(async () => {
      await firstValueFrom(this.service.setVisibility(link.id, { coaching: productVisible(link, 'Coaching'), speedReading: next }));
      this.links.update(items => items.map(item => item.id !== link.id ? item : { ...item, products: withSpeedReading(item, next) }));
      this.status.set(next
        ? `${link.parentName} artık hızlı okuma ilerlemeni görebilir.`
        : `${link.parentName} artık hızlı okuma ilerlemeni görmeyecek.`);
    }, 'Görünürlük ayarı kaydedilemedi.');
    if (!ok) input.checked = previous;
  }

  private async load(): Promise<void> {
    try {
      this.links.set(await firstValueFrom(this.service.getLinks()));
    } catch {
      this.error.set('Veli bağlantıların yüklenemedi. Sayfayı yenileyip tekrar dene.');
    } finally {
      this.loaded.set(true);
    }
  }

  private async run(action: () => Promise<void>, failure: string): Promise<boolean> {
    if (this.busy()) return false;
    this.busy.set(true);
    this.error.set(null);
    try {
      await action();
      return true;
    } catch (response) {
      this.status.set('');
      this.error.set(apiMessage(response, failure));
      return false;
    } finally {
      this.busy.set(false);
    }
  }
}

function withSpeedReading(link: ParentLink, visible: boolean): ParentLink['products'] {
  const products = link.products?.length ? link.products : [
    { product: 'Coaching' as const, studentHasAccess: false, visibleToParent: true },
    { product: 'SpeedReading' as const, studentHasAccess: true, visibleToParent: true }
  ];
  return products.map(item => item.product === 'SpeedReading' ? { ...item, visibleToParent: visible } : item);
}

/** Identity answers `{ error: { code, message } }`; other gateways `{ message }`. */
function apiMessage(response: unknown, fallback: string): string {
  const body = (response as { error?: { error?: { message?: string }; message?: string } } | null)?.error;
  return body?.error?.message || body?.message || fallback;
}
