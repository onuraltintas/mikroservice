import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, ElementRef, OnInit, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { ToasterService } from '../../../core/services/toaster.service';
import { scoreModelActionLabel } from './coaching-exam-groups';

export interface ScoreModelItem { id: string; year: number; scoreType: string; name: string; baseScore: number; rankRows: number; importedAt: string }
export interface ScoreModelPreview {
  fingerprint: string; year: number;
  models: { scoreType: string; name: string; baseScore: number; coefficients: number; rankRows: number; replaces: boolean }[];
  errors: { line: number; message: string }[];
}
/** KM7: one upload, restore or delete of one score type. */
export interface ScoreModelHistoryItem {
  id: string; uploadId: string; year: number; scoreType: string; name: string; action: string; baseScore: number; rankRows: number; coefficients: number;
  fileSha256: string | null; reason: string | null; actorUserId: string; createdAt: string; isCurrent: boolean;
}

/**
 * Roadmap O5: the platform administrator uploads each year's LGS and YKS score models (coefficients and score-to-rank
 * tables) as one CSV file. The file is previewed line by line and applied only when it has no errors.
 * KM7 (01.11.2026): the preview follows the year, the file input resets after each action, overwriting asks first, every
 * write takes a reason, and the history lists uploads with restore; a year's score type can be deleted.
 */
