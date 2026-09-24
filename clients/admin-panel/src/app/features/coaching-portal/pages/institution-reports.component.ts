import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import {
  CoachingAdminService, InstitutionCoachingComparison, InstitutionEarlyWarningReport,
  StudentEarlyWarning
} from '../../../core/services/coaching-admin.service';

@Component({
  selector: 'app-institution-reports',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <section class="space-y-5">
      <a routerLink="/coaching-portal/institution/overview" class="text-sm text-indigo-700 underline">← Kurum özeti</a>
      <h1 class="text-2xl font-bold">Kurum raporları</h1>
      <p class="text-sm text-slate-600">Yalnızca aktif kurumunuzdaki öğrencilerin koçluk verileri gösterilir. Tarih seçilmezse son 30 gün kullanılır.</p>
      <form (ngSubmit)="applyFilters()" class="flex flex-wrap items-end gap-3">
        <label class="text-sm">Başlangıç <input type="date" name="fromDate" [(ngModel)]="fromDate" class="block rounded border p-2" /></label>
        <label class="text-sm">Bitiş <input type="date" name="toDate" [(ngModel)]="toDate" class="block rounded border p-2" /></label>
        <label class="text-sm">Sınıf <select name="gradeLevel" [(ngModel)]="gradeLevel" class="block rounded border p-2">
          <option [ngValue]="undefined">Tümü</option>
          @for (grade of grades; track grade) { <option [ngValue]="grade">{{ grade }}. sınıf</option> }
        </select></label>
        <button type="submit" class="rounded bg-indigo-600 px-4 py-2 text-white">Uygula</button>
      </form>
      @if (error()) { <p role="alert" class="text-red-700">{{ error() }}</p> }
      @if (loading()) { <p role="status">Raporlar yükleniyor…</p> }
      @if (comparison(); as report) {
        <section class="space-y-3"><h2 class="text-lg font-semibold">Kurum karşılaştırması</h2>
          <div class="grid gap-3 sm:grid-cols-3">
            <p>Öğrenci: {{ report.studentCount }}</p>
            <p>Ödev teslimi: {{ report.submittedAssignmentCount }} / {{ report.assignedAssignmentCount }}</p>
            <p>Ortalama ödev başarısı: {{ percentage(report.averageAssignmentPercentage) }}</p>
            <p>Ortalama sınav başarısı: {{ percentage(report.averageExamPercentage) }}</p>
            <p>Seans katılımı: {{ percentage(report.attendancePercentage) }}</p>
            <p>Kurum hedefleri: {{ report.completedGoalCount }} / {{ report.goalCount }} tamamlandı</p>
            <p>Hedef ilerlemesi: {{ report.goalCount ? percentage(report.averageGoalProgress) : 'Veri yok' }}</p>
          </div>
        </section>
      }
      @if (warnings(); as report) {
        <section class="space-y-3"><h2 class="text-lg font-semibold">İnceleme gerektiren öğrenciler</h2>
          <p class="text-xs text-slate-600">Bu göstergeler tanı veya kesin performans hükmü değildir; ilgili kayıtlarla birlikte değerlendirilmelidir.</p>
          @for (item of report.items; track item.studentId) {
            <article class="rounded border p-3">
              <a [routerLink]="['/coaching-portal/institution/students', item.studentId]" class="font-semibold text-indigo-700 underline">{{ item.studentName || item.studentEmail || item.studentId }}</a>
              <p>Öncelik: {{ riskLabel(item.riskLevel) }} · {{ item.teacherName || 'Öğretmen atanmamış' }}</p>
              <p>Gerekçe: {{ reasonLabels(item) }}</p>
            </article>
          } @empty { <p>Bu aralıkta uyarı yok.</p> }
          <div class="flex items-center gap-3 text-sm">
            <span>Toplam {{ report.totalCount }} · {{ report.pageNumber }} / {{ report.totalPages || 1 }}</span>
            <button type="button" [disabled]="report.pageNumber <= 1 || loading()" (click)="loadWarnings(report.pageNumber - 1)">Önceki</button>
            <button type="button" [disabled]="report.pageNumber >= report.totalPages || loading()" (click)="loadWarnings(report.pageNumber + 1)">Sonraki</button>
          </div>
        </section>
      }
    </section>
  `
})
export class InstitutionReportsComponent implements OnInit {
  private readonly service = inject(CoachingAdminService);
  readonly grades = Array.from({ length: 12 }, (_, index) => index + 1);
  readonly comparison = signal<InstitutionCoachingComparison | null>(null);
  readonly warnings = signal<InstitutionEarlyWarningReport | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  private institutionId: string | null = null;
  gradeLevel?: number;
  fromDate = '';
  toDate = '';

  ngOnInit(): void {
    this.service.getReadScope().subscribe({
      next: scope => {
        if (scope.isGlobal || !scope.institutionId) {
          this.error.set('Aktif kurum kapsamı bulunamadı.');
          return;
        }
        this.institutionId = scope.institutionId;
        this.applyFilters();
      },
      error: () => this.error.set('Kurum kapsamı yüklenemedi.')
    });
  }

  applyFilters(): void {
    if (!this.institutionId) return;
    if (!!this.fromDate !== !!this.toDate) {
      this.error.set('İki tarihi birlikte girin veya ikisini de boş bırakın.');
      return;
    }
    if (this.fromDate && this.toDate && this.fromDate > this.toDate) {
      this.error.set('Başlangıç tarihi bitiş tarihinden sonra olamaz.');
      return;
    }
    if (this.fromDate && this.toDate &&
      Date.parse(`${this.toDate}T00:00:00.000Z`) + 86_400_000 - Date.parse(`${this.fromDate}T00:00:00.000Z`) > 366 * 86_400_000) {
      this.error.set('Rapor aralığı en fazla 366 gün olabilir.');
      return;
    }
    const filters = this.filters();
    this.loading.set(true);
    this.error.set(null);
    forkJoin({
      comparison: this.service.getInstitutionComparison(this.institutionId, filters),
      warnings: this.service.getInstitutionEarlyWarnings(this.institutionId, { ...filters, pageNumber: 1, pageSize: 25 })
    }).subscribe({
      next: result => {
        this.comparison.set(result.comparison);
        this.warnings.set(result.warnings);
        this.loading.set(false);
      },
      error: () => { this.error.set('Kurum raporları yüklenemedi.'); this.loading.set(false); }
    });
  }

  loadWarnings(pageNumber: number): void {
    if (!this.institutionId || pageNumber < 1) return;
    this.loading.set(true);
    this.error.set(null);
    this.service.getInstitutionEarlyWarnings(this.institutionId, { ...this.filters(), pageNumber, pageSize: 25 }).subscribe({
      next: report => { this.warnings.set(report); this.loading.set(false); },
      error: () => { this.error.set('Uyarı listesi yüklenemedi.'); this.loading.set(false); }
    });
  }

  percentage(value?: number | null): string {
    return value == null ? 'Veri yok' : `${value.toFixed(1)}%`;
  }

  riskLabel(value: StudentEarlyWarning['riskLevel']): string {
    return value === 'High' || value === 2 ? 'Yüksek' : value === 'Medium' || value === 1 ? 'Orta' : 'Düşük';
  }

  reasonLabels(item: StudentEarlyWarning): string {
    const labels: Record<string, string> = {
      low_assignment_submission: 'Düşük ödev teslimi',
      low_assignment_performance: 'Düşük ödev sonucu',
      low_attendance: 'Düşük katılım',
      low_goal_progress: 'Düşük hedef ilerlemesi',
      no_recent_activity: 'Yakın zamanda etkinlik yok'
    };
    return item.reasonCodes.map(code => labels[code] ?? code).join(' · ') || 'Gerekçe belirtilmedi';
  }

  private filters(): { gradeLevel?: number; fromDate?: string; toDate?: string } {
    return {
      ...(this.gradeLevel ? { gradeLevel: this.gradeLevel } : {}),
      ...(this.fromDate ? { fromDate: `${this.fromDate}T00:00:00.000Z` } : {}),
      ...(this.toDate ? { toDate: new Date(Date.parse(`${this.toDate}T00:00:00.000Z`) + 86_400_000).toISOString() } : {})
    };
  }
}
