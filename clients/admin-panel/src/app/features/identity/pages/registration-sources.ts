import { HttpClient, HttpParams } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../../environments/environment';

export interface RegistrationSourceRow { utmSource: string | null; utmMedium: string | null; utmCampaign: string | null; count: number }
export interface RegistrationSourceSummary { from: string; to: string; totalRegistrations: number; untagged: number; rows: RegistrationSourceRow[] }

/**
 * Roadmap O11: which campaigns (utm_source / utm_medium / utm_campaign of the landing page) brought the registrations.
 * Kept with the account at sign-up, so it also counts visitors who refused analytics cookies.
 */
@Component({
  selector: 'app-registration-sources', standalone: true, imports: [FormsModule],
  template: `
    <h1 class="mb-2 text-2xl font-bold">Kayıt kaynakları</h1>
    <p class="mb-4 max-w-3xl text-sm text-slate-600 dark:text-slate-300">Kampanya bağlantılarına <code>utm_source</code>, <code>utm_medium</code> ve <code>utm_campaign</code> ekleyin
      (örneğin <code>/coaching?utm_source=instagram&amp;utm_medium=social&amp;utm_campaign=lgs-ekim</code>). Bu bağlantıyla gelip kayıt olanlar burada sayılır.</p>
    <form class="mb-4 flex flex-wrap items-end gap-3" (ngSubmit)="load()">
      <label class="grid gap-1 text-sm">Ürün<select name="product" [(ngModel)]="product" class="rounded border px-2 py-1 dark:bg-slate-900"><option value="coaching">Koçluk</option><option value="speed-reading">Hızlı okuma</option></select></label>
      <label class="grid gap-1 text-sm">Dönem<select name="days" [(ngModel)]="days" class="rounded border px-2 py-1 dark:bg-slate-900"><option [ngValue]="7">Son 7 gün</option><option [ngValue]="30">Son 30 gün</option><option [ngValue]="90">Son 90 gün</option><option [ngValue]="365">Son 1 yıl</option></select></label>
      <button type="submit" class="rounded-lg bg-indigo-600 px-4 py-2 text-sm font-semibold text-white" [disabled]="loading()">Göster</button>
    </form>
    @if (error()) { <p role="alert" class="mb-3 rounded-lg bg-red-50 p-3 text-sm text-red-700 dark:bg-red-950/30 dark:text-red-200">{{ error() }}</p> }
    @if (summary(); as s) {
      <p class="mb-3 text-sm" data-testid="registration-total"><strong>{{ s.totalRegistrations }}</strong> kayıt · {{ s.totalRegistrations - s.untagged }} kampanyalı · {{ s.untagged }} doğrudan ya da etiketsiz</p>
      <table class="w-full text-sm">
        <thead><tr><th class="text-left">Kaynak</th><th class="text-left">Ortam</th><th class="text-left">Kampanya</th><th class="text-right">Kayıt</th></tr></thead>
        <tbody>
          @for (row of s.rows; track $index) {
            <tr class="border-t border-slate-200 dark:border-slate-700"><td>{{ row.utmSource || '—' }}</td><td>{{ row.utmMedium || '—' }}</td><td>{{ row.utmCampaign || '—' }}</td><td class="text-right font-semibold">{{ row.count }}</td></tr>
          } @empty { <tr><td colspan="4" class="py-2 text-slate-500">Bu dönemde kampanya etiketli kayıt yok.</td></tr> }
        </tbody>
      </table>
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

  async load(): Promise<void> {
    this.loading.set(true); this.error.set(null);
    try {
      const params = new HttpParams().set('product', this.product).set('days', this.days);
      this.summary.set(await firstValueFrom(this.http.get<RegistrationSourceSummary>(`${environment.apiUrl}/users/registration-sources`, { params })));
    } catch { this.error.set('Kayıt kaynakları yüklenemedi.'); }
    finally { this.loading.set(false); }
  }
}
