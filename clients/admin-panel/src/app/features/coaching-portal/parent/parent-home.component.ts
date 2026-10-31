import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ToasterService } from '../../../core/services/toaster.service';
import { CoachingDatePipe } from '../coaching-labels';
import { ParentLink, ParentRelationship, CoachingParentService } from './coaching-parent.service';
import { PRODUCT_LABELS, ParentProduct, coachingStatusLine, firstName, hiddenProductsText, productSlug, speedReadingStatusLine, visibleProducts } from './parent-model';

type Status = { text: string; failed?: boolean };

/**
 * Roadmap Y7 / V4 (01.11.2026): the parent's start page — one card per linked child with the products the child shares
 * and a one-line status for each, waiting requests, removing a link and the code form.
 */
@Component({
  selector: 'app-parent-home',
  standalone: true,
  imports: [FormsModule, RouterLink, CoachingDatePipe],
  template: `
    <div class="cp-page" aria-labelledby="parent-home-title">
      <header class="cp-header">
        <h1 id="parent-home-title">Çocuklarım</h1>
        <p>Çocuğunun Koçluk ve Hızlı Okuma ilerlemesini buradan izleyebilirsin. Yalnızca çocuğunun seninle paylaştığı bölümleri görürsün; değişiklikleri çocuğun ve öğretmeni yapar.</p>
      </header>
      @if (error()) { <p class="cp-alert" role="alert">{{ error() }}</p> }
      @if (success()) { <p class="cp-success" role="status">{{ success() }}</p> }

      <section class="cp-children" aria-labelledby="children-heading">
        <h2 id="children-heading" class="cp-sr-only">Bağlı çocuklar</h2>
        @for (link of verified(); track link.id) {
          <article class="cp-child" data-testid="child-card" [attr.aria-labelledby]="'child-' + link.id">
            <header>
              <span class="cp-child-avatar" aria-hidden="true">{{ initial(link.studentName) }}</span>
              <div><h3 [id]="'child-' + link.id">{{ link.studentName }}</h3>
                <p class="cp-muted">{{ relationshipLabel(link.relationship) }}@if (link.verifiedAt) { · {{ link.verifiedAt | coachingDate }} tarihinden beri bağlı }</p></div>
            </header>
            @if (productsOf(link).length) {
              <ul class="cp-child-products">
                @for (product of productsOf(link); track product) {
                  <li>
                    <span class="cp-product-badge" data-testid="product-badge" [attr.data-product]="product">{{ productLabel(product) }}</span>
                    <span class="cp-child-status" data-testid="child-status" [class.is-muted]="status(link, product)?.failed">{{ status(link, product)?.text ?? 'Yükleniyor…' }}</span>
                    <a class="cp-child-open" [routerLink]="['/coaching-portal/parent/children', link.studentUserId]" [queryParams]="{ urun: slug(product) }"
                      [attr.aria-label]="first(link.studentName) + ' · ' + productLabel(product) + ' ayrıntıları'">Ayrıntılar →</a>
                  </li>
                }
              </ul>
            } @else {
              <p class="cp-notice">{{ hiddenText(link) }}</p>
            }
            <footer><button type="button" class="cp-link-button" data-testid="remove-link" [disabled]="busy() === link.id" (click)="remove(link)">Bağlantıyı kaldır</button></footer>
          </article>
        } @empty {
          @if (!loading()) {
            <p class="cp-empty">Henüz bağlı bir çocuğun yok. Çocuğundan bir veli kodu oluşturmasını iste: Koçluk'ta <strong>Profil ve ayarlar › Profilim</strong> bölümünden,
              Hızlı Okuma'da <strong>Veli bağlantısı</strong> sayfasından. Kodu aşağıya gir.</p>
          }
        }
      </section>

      @if (pending().length) {
        <section class="cp-section cp-pending" aria-labelledby="pending-heading" data-testid="pending-links">
          <h2 id="pending-heading">Bekleyen istekler</h2>
          <ul class="cp-rows">
            @for (link of pending(); track link.id) {
              <li><span class="cp-row-main"><strong>{{ link.studentName }}</strong><small>Çocuğunun onayını bekliyor · {{ link.requestedAt | coachingDate }}</small></span>
                <button type="button" class="cp-link-button" [disabled]="busy() === link.id" (click)="remove(link)">İsteği geri al</button></li>
            }
          </ul>
        </section>
      }

      <form class="cp-section cp-card cp-link-form" (ngSubmit)="submit()" aria-labelledby="link-heading">
        <h2 id="link-heading">Çocuğunla bağlan</h2>
        <p class="cp-muted">Kod 7 gün geçerlidir ve bir kez kullanılır. Gönderdikten sonra çocuğun isteği kendi hesabından onaylar.</p>
        <div class="cp-form">
          <label>Veli kodu<input class="cp-code" [(ngModel)]="code" name="code" maxlength="12" required autocomplete="off" placeholder="ABCD-2345" /></label>
          <label>Yakınlığın<select [(ngModel)]="relationship" name="relationship"><option value="Mother">Anne</option><option value="Father">Baba</option><option value="Guardian">Vasi</option></select></label>
          <button type="submit" class="cp-primary cp-span" [disabled]="submitting() || !code.trim()">{{ submitting() ? 'Gönderiliyor…' : 'Bağlantı isteği gönder' }}</button>
        </div>
      </form>
    </div>
  `
})
export class ParentHomeComponent implements OnInit {
  private readonly service = inject(CoachingParentService);
  private readonly toaster = inject(ToasterService);
  readonly links = signal<ParentLink[]>([]);
  readonly verified = computed(() => this.links().filter(link => link.status === 'Verified'));
  readonly pending = computed(() => this.links().filter(link => link.status === 'Pending'));
  readonly statuses = signal<Record<string, Status>>({});
  readonly loading = signal(true);
  readonly submitting = signal(false);
  readonly busy = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  readonly success = signal<string | null>(null);
  code = '';
  relationship: ParentRelationship = 'Mother';

