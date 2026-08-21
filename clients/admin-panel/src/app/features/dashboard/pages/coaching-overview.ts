import { CommonModule, isPlatformBrowser } from '@angular/common';
import { Component, inject, OnInit, PLATFORM_ID, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import {
  CoachingAdminOverview,
  CoachingAdminService,
  InstitutionCoachingComparison
} from '../../../core/services/coaching-admin.service';
import { InstitutionDto, InstitutionService } from '../../../core/services/institution.service';

@Component({
  selector: 'app-coaching-overview',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <section class="space-y-6">
      <div class="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 class="text-2xl font-bold text-gray-900 dark:text-white">Koçluk Özeti</h1>
          <p class="text-sm text-gray-500 dark:text-gray-400">Sistem yöneticisi için salt-okunur operasyon görünümü.</p>
        </div>
        <button type="button" (click)="load()" [disabled]="loading()"
          class="rounded-lg bg-indigo-600 px-4 py-2 text-sm font-medium text-white disabled:opacity-50">
          Yenile
        </button>
      </div>

      @if (error()) {
        <div class="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700">{{ error() }}</div>
      }

      @if (loading() && !overview()) {
        <div class="rounded-xl bg-white p-6 shadow-sm dark:bg-gray-800">Yükleniyor...</div>
      } @else if (overview(); as data) {
        <div class="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
          @for (card of cards(data); track card.label) {
            <div class="rounded-xl border border-gray-200 bg-white p-5 shadow-sm dark:border-gray-700 dark:bg-gray-800">
              <p class="text-sm text-gray-500 dark:text-gray-400">{{ card.label }}</p>
              <p class="mt-2 text-3xl font-bold text-gray-900 dark:text-white">{{ card.value }}</p>
            </div>
          }
        </div>

        <div class="space-y-5 rounded-xl border border-gray-200 bg-white p-5 shadow-sm dark:border-gray-700 dark:bg-gray-800">
          <div>
            <h2 class="font-semibold text-gray-900 dark:text-white">Kurum / sınıf karşılaştırması</h2>
            <p class="mt-1 text-sm text-gray-500 dark:text-gray-400">Yalnızca Identity tarafından aktif kurum kapsamına alınan öğrenciler aggregate edilir.</p>
          </div>
          <div class="grid gap-4 md:grid-cols-4">
            <label class="text-sm text-gray-700 dark:text-gray-200">Kurum
              <select [(ngModel)]="selectedInstitutionId" class="mt-1 w-full rounded border px-3 py-2 dark:border-gray-600 dark:bg-gray-900">
                <option value="">Kurum seçin</option>
                @for (institution of institutions(); track institution.id) { <option [value]="institution.id">{{ institution.name }}</option> }
              </select>
            </label>
            <label class="text-sm text-gray-700 dark:text-gray-200">Sınıf
              <select [(ngModel)]="selectedGradeLevel" class="mt-1 w-full rounded border px-3 py-2 dark:border-gray-600 dark:bg-gray-900">
                <option [ngValue]="null">Tüm sınıflar</option>
                @for (grade of grades; track grade) { <option [ngValue]="grade">{{ grade }}. sınıf</option> }
              </select>
            </label>
            <label class="text-sm text-gray-700 dark:text-gray-200">Başlangıç
              <input [(ngModel)]="fromDate" type="date" class="mt-1 w-full rounded border px-3 py-2 dark:border-gray-600 dark:bg-gray-900" />
            </label>
            <label class="text-sm text-gray-700 dark:text-gray-200">Bitiş
              <input [(ngModel)]="toDate" type="date" class="mt-1 w-full rounded border px-3 py-2 dark:border-gray-600 dark:bg-gray-900" />
            </label>
          </div>
          <button type="button" (click)="loadComparison()" [disabled]="comparisonLoading() || !selectedInstitutionId" class="rounded-lg bg-indigo-600 px-4 py-2 text-sm font-medium text-white disabled:opacity-50">{{ comparisonLoading() ? 'Rapor hazırlanıyor…' : 'Karşılaştırmayı getir' }}</button>
          @if (comparisonError()) { <div class="rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-700">{{ comparisonError() }}</div> }
          @if (comparison(); as report) {
            <div class="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
              <div class="rounded-lg bg-gray-50 p-4 dark:bg-gray-900/50"><p class="text-xs text-gray-500">Aktif öğrenci</p><p class="mt-1 text-2xl font-bold">{{ report.studentCount }}</p></div>
              <div class="rounded-lg bg-gray-50 p-4 dark:bg-gray-900/50"><p class="text-xs text-gray-500">Ödev teslim oranı</p><p class="mt-1 text-2xl font-bold">{{ percentage(report.submittedAssignmentCount, report.assignedAssignmentCount) }}</p></div>
              <div class="rounded-lg bg-gray-50 p-4 dark:bg-gray-900/50"><p class="text-xs text-gray-500">Ödev ortalaması</p><p class="mt-1 text-2xl font-bold">{{ formatPercent(report.averageAssignmentPercentage) }}</p></div>
              <div class="rounded-lg bg-gray-50 p-4 dark:bg-gray-900/50"><p class="text-xs text-gray-500">Sınav ortalaması</p><p class="mt-1 text-2xl font-bold">{{ formatPercent(report.averageExamPercentage) }}</p></div>
              <div class="rounded-lg bg-gray-50 p-4 dark:bg-gray-900/50"><p class="text-xs text-gray-500">Katılım oranı</p><p class="mt-1 text-2xl font-bold">{{ formatPercent(report.attendancePercentage) }}</p></div>
              <div class="rounded-lg bg-gray-50 p-4 dark:bg-gray-900/50"><p class="text-xs text-gray-500">Hedef ilerlemesi</p><p class="mt-1 text-2xl font-bold">{{ report.averageGoalProgress }}%</p></div>
              <div class="rounded-lg bg-gray-50 p-4 dark:bg-gray-900/50"><p class="text-xs text-gray-500">Tamamlanan hedef</p><p class="mt-1 text-2xl font-bold">{{ report.completedGoalCount }}/{{ report.goalCount }}</p></div>
              <div class="rounded-lg bg-gray-50 p-4 dark:bg-gray-900/50"><p class="text-xs text-gray-500">Seans</p><p class="mt-1 text-2xl font-bold">{{ report.sessionCount }}</p></div>
            </div>
          }
        </div>

        <div class="rounded-xl border border-gray-200 bg-white shadow-sm dark:border-gray-700 dark:bg-gray-800">
          <div class="border-b border-gray-200 px-5 py-4 dark:border-gray-700">
            <h2 class="font-semibold text-gray-900 dark:text-white">Son ödevler</h2>
          </div>
          <div class="overflow-x-auto">
            <table class="min-w-full divide-y divide-gray-200 text-sm dark:divide-gray-700">
              <thead class="bg-gray-50 text-left text-xs uppercase text-gray-500 dark:bg-gray-900/40 dark:text-gray-400">
                <tr><th class="px-5 py-3">Başlık</th><th class="px-5 py-3">Durum</th><th class="px-5 py-3">Öğrenci</th><th class="px-5 py-3">Teslim</th><th class="px-5 py-3">Teslim tarihi</th></tr>
              </thead>
              <tbody class="divide-y divide-gray-200 dark:divide-gray-700">
                @for (assignment of data.recentAssignments; track assignment.id) {
                  <tr><td class="px-5 py-3 font-medium text-gray-900 dark:text-white">{{ assignment.title }}</td><td class="px-5 py-3">{{ assignment.status }}</td><td class="px-5 py-3">{{ assignment.studentCount }}</td><td class="px-5 py-3">{{ assignment.submittedStudentCount }}</td><td class="px-5 py-3">{{ assignment.dueDate | date:'shortDate' }}</td></tr>
                } @empty {
                  <tr><td colspan="5" class="px-5 py-8 text-center text-gray-500">Kayıt bulunamadı.</td></tr>
                }
              </tbody>
            </table>
          </div>
        </div>
      }
    </section>
  `
})
export class CoachingOverviewComponent implements OnInit {
  private readonly service = inject(CoachingAdminService);
  private readonly institutionService = inject(InstitutionService);
  private readonly platformId = inject(PLATFORM_ID);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly overview = signal<CoachingAdminOverview | null>(null);
  readonly institutions = signal<InstitutionDto[]>([]);
  readonly comparison = signal<InstitutionCoachingComparison | null>(null);
  readonly comparisonLoading = signal(false);
  readonly comparisonError = signal<string | null>(null);
  readonly grades = Array.from({ length: 12 }, (_, index) => index + 1);
  selectedInstitutionId = '';
  selectedGradeLevel: number | null = null;
  fromDate = new Date(Date.now() - 30 * 24 * 60 * 60 * 1000).toISOString().slice(0, 10);
  toDate = new Date().toISOString().slice(0, 10);

  ngOnInit() {
    if (isPlatformBrowser(this.platformId)) {
      this.load();
      this.loadInstitutions();
    }
  }

  load() {
    this.loading.set(true);
    this.error.set(null);
    this.service.getOverview().pipe(finalize(() => this.loading.set(false))).subscribe({
      next: value => this.overview.set(value),
      error: () => this.error.set('Koçluk özeti yüklenemedi.')
    });
  }

  loadInstitutions() {
    this.institutionService.getAll(1, 100, '', true).subscribe({
      next: response => this.institutions.set(response.items ?? []),
      error: () => this.comparisonError.set('Kurum listesi yüklenemedi.')
    });
  }

  loadComparison() {
    if (!this.selectedInstitutionId) return;
    const fromDate = this.toUtcDate(this.fromDate, false);
    const toDate = this.toUtcDate(this.toDate, true);
    if (!fromDate || !toDate || fromDate > toDate) {
      this.comparisonError.set('Rapor tarih aralığı geçersiz.');
      return;
    }

    this.comparisonLoading.set(true);
    this.comparisonError.set(null);
    this.service.getInstitutionComparison(this.selectedInstitutionId, {
      gradeLevel: this.selectedGradeLevel ?? undefined,
      fromDate,
      toDate
    }).pipe(finalize(() => this.comparisonLoading.set(false))).subscribe({
      next: report => this.comparison.set(report),
      error: () => this.comparisonError.set('Karşılaştırmalı rapor yüklenemedi.')
    });
  }

  percentage(numerator: number, denominator: number) {
    return denominator === 0 ? '—' : `${Math.round(numerator / denominator * 100)}%`;
  }

  formatPercent(value?: number) {
    return value === undefined || value === null ? '—' : `${value.toFixed(2)}%`;
  }

  private toUtcDate(value: string, endOfDay: boolean) {
    if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) return null;
    const suffix = endOfDay ? 'T23:59:59.999Z' : 'T00:00:00.000Z';
    const date = new Date(`${value}${suffix}`);
    return Number.isNaN(date.getTime()) ? null : date.toISOString();
  }

  cards(data: CoachingAdminOverview) {
    return [
      { label: 'Toplam ödev', value: data.totalAssignments },
      { label: 'Aktif ödev', value: data.activeAssignments },
      { label: 'Toplam sınav', value: data.totalExams },
      { label: 'Sınav sonucu', value: data.totalExamResults },
      { label: 'Toplam seans', value: data.totalSessions },
      { label: 'Yaklaşan seans', value: data.upcomingSessions },
      { label: 'Toplam hedef', value: data.totalGoals },
      { label: 'Tamamlanan hedef', value: data.completedGoals }
    ];
  }
}
