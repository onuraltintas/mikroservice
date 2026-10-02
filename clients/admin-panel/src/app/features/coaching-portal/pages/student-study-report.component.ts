import { HttpClient, HttpParams } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { Component, DestroyRef, Input, OnChanges, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { finalize, Subscription } from 'rxjs';
import { environment } from '../../../../environments/environment';

interface StudyReport {
  fromDate: string; toDate: string; source: string; reason: string;
  scheduledTasks: number; completedTasks: number; completionPercentage: number | null;
  plannedMinutes: number; actualMinutes: number | null;
  topics: { topicId: string | null; topicName?: string | null; scheduledTasks: number; completedTasks: number; plannedMinutes: number; actualMinutes: number | null }[];
  examGroups?: { source: string; examType: string; maxScore: number; count: number; averagePercentage: number }[];
  lessonResults?: { source: string; examType: string; lessonName?: string | null; topicName?: string | null;
    questionCount: number; correct: number; wrong: number; empty: number }[];
  goals?: { goalId: string; title: string; source: string; recordedProgress: number; isCompleted: boolean;
    targetDate?: string | null; targetScore?: number | null; targetExamType?: string | null; targetSubject?: string | null;
    targetMaxScore?: number | null; scoreAssessment?: { reason: string; comparisons: { resultId: string; examId: string;
      source: string; examDate: string; score: number; targetAttainmentPercentage: number; remainingScore: number; targetReached: boolean }[] } }[];
}

@Component({ selector: 'app-student-study-report', standalone: true, imports: [CommonModule, FormsModule],
  template: `
    <section class="rounded-2xl border border-slate-200 bg-white p-5 dark:border-slate-800 dark:bg-slate-900" aria-labelledby="study-report-title">
      <h2 id="study-report-title" class="text-lg font-semibold">{{ adminStudentId ? 'Öğrencinin çalışma planı raporu' : 'Çalışma planı raporum' }}</h2>
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
        <div *ngIf="data.topics.length" class="mt-4 overflow-x-auto">
          <table class="w-full text-left text-sm"><caption class="py-2 text-left font-semibold">Konu bazında çalışma · Öğrenci beyanı</caption>
            <thead><tr><th class="p-2">Konu</th><th class="p-2">Tamamlanan / planlanan</th><th class="p-2">Planlanan / kaydedilen dakika</th></tr></thead>
            <tbody><tr *ngFor="let topic of data.topics" class="border-t dark:border-slate-800"><td class="p-2">{{ topic.topicName ?? (topic.topicId ? 'Konu adı bulunamadı' : 'Kataloğa bağlanmamış çalışma') }}</td><td class="p-2">{{ topic.completedTasks }} / {{ topic.scheduledTasks }}</td><td class="p-2">{{ topic.plannedMinutes }} / {{ topic.actualMinutes ?? 'Kayıt yok' }}</td></tr></tbody>
          </table>
        </div>
        <h3 class="mt-5 font-semibold">Dönem sınav özeti</h3>
        <p *ngIf="!data.examGroups?.length" class="mt-2 text-sm">Bu dönemde sınav sonucu yok; başarı ortalaması hesaplanmadı.</p>
        <p class="mt-2 text-xs text-slate-500">Kaynak, sınav türü ve puan ölçeği ayrı değerlendirilir. Ortalama, resmî sınav puanı veya yerleşme tahmini değildir.</p>
        <div *ngFor="let group of data.examGroups" class="mt-2 rounded-lg border p-3 text-sm dark:border-slate-800">
          {{ sourceLabel(group.source) }} · {{ group.examType }} · {{ group.maxScore }} puan ölçeği · {{ group.count }} sonuç · Ortalama %{{ group.averagePercentage }}
        </div>
        <div *ngIf="data.lessonResults?.length" class="mt-4 overflow-x-auto">
          <table class="w-full text-left text-sm"><caption class="py-2 text-left font-semibold">Ders / konu soru dağılımı</caption>
            <thead><tr><th class="p-2">Ders / konu</th><th class="p-2">Kaynak / tür</th><th class="p-2">Soru</th><th class="p-2">Doğru / yanlış / boş</th></tr></thead>
            <tbody><tr *ngFor="let lesson of data.lessonResults" class="border-t dark:border-slate-800"><td class="p-2">{{ lesson.lessonName ?? 'Ders adı bulunamadı' }} · {{ lesson.topicName ?? 'Ders toplamı' }}</td><td class="p-2">{{ sourceLabel(lesson.source) }} · {{ lesson.examType }}</td><td class="p-2">{{ lesson.questionCount }}</td><td class="p-2">{{ lesson.correct }} / {{ lesson.wrong }} / {{ lesson.empty }}</td></tr></tbody>
          </table>
        </div>
        <h3 class="mt-6 font-semibold">{{ adminStudentId ? 'Öğrencinin güncel hedefleri' : 'Güncel hedeflerim' }}</h3>
        <p class="mt-2 text-xs text-slate-500">Seçilen dönemin geçmiş durumunu göstermez. İlerleme, hedef kaydına girilen değerdir; sınavlardan veya çalışma planından otomatik hesaplanmaz. Otomatik başarı veya yerleşme tahmini değildir.</p>
        <p *ngIf="!data.goals?.length" class="mt-2 text-sm">Henüz kayıtlı hedefin yok.</p>
        <ul class="mt-3 space-y-3">
          <li *ngFor="let goal of data.goals" class="rounded-lg border p-3 text-sm dark:border-slate-800">
            <h4 class="font-semibold">{{ goal.title }}</h4>
            <p class="mt-1 text-xs text-slate-500">{{ goal.source === 'TeacherSet' ? 'Öğretmenin belirlediği hedef' : 'Belirleyen kişi kaydedilmemiş' }}</p>
            <p class="mt-2">Kaydedilen ilerleme: %{{ goal.recordedProgress }} · {{ goal.isCompleted ? 'Tamamlandı olarak işaretli' : 'Devam ediyor' }}</p>
            <p *ngIf="goal.targetDate">Hedef tarihi: {{ goal.targetDate | date:'dd.MM.yyyy':'UTC' }}</p>
            <p *ngIf="goal.targetScore !== null && goal.targetScore !== undefined">Hedef puanı: {{ goal.targetScore }}<span *ngIf="goal.targetExamType"> · {{ goal.targetExamType }}</span></p>
            <p *ngIf="goal.targetSubject">Hedef ders: {{ goal.targetSubject }}</p>
            <div class="mt-3 rounded-lg bg-indigo-50 p-3 dark:bg-indigo-950/30" *ngIf="goal.scoreAssessment as assessment">
              <h5 class="font-semibold">Otomatik puan karşılaştırması</h5>
              <p class="mt-1 text-xs text-slate-500">Dönemdeki son uyumlu sonuç / hedef puanı. Öğrenme artışı veya yerleşme olasılığı değildir; hedefi otomatik tamamlamaz.</p>
              <p *ngIf="!assessment.comparisons.length" class="mt-2">{{ scoreReason(assessment.reason) }}</p>
              <div *ngFor="let comparison of assessment.comparisons" class="mt-3">
                <p>{{ sourceLabel(comparison.source) }} · {{ comparison.examDate | date:'dd.MM.yyyy':'UTC' }} · Sonuç: {{ comparison.score }} / {{ goal.targetMaxScore }}</p>
                <p class="mt-1 font-semibold">Puan hedefine erişim: %{{ comparison.targetAttainmentPercentage }} · Kalan puan: {{ comparison.remainingScore }}</p>
                <p *ngIf="comparison.targetReached" class="mt-1">Bu sonuçta puan hedefi karşılandı.</p>
              </div>
            </div>
          </li>
        </ul>
      </div>
    </section>`,
})
export class StudentStudyReportComponent implements OnChanges {
  @Input() adminStudentId: string | null = null;
  private request?: Subscription;
  ngOnChanges() { this.request?.unsubscribe(); this.report.set(null); this.error.set(null); this.busy.set(false); }
  private readonly http = inject(HttpClient);
  private readonly destroyRef = inject(DestroyRef);
  readonly report = signal<StudyReport | null>(null);
  readonly error = signal<string | null>(null);
  readonly busy = signal(false);
  private readonly today = new Date();
  toDate = this.localDate(this.today);
  fromDate = this.localDate(new Date(this.today.getFullYear(), this.today.getMonth(), this.today.getDate() - 29));
  private localDate(date: Date) {
    return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;
  }
  sourceLabel(source: string) { return source === 'StudentReported' ? 'Öğrenci beyanı' : source === 'TeacherRecorded' ? 'Öğretmen kaydı' : 'Kaynak belirtilmemiş'; }
  scoreReason(reason: string) {
    return reason === 'NoMatchingResults' ? 'Seçilen dönemde aynı tür ve puan ölçeğinde uyumlu sınav sonucu yok.'
      : reason === 'SubjectTargetNotComparable' ? 'Ders hedefi toplam sınav puanıyla karşılaştırılamaz.'
      : 'Otomatik karşılaştırma için hedef puanı, sınav türü ve puan ölçeğini birlikte belirleyin.';
  }
  load() {
    if (this.busy()) return;
    this.report.set(null); this.error.set(null);
    const from = this.parseDate(this.fromDate), to = this.parseDate(this.toDate);
    if (from === null || to === null || to < from || to - from > 365 * 86400000) {
      this.error.set('Geçerli başlangıç ve bitiş tarihleri seç. Dönem en fazla 366 gün olabilir.'); return;
    }
    this.busy.set(true);
    const params = new HttpParams().set('fromDate', this.fromDate).set('toDate', this.toDate);
    const url = this.adminStudentId ? `${environment.apiUrl}/coaching-admin/students/${encodeURIComponent(this.adminStudentId)}/study/report` : `${environment.apiUrl}/coaching/study-planning/reports`;
    this.request = this.http.get<{ success: boolean; data: StudyReport }>(url, { params })
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.busy.set(false)))
      .subscribe({ next: response => this.report.set(response.data), error: () => this.error.set('Çalışma raporu yüklenemedi. Yeniden deneyebilirsin.') });
  }
  private parseDate(value: string): number | null {
    if (!/^\d{4}-\d{2}-\d{2}$/.test(value) || value <= '0001-01-01') return null;
    const date = new Date(`${value}T00:00:00Z`);
    return Number.isFinite(date.getTime()) && date.toISOString().slice(0, 10) === value ? date.getTime() : null;
  }
}
