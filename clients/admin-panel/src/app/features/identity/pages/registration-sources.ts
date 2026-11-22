import { HttpClient, HttpParams } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { AdminBarsComponent, AdminPoint, AdminStatComponent } from '../../../shared/components/admin-charts';
import { environment } from '../../../../environments/environment';

export interface RegistrationSourceRow { utmSource: string | null; utmMedium: string | null; utmCampaign: string | null; count: number }
export interface RegistrationSourceSummary { from: string; to: string; totalRegistrations: number; untagged: number; rows: RegistrationSourceRow[] }

/**
 * Roadmap O11: which campaigns (utm_source / utm_medium / utm_campaign of the landing page) brought the registrations.
 * Kept with the account at sign-up, so it also counts visitors who refused analytics cookies.
 */
@Component({
  selector: 'app-registration-sources', standalone: true, imports: [FormsModule, AdminStatComponent, AdminBarsComponent],
  template: `
    <header class="mb-5">
      <h1 class="text-2xl font-bold">Kayıt kaynakları</h1>
      <p class="mt-1 max-w-3xl text-sm text-(--ui-text-muted)">Kampanya bağlantılarına <code>utm_source</code>, <code>utm_medium</code> ve <code>utm_campaign</code> ekleyin
        (örneğin <code>/coaching?utm_source=instagram&amp;utm_medium=social&amp;utm_campaign=lgs-ekim</code>). Bu bağlantıyla gelip kayıt olanlar burada sayılır.</p>
    </header>
    <form class="mb-5 flex flex-wrap items-end gap-3 rounded-2xl border border-(--ui-border) bg-(--ui-surface) p-4" (ngSubmit)="load()">
      <label class="grid gap-1 text-sm font-medium">Ürün<select name="product" [(ngModel)]="product" class="min-h-10 rounded-lg border border-(--ui-border-strong) bg-(--ui-surface) px-3"><option value="coaching">Koçluk</option><option value="speed-reading">Hızlı okuma</option></select></label>
      <label class="grid gap-1 text-sm font-medium">Dönem<select name="days" [(ngModel)]="days" class="min-h-10 rounded-lg border border-(--ui-border-strong) bg-(--ui-surface) px-3"><option [ngValue]="7">Son 7 gün</option><option [ngValue]="30">Son 30 gün</option><option [ngValue]="90">Son 90 gün</option><option [ngValue]="365">Son 1 yıl</option></select></label>
      <button type="submit" class="min-h-10 rounded-full bg-(--ui-brand) px-5 text-sm font-semibold text-(--ui-brand-contrast)" [disabled]="loading()">Göster</button>
    </form>
    @if (loading() && !summary()) { <p role="status" class="text-sm text-(--ui-text-muted)">Kayıt kaynakları yükleniyor…</p> }
    @if (error()) { <p role="alert" class="mb-3 rounded-lg bg-(--ui-danger-soft) p-3 text-sm text-(--ui-danger)">{{ error() }}</p> }
    @if (summary(); as s) {
      <p class="sr-only" data-testid="registration-total">{{ total(s) }} kayıt, {{ tagged(s) }} kampanyalı, {{ untagged(s) }} doğrudan ya da etiketsiz</p>
      <div class="mb-5 grid gap-3 sm:grid-cols-3" aria-hidden="true">
        <admin-stat label="Toplam kayıt" [value]="total(s)" tone="info" />
        <admin-stat label="Kampanyadan gelen" [value]="tagged(s)" tone="success" />
        <admin-stat label="Doğrudan ya da etiketsiz" [value]="untagged(s)" tone="honey" />
      </div>
      @if (rows(s).length) {
        <section class="mb-5 rounded-2xl border border-(--ui-border) bg-(--ui-surface) p-4">
          <h2 class="mb-3 text-base font-semibold">Kampanyalara göre kayıt</h2>
          <admin-bars [points]="bars(s)" />
        </section>
      }
      <div class="overflow-x-auto rounded-2xl border border-(--ui-border) bg-(--ui-surface)">
        <table class="w-full text-sm">
          <thead><tr><th class="p-3 text-left">Kaynak</th><th class="p-3 text-left">Ortam</th><th class="p-3 text-left">Kampanya</th><th class="p-3 text-right">Kayıt</th></tr></thead>
          <tbody>
            @for (row of rows(s); track $index) {
              <tr class="border-t border-(--ui-border)"><td class="p-3">{{ row.utmSource || '—' }}</td><td class="p-3">{{ row.utmMedium || '—' }}</td><td class="p-3">{{ row.utmCampaign || '—' }}</td><td class="p-3 text-right font-semibold tabular-nums">{{ row.count }}</td></tr>
            } @empty { <tr><td colspan="4" class="p-6 text-center text-(--ui-text-muted)">Bu dönemde kampanya etiketli kayıt yok. Bağlantılarınıza utm etiketlerini ekledikten sonra gelen kayıtlar burada görünür.</td></tr> }
          </tbody>
        </table>
      </div>
    }
  `
})
export class RegistrationSourcesComponent implements OnInit {
  private readonly http = inject(HttpClient);
  readonly summary = signal<RegistrationSourceSummary | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  product = 'coaching';
  days = 30;

  ngOnInit(): void { void this.load(); }

  total(s: RegistrationSourceSummary): number { return Number(s.totalRegistrations) || 0; }
  untagged(s: RegistrationSourceSummary): number { return Number(s.untagged) || 0; }
  tagged(s: RegistrationSourceSummary): number { return Math.max(0, this.total(s) - this.untagged(s)); }
  rows(s: RegistrationSourceSummary): RegistrationSourceRow[] { return Array.isArray(s.rows) ? s.rows : []; }
  bars(s: RegistrationSourceSummary): AdminPoint[] {
    return this.rows(s).slice(0, 12).map(row => ({ label: row.utmCampaign || row.utmSource || 'Etiketsiz', value: Number(row.count) || 0 }));
  }

  async load(): Promise<void> {
    this.loading.set(true); this.error.set(null);
    try {
      const params = new HttpParams().set('product', this.product).set('days', this.days);
      this.summary.set(await firstValueFrom(this.http.get<RegistrationSourceSummary>(`${environment.apiUrl}/users/registration-sources`, { params })));
    } catch { this.error.set('Kayıt kaynakları yüklenemedi. Sayfayı yenileyip yeniden deneyin.'); }
    finally { this.loading.set(false); }
  }
}
