import { HttpClient, HttpParams } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { environment } from '../../../../environments/environment';

interface StudyReport {
  fromDate: string; toDate: string; source: string; reason: string;
  scheduledTasks: number; completedTasks: number; completionPercentage: number | null;
  plannedMinutes: number; actualMinutes: number | null;
  topics: { topicId: string | null; scheduledTasks: number; completedTasks: number; plannedMinutes: number; actualMinutes: number | null }[];
}

@Component({ selector: 'app-student-study-report', standalone: true, imports: [CommonModule, FormsModule],
  template: `
    <section class="rounded-2xl border border-slate-200 bg-white p-5 dark:border-slate-800 dark:bg-slate-900" aria-labelledby="study-report-title">
      <h2 id="study-report-title" class="text-lg font-semibold">Çalışma planı raporum</h2>
      <p class="mt-2 text-sm text-slate-500">Öğrenci beyanı · Seçilen tarihlere planlanmış işlerin güncel durumu. Resmî ölçüm veya geçmiş tarihli durum görüntüsü değildir.</p>
      <div class="my-4 flex flex-wrap items-end gap-3">
        <label class="text-sm">Başlangıç<input class="mt-1 block rounded-lg border p-2 dark:bg-slate-800" type="date" [(ngModel)]="fromDate" [disabled]="busy()"></label>
        <label class="text-sm">Bitiş<input class="mt-1 block rounded-lg border p-2 dark:bg-slate-800" type="date" [(ngModel)]="toDate" [disabled]="busy()"></label>
        <button class="rounded-lg bg-indigo-600 px-4 py-2 text-white disabled:opacity-50" type="button" (click)="load()" [disabled]="busy()">{{ busy() ? 'Yükleniyor…' : 'Raporu göster' }}</button>
      </div>
      <p *ngIf="error()" role="alert" class="text-sm text-red-600">{{ error() }}</p>
      <div *ngIf="report() as data" aria-live="polite">
        <p class="text-sm text-slate-500">{{ data.fromDate | date:'dd.MM.yyyy' }} – {{ data.toDate | date:'dd.MM.yyyy' }}</p>
        <p *ngIf="data.reason === 'NoScheduledTasks'" class="mt-3">Bu döneme planlanmış çalışma yok. Başarı oranı hesaplanmadı.</p>
        <dl *ngIf="data.scheduledTasks > 0" class="mt-4 grid gap-4 sm:grid-cols-3">
          <div><dt>Tamamlanan çalışma</dt><dd class="text-xl font-semibold">{{ data.completedTasks }} / {{ data.scheduledTasks }} · %{{ data.completionPercentage }}</dd></div>
          <div><dt>Planlanan süre</dt><dd class="text-xl font-semibold">{{ data.plannedMinutes }} dakika</dd></div>
          <div><dt>Kaydedilen süre</dt><dd class="text-xl font-semibold">{{ data.actualMinutes === null ? 'Henüz süre kaydı yok' : data.actualMinutes + ' dakika' }}</dd></div>
        </dl>
        <p class="mt-3 text-xs text-slate-500">Taslaklar ve arşivlenen planların tamamlanmamış işleri hesaba katılmaz. Tamamlanmış eski işler korunur.</p>
      </div>
    </section>`,
})
export class StudentStudyReportComponent {
  private readonly http = inject(HttpClient);
  private readonly destroyRef = inject(DestroyRef);
  readonly report = signal<StudyReport | null>(null);
  readonly error = signal<string | null>(null);
  readonly busy = signal(false);
  toDate = new Date().toISOString().slice(0, 10);
  fromDate = new Date(Date.now() - 29 * 86400000).toISOString().slice(0, 10);
  load() {
    if (this.busy()) return;
    this.report.set(null); this.error.set(null);
    const from = this.parseDate(this.fromDate), to = this.parseDate(this.toDate);
    if (from === null || to === null || to < from || to - from > 365 * 86400000) {
      this.error.set('Geçerli başlangıç ve bitiş tarihleri seç. Dönem en fazla 366 gün olabilir.'); return;
    }
    this.busy.set(true);
    const params = new HttpParams().set('fromDate', this.fromDate).set('toDate', this.toDate);
    this.http.get<{ success: boolean; data: StudyReport }>(`${environment.apiUrl}/coaching/study-planning/reports`, { params })
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.busy.set(false)))
      .subscribe({ next: response => this.report.set(response.data), error: () => this.error.set('Çalışma raporu yüklenemedi. Yeniden deneyebilirsin.') });
  }
  private parseDate(value: string): number | null {
    if (!/^\d{4}-\d{2}-\d{2}$/.test(value) || value <= '0001-01-01') return null;
    const date = new Date(`${value}T00:00:00Z`);
    return Number.isFinite(date.getTime()) && date.toISOString().slice(0, 10) === value ? date.getTime() : null;
  }
}
