import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../../environments/environment';

export interface ScoreModelItem { id: string; year: number; scoreType: string; name: string; baseScore: number; rankRows: number; importedAt: string }
export interface ScoreModelPreview {
  fingerprint: string; year: number;
  models: { scoreType: string; name: string; baseScore: number; coefficients: number; rankRows: number; replaces: boolean }[];
  errors: { line: number; message: string }[];
}

/**
 * Roadmap O5: the platform administrator uploads each year's LGS and YKS score models (coefficients and score-to-rank
 * tables) as one CSV file. The file is previewed line by line and applied only when it has no errors.
 */
@Component({
  selector: 'app-coaching-score-models', standalone: true, imports: [FormsModule],
  template: `
    <h1 class="mb-2 text-2xl font-bold">Puan tahmini verileri</h1>
    <p class="mb-4 max-w-3xl text-sm text-slate-600 dark:text-slate-300">Öğrencilerin LGS ve YKS puan ve sıralama tahmini bu verilerle hesaplanır. Her yıl ÖSYM ve MEB'in açıkladığı sonuçlarla
      katsayıları ve puan–sıralama tablosunu tek bir CSV dosyasıyla yükleyin. Her puan türü için en son yılın verisi kullanılır.</p>
    @if (error()) { <p role="alert" class="mb-3 rounded-lg bg-red-50 p-3 text-sm text-red-700 dark:bg-red-950/30 dark:text-red-200">{{ error() }}</p> }
    @if (success()) { <p role="status" class="mb-3 rounded-lg bg-emerald-50 p-3 text-sm text-emerald-800 dark:bg-emerald-950/30 dark:text-emerald-200">{{ success() }}</p> }
    <section class="mb-6 rounded-xl border border-slate-200 p-4 dark:border-slate-700" aria-labelledby="score-upload-heading">
      <h2 id="score-upload-heading" class="mb-3 font-semibold">Yeni yılın verisini yükle</h2>
      <div class="flex flex-wrap items-end gap-3">
        <label class="grid gap-1 text-sm">Yıl <input type="number" name="year" min="2018" max="2100" [(ngModel)]="year" class="w-28 rounded border px-2 py-1 dark:bg-slate-900"></label>
        <label class="grid gap-1 text-sm">CSV dosyası <input type="file" accept=".csv,text/csv" (change)="choose($event)"></label>
        <button type="button" class="rounded-lg border px-3 py-2 text-sm" (click)="template()">Şablonu indir</button>
      </div>
      @if (preview(); as p) {
        <h3 class="mt-4 font-semibold">{{ p.year }} önizlemesi</h3>
        @if (p.errors.length) {
          <ul class="mt-2 list-disc pl-5 text-sm text-red-700 dark:text-red-300">
            @for (item of p.errors; track $index) { <li>{{ item.line ? item.line + '. satır: ' : '' }}{{ item.message }}</li> }
          </ul>
        }
        <table class="mt-2 w-full text-sm">
          <thead><tr><th class="text-left">Puan türü</th><th class="text-left">Taban</th><th class="text-left">Katsayı</th><th class="text-left">Sıralama satırı</th><th class="text-left">Durum</th></tr></thead>
          <tbody>
            @for (model of p.models; track model.scoreType) {
              <tr><td>{{ model.name }}</td><td>{{ model.baseScore }}</td><td>{{ model.coefficients }}</td><td>{{ model.rankRows }}</td><td>{{ model.replaces ? 'Mevcut veriyi değiştirir' : 'Yeni' }}</td></tr>
            }
          </tbody>
        </table>
        <button type="button" class="mt-3 rounded-lg bg-indigo-600 px-4 py-2 text-sm font-semibold text-white disabled:opacity-50" [disabled]="!canApply() || working()" (click)="apply()">Verileri uygula</button>
      }
    </section>
    <section class="rounded-xl border border-slate-200 p-4 dark:border-slate-700" aria-labelledby="score-models-heading">
      <h2 id="score-models-heading" class="mb-3 font-semibold">Yüklü veriler</h2>
      <table class="w-full text-sm">
        <thead><tr><th class="text-left">Yıl</th><th class="text-left">Puan türü</th><th class="text-left">Taban</th><th class="text-left">Sıralama satırı</th><th class="text-left">Yüklenme</th></tr></thead>
        <tbody>
          @for (model of models(); track model.id) {
            <tr><td>{{ model.year }}</td><td>{{ model.name }}</td><td>{{ model.baseScore }}</td><td>{{ model.rankRows }}</td><td>{{ date(model.importedAt) }}</td></tr>
          } @empty { <tr><td colspan="5" class="py-2 text-slate-500">Henüz veri yüklenmedi; öğrencilere tahmin gösterilmiyor.</td></tr> }
        </tbody>
      </table>
    </section>`
})
export class CoachingScoreModelsComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}/coaching-admin/score-models`;
  readonly models = signal<ScoreModelItem[]>([]);
  readonly preview = signal<ScoreModelPreview | null>(null);
  readonly working = signal(false);
  readonly error = signal<string | null>(null);
  readonly success = signal<string | null>(null);
  year = new Date().getFullYear() - 1;
  private csv = '';

  ngOnInit(): void { void this.load(); }

  canApply(): boolean { const p = this.preview(); return !!p && p.errors.length === 0 && p.models.length > 0; }
  date(value: string): string { return new Date(value).toLocaleDateString('tr-TR'); }

  async choose(event: Event): Promise<void> {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;
    if (file.size > 1024 * 1024) { this.error.set('Dosya en fazla 1 MB olabilir.'); return; }
    this.csv = await readText(file);
    await this.run(async () => this.preview.set(await firstValueFrom(this.http.post<ScoreModelPreview>(`${this.url}/preview`, { year: this.year, csv: this.csv, fingerprint: null }))));
  }

  async apply(): Promise<void> {
    const p = this.preview();
    if (!p || !this.canApply()) return;
    await this.run(async () => {
      const result = await firstValueFrom(this.http.post<{ applied: number }>(`${this.url}/apply`, { year: p.year, csv: this.csv, fingerprint: p.fingerprint }));
      this.success.set(`${p.year} yılı için ${result.applied} puan türünün verisi kaydedildi.`);
      this.preview.set(null); await this.load();
    });
  }

  async template(): Promise<void> {
    await this.run(async () => {
      const blob = await firstValueFrom(this.http.get(`${this.url}/template`, { responseType: 'blob' }));
      const link = document.createElement('a'); link.href = URL.createObjectURL(blob); link.download = 'puan-tahmini-sablonu.csv'; link.click();
      setTimeout(() => URL.revokeObjectURL(link.href), 1000);
    });
  }

  private async load(): Promise<void> {
    try { this.models.set(await firstValueFrom(this.http.get<ScoreModelItem[]>(this.url))); }
    catch (error) { this.error.set(message(error)); }
  }

  private async run(action: () => Promise<void>): Promise<void> {
    this.working.set(true); this.error.set(null); this.success.set(null);
    try { await action(); } catch (error) { this.error.set(message(error)); } finally { this.working.set(false); }
  }
}

function readText(file: File): Promise<string> {
  if (typeof file.text === 'function') return file.text();
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(String(reader.result ?? '')); reader.onerror = () => reject(reader.error);
    reader.readAsText(file);
  });
}

function message(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    const body = error.error as { message?: string; detail?: string } | null;
    if (body && typeof body === 'object' && (body.message || body.detail)) return (body.message || body.detail)!;
  }
  return 'İşlem tamamlanamadı.';
}