@Component({
  selector: 'app-coaching-score-models', standalone: true, imports: [FormsModule],
  template: `
    <section aria-labelledby="score-models-title">
    <h1 id="score-models-title" class="mb-2 text-2xl font-bold">Puan tahmini verileri</h1>
    <p class="mb-4 max-w-3xl text-sm text-slate-600 dark:text-slate-300">Öğrencilerin LGS ve YKS puan ve sıralama tahmini bu verilerle hesaplanır. Her yıl ÖSYM ve MEB'in açıkladığı sonuçlarla
      katsayıları ve puan–sıralama tablosunu tek bir CSV dosyasıyla yükleyin. Her puan türü için en son yılın verisi kullanılır.</p>
    @if (error()) { <p role="alert" class="mb-3 rounded-lg bg-red-50 p-3 text-sm text-red-700 dark:bg-red-950/30 dark:text-red-200">{{ error() }}</p> }
    @if (success()) { <p role="status" class="mb-3 rounded-lg bg-emerald-50 p-3 text-sm text-emerald-800 dark:bg-emerald-950/30 dark:text-emerald-200">{{ success() }}</p> }
    <section class="mb-6 rounded-xl border border-slate-200 p-4 dark:border-slate-700" aria-labelledby="score-upload-heading">
      <h2 id="score-upload-heading" class="mb-3 font-semibold">Yeni yılın verisini yükle</h2>
      <div class="flex flex-wrap items-end gap-3">
        <label class="grid gap-1 text-sm">Yıl <input type="number" name="year" min="2018" max="2100" [(ngModel)]="year" (ngModelChange)="yearChanged()" class="w-28 rounded border px-2 py-1 dark:bg-slate-900"></label>
        <label class="grid gap-1 text-sm">CSV dosyası <input #file type="file" accept=".csv,text/csv" (change)="choose($event)"></label>
        <button type="button" class="rounded-lg border px-3 py-2 text-sm" (click)="template()">Şablonu indir</button>
      </div>
      @if (preview(); as p) {
        <h3 class="mt-4 font-semibold">{{ p.year }} önizlemesi</h3>
        @if (p.errors.length) {
          <ul class="mt-2 list-disc pl-5 text-sm text-red-700 dark:text-red-300">
            @for (item of p.errors; track $index) { <li>{{ item.line ? item.line + '. satır: ' : '' }}{{ item.message }}</li> }
          </ul>
        }
        <div class="overflow-x-auto"><table class="mt-2 w-full text-sm">
          <thead><tr><th class="text-left">Puan türü</th><th class="text-left">Taban</th><th class="text-left">Katsayı</th><th class="text-left">Sıralama satırı</th><th class="text-left">Durum</th></tr></thead>
          <tbody>
            @for (model of p.models; track model.scoreType) {
              <tr><td>{{ model.name }}</td><td>{{ model.baseScore }}</td><td>{{ model.coefficients }}</td><td>{{ model.rankRows }}</td><td>{{ model.replaces ? 'Mevcut veriyi değiştirir' : 'Yeni' }}</td></tr>
            }
          </tbody>
        </table></div>
        <label class="mt-3 grid max-w-xl gap-1 text-sm">Yükleme gerekçesi (5–500 karakter)<input name="applyReason" [(ngModel)]="applyReason" maxlength="500" class="rounded border px-2 py-1 dark:bg-slate-900"></label>
        <button type="button" class="mt-3 rounded-lg bg-indigo-600 px-4 py-2 text-sm font-semibold text-white disabled:opacity-50" [disabled]="!canApply() || working() || applyReason.trim().length < 5" (click)="apply()">Verileri uygula</button>
      }
    </section>
    <section class="mb-6 rounded-xl border border-slate-200 p-4 dark:border-slate-700" aria-labelledby="score-models-heading">
      <h2 id="score-models-heading" class="mb-3 font-semibold">Yüklü veriler</h2>
      <label class="mb-3 grid max-w-xl gap-1 text-sm">Silme veya geri yükleme gerekçesi (5–500 karakter)<input name="changeReason" [(ngModel)]="changeReason" maxlength="500" class="rounded border px-2 py-1 dark:bg-slate-900"></label>
      <div class="overflow-x-auto"><table class="w-full text-sm">
        <thead><tr><th class="text-left">Yıl</th><th class="text-left">Puan türü</th><th class="text-left">Taban</th><th class="text-left">Sıralama satırı</th><th class="text-left">Yüklenme</th><th class="text-left"><span class="sr-only">İşlem</span></th></tr></thead>
        <tbody>
          @for (model of models(); track model.id) {
            <tr><td>{{ model.year }}</td><td>{{ model.name }}</td><td>{{ model.baseScore }}</td><td>{{ model.rankRows }}</td><td>{{ date(model.importedAt) }}</td>
              <td><button type="button" class="rounded border px-2 py-1 text-xs disabled:opacity-50" [disabled]="working() || changeReason.trim().length < 5" (click)="remove(model)" [attr.aria-label]="model.year + ' ' + model.name + ' verisini sil'">Sil</button></td></tr>
          } @empty { <tr><td colspan="6" class="py-2 text-slate-600 dark:text-slate-300">Henüz veri yüklenmedi; öğrencilere tahmin gösterilmiyor.</td></tr> }
        </tbody>
      </table></div>
    </section>
    <section class="rounded-xl border border-slate-200 p-4 dark:border-slate-700" aria-labelledby="score-history-heading">
      <h2 id="score-history-heading" class="mb-3 font-semibold">Geçmiş</h2>
      <label class="mb-3 grid w-40 gap-1 text-sm">Yıl<input type="number" name="historyYear" min="2018" max="2100" [(ngModel)]="historyYear" (ngModelChange)="loadHistory()" class="rounded border px-2 py-1 dark:bg-slate-900" placeholder="Tümü"></label>
      <div class="overflow-x-auto"><table class="w-full text-sm">
        <thead><tr><th class="text-left">Tarih</th><th class="text-left">İşlem</th><th class="text-left">Yıl</th><th class="text-left">Puan türü</th><th class="text-left">Taban</th><th class="text-left">Sıralama satırı</th><th class="text-left">Dosya özeti</th><th class="text-left">Gerekçe</th><th class="text-left"><span class="sr-only">İşlem</span></th></tr></thead>
        <tbody>
          @for (item of history(); track item.id) {
            <tr class="border-t dark:border-slate-700"><td>{{ dateTime(item.createdAt) }}</td><td>{{ action(item.action) }}@if (item.isCurrent) { <span class="ml-1 rounded bg-emerald-100 px-1 text-xs text-emerald-900 dark:bg-emerald-900/50 dark:text-emerald-100">Kullanımda</span> }</td>
              <td>{{ item.year }}</td><td>{{ item.name }}</td><td>{{ item.baseScore }}</td><td>{{ item.rankRows }}</td>
              <td><code class="text-xs" [title]="item.fileSha256 || ''">{{ item.fileSha256 ? item.fileSha256.slice(0, 12) : '—' }}</code></td><td class="max-w-xs break-words">{{ item.reason || '—' }}</td>
              <td>@if (item.action !== 'Delete' && !item.isCurrent) { <button type="button" class="rounded border px-2 py-1 text-xs disabled:opacity-50" [disabled]="working() || changeReason.trim().length < 5" (click)="restore(item)" [attr.aria-label]="item.year + ' ' + item.name + ' sürümünü geri yükle'">Geri yükle</button> }</td></tr>
          } @empty { <tr><td colspan="9" class="py-2 text-slate-600 dark:text-slate-300">Geçmiş kaydı yok.</td></tr> }
        </tbody>
      </table></div>
    </section>
    </section>`
})
export class CoachingScoreModelsComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly toaster = inject(ToasterService);
  private readonly url = `${environment.apiUrl}/coaching-admin/score-models`;
  private readonly fileInput = viewChild<ElementRef<HTMLInputElement>>('file');
  readonly models = signal<ScoreModelItem[]>([]);
  readonly history = signal<ScoreModelHistoryItem[]>([]);
  readonly preview = signal<ScoreModelPreview | null>(null);
  readonly working = signal(false);
  readonly error = signal<string | null>(null);
  readonly success = signal<string | null>(null);
  year = new Date().getFullYear() - 1;
  historyYear: number | null = null;
  applyReason = '';
  changeReason = '';
  private csv = '';

  ngOnInit(): void { void this.load(); }

  canApply(): boolean { const p = this.preview(); return !!p && p.errors.length === 0 && p.models.length > 0 && p.year === this.year; }
  date(value: string): string { return new Date(value).toLocaleDateString('tr-TR'); }
  dateTime(value: string): string { return new Date(value).toLocaleString('tr-TR', { dateStyle: 'short', timeStyle: 'short' }); }
  action(value: string): string { return scoreModelActionLabel(value); }

  async choose(event: Event): Promise<void> {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;
    if (file.size > 1024 * 1024) { this.error.set('Dosya en fazla 1 MB olabilir.'); this.resetFile(); return; }
    this.csv = await readText(file);
    // The same file can be chosen again right away: the input is emptied once its content is read.
    this.resetFile();
    await this.runPreview();
  }

  /** The preview always belongs to the chosen year; a changed year previews the same file again. */
  async yearChanged(): Promise<void> {
    if (!this.csv) return;
    this.preview.set(null);
    if (this.year >= 2018 && this.year <= 2100) await this.runPreview();
  }

  async apply(): Promise<void> {
    const p = this.preview();
    if (!p || !this.canApply() || this.applyReason.trim().length < 5) return;
    const replaced = p.models.filter(x => x.replaces).map(x => x.name);
    if (replaced.length && !await this.toaster.confirm(`${p.year} yılı için ${replaced.join(', ')} verisi değiştirilecek. Önceki sürüm geçmişte kalır ve geri yüklenebilir. Devam edilsin mi?`,
      { title: 'Mevcut verinin üzerine yaz', confirmText: 'Evet, değiştir', cancelText: 'Vazgeç' })) return;
    await this.run(async () => {
      const result = await firstValueFrom(this.http.post<{ applied: number }>(`${this.url}/apply`, { year: p.year, csv: this.csv, fingerprint: p.fingerprint, reason: this.applyReason.trim() }));
      this.success.set(`${p.year} yılı için ${result.applied} puan türünün verisi kaydedildi.`);
      this.preview.set(null); this.csv = ''; this.applyReason = ''; this.resetFile(); await this.load();
    });
  }

  async remove(model: ScoreModelItem): Promise<void> {
    if (this.changeReason.trim().length < 5) return;
    if (!await this.toaster.confirm(`${model.year} yılı ${model.name} verisi silinecek. Öğrencilerin tahmini bir önceki yılın verisiyle hesaplanır; silinen veri geçmişten geri yüklenebilir. Devam edilsin mi?`,
      { title: 'Puan verisini sil', confirmText: 'Evet, sil', cancelText: 'Vazgeç' })) return;
    await this.run(async () => {
      await firstValueFrom(this.http.delete(`${this.url}/${model.year}/${encodeURIComponent(model.scoreType)}`, { body: { reason: this.changeReason.trim() } }));
      this.success.set(`${model.year} yılı ${model.name} verisi silindi.`); this.changeReason = ''; await this.load();
    });
  }

  async restore(item: ScoreModelHistoryItem): Promise<void> {
    if (this.changeReason.trim().length < 5) return;
    if (!await this.toaster.confirm(`${item.year} yılı ${item.name} için ${this.dateTime(item.createdAt)} tarihli sürüm geri yüklenecek ve kullanımdaki verinin yerini alacak. Devam edilsin mi?`,
      { title: 'Sürümü geri yükle', confirmText: 'Evet, geri yükle', cancelText: 'Vazgeç' })) return;
    await this.run(async () => {
      await firstValueFrom(this.http.post(`${this.url}/history/${item.id}/restore`, { reason: this.changeReason.trim() }));
      this.success.set(`${item.year} yılı ${item.name} verisi geri yüklendi.`); this.changeReason = ''; await this.load();
    });
  }

  async template(): Promise<void> {
    await this.run(async () => {
      const blob = await firstValueFrom(this.http.get(`${this.url}/template`, { responseType: 'blob' }));
      const link = document.createElement('a'); link.href = URL.createObjectURL(blob); link.download = 'puan-tahmini-sablonu.csv'; link.click();
      setTimeout(() => URL.revokeObjectURL(link.href), 1000);
    });
  }

  async loadHistory(): Promise<void> {
    try {
      const year = this.historyYear && this.historyYear >= 2018 && this.historyYear <= 2100 ? `?year=${this.historyYear}` : '';
      this.history.set(await firstValueFrom(this.http.get<ScoreModelHistoryItem[]>(`${this.url}/history${year}`)));
    } catch (error) { this.error.set(message(error)); }
  }

  private async runPreview(): Promise<void> {
    await this.run(async () => this.preview.set(await firstValueFrom(this.http.post<ScoreModelPreview>(`${this.url}/preview`, { year: this.year, csv: this.csv, fingerprint: null }))));
  }

  private resetFile(): void { const input = this.fileInput()?.nativeElement; if (input) input.value = ''; }

  private async load(): Promise<void> {
    try { this.models.set(await firstValueFrom(this.http.get<ScoreModelItem[]>(this.url))); }
    catch (error) { this.error.set(message(error)); }
    await this.loadHistory();
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