  ngOnInit(): void { this.load(); }

  relationshipLabel(value: string): string { return parentRelationshipLabel(value); }
  productsOf(link: ParentLink) { return visibleProducts(link); }
  productLabel(product: ParentProduct) { return PRODUCT_LABELS[product]; }
  slug(product: ParentProduct) { return productSlug(product); }
  hiddenText(link: ParentLink) { return hiddenProductsText(link); }
  first(name: string) { return firstName(name); }
  initial(name: string) { return (name?.trim().charAt(0) || '?').toLocaleUpperCase('tr-TR'); }
  status(link: ParentLink, product: ParentProduct): Status | undefined { return this.statuses()[`${link.studentUserId}:${product}`]; }

  async submit(): Promise<void> {
    const code = this.code.trim();
    if (!code || this.submitting()) return;
    this.submitting.set(true); this.error.set(null); this.success.set(null);
    try {
      const link = await firstValueFrom(this.service.requestLink(code, this.relationship));
      this.code = '';
      this.success.set(`${link.studentName} için bağlantı isteğin gönderildi. Çocuğun onayladığında burada görünecek.`);
      this.load();
    } catch (response) {
      this.error.set(apiMessage(response, 'Bağlantı isteği gönderilemedi. Kodu kontrol edip yeniden dene.'));
    } finally { this.submitting.set(false); }
  }

  /** Removes a verified link or withdraws a pending request, after confirmation. */
  async remove(link: ParentLink): Promise<void> {
    const pending = link.status === 'Pending';
    const ok = await this.toaster.confirm(pending
      ? `${link.studentName} için gönderdiğin bağlantı isteği geri alınsın mı?`
      : `${link.studentName} ile bağlantın kaldırılsın mı? Artık ilerlemesini göremezsin; yeniden bağlanmak için çocuğunun yeni bir kod oluşturması gerekir.`,
      { title: pending ? 'İsteği geri al' : 'Bağlantıyı kaldır', confirmText: pending ? 'Geri al' : 'Kaldır', cancelText: 'Vazgeç' });
    if (!ok) return;
    this.busy.set(link.id); this.error.set(null); this.success.set(null);
    try {
      await firstValueFrom(this.service.revokeLink(link.id));
      this.success.set(pending ? 'Bağlantı isteğin geri alındı.' : `${link.studentName} ile bağlantın kaldırıldı.`);
      this.load();
    } catch (response) {
      this.error.set(apiMessage(response, 'Bağlantı kaldırılamadı. Yeniden dene.'));
    } finally { this.busy.set(null); }
  }

  private load(): void {
    this.loading.set(true);
    this.service.getLinks().subscribe({
      next: links => { this.links.set(links); this.loading.set(false); this.loadStatuses(); },
      error: () => { this.error.set('Bağlantıların yüklenemedi.'); this.loading.set(false); }
    });
  }

  private loadStatuses(): void {
    const set = (key: string, status: Status) => this.statuses.update(all => ({ ...all, [key]: status }));
    const failed = { text: 'Durum şu an alınamadı', failed: true };
    for (const link of this.verified()) {
      for (const product of visibleProducts(link)) {
        const key = `${link.studentUserId}:${product}`;
        if (product === 'Coaching') this.service.getCoachingWeek(link.studentUserId).subscribe({ next: week => set(key, { text: coachingStatusLine(week) }), error: () => set(key, failed) });
        else this.service.getSpeedReadingOverview(link.studentUserId).subscribe({ next: view => set(key, { text: speedReadingStatusLine(view) }), error: () => set(key, failed) });
      }
    }
  }
}

export function parentRelationshipLabel(value: string): string {
  return ({ Mother: 'Anne', Father: 'Baba', Guardian: 'Vasi', Other: 'Diğer' } as Record<string, string>)[value] ?? value;
}

/** Message from an API error body ({ error: { description } } from Identity, { message } from Coaching). */
export function apiMessage(response: unknown, fallback: string): string {
  const body = (response as { error?: { message?: string; error?: { description?: string; message?: string } } })?.error;
  return body?.error?.description ?? body?.error?.message ?? body?.message ?? fallback;
}
