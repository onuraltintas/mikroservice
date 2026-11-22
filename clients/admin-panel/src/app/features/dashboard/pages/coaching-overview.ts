import { CommonModule, isPlatformBrowser } from '@angular/common';
import { Component, inject, OnInit, PLATFORM_ID, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { finalize, forkJoin } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import {
  CoachingAdminOverview,
  CoachingAdminService,
  InstitutionCoachingComparison,
  InstitutionEarlyWarningReport,
  StudentEarlyWarning,
  TeacherCoachingOverview
} from '../../../core/services/coaching-admin.service';
import { InstitutionDto, InstitutionService } from '../../../core/services/institution.service';
import {
  CoachingPortalService,
  ExamResult,
  Goal,
  StudentAssignment,
  StudentProgressSummary
} from '../../../core/services/coaching-portal.service';
import { coachingReportCsv, collectCoachingReportPages, downloadCoachingHistoryCsv, renderCoachingReportPrint } from '../../coaching-portal/coaching-history-export';

import { CoachingLabelPipe } from '../../coaching-portal/coaching-labels';
import { AdminMeterComponent, AdminStatComponent, AdminTone } from '../../../shared/components/admin-charts';
@Component({
  selector: 'app-coaching-overview',
  standalone: true,
  imports: [CoachingLabelPipe, CommonModule, FormsModule, RouterLink, AdminStatComponent, AdminMeterComponent],
  template: `
    <section class="space-y-6">
      <div class="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 class="text-2xl font-bold text-(--ui-text)">Koçluk özeti</h1>
          <p class="text-sm text-(--ui-text-muted)">Ödevler, sınavlar, görüşmeler ve hedeflerin genel görünümü. Buradan kayıt değiştirilmez.</p>
        </div>
        <button type="button" (click)="load()" [disabled]="loading()" class="secondary rounded-full border border-(--ui-border-strong) bg-(--ui-surface) px-4 py-2 text-sm font-semibold text-(--ui-text) disabled:opacity-50">
          Yenile
        </button>
      </div>

      <nav class="flex flex-wrap gap-2" aria-label="Koçluk kişi yönetimi">
        <a routerLink="/dashboard/coaching/students" class="rounded border border-(--ui-brand-soft) px-3 py-2 text-sm text-(--ui-brand-text)">Öğrenciler</a>
        <a routerLink="/dashboard/coaching/teachers" class="rounded border border-(--ui-brand-soft) px-3 py-2 text-sm text-(--ui-brand-text)">Öğretmenler</a>
      </nav>

      @if (error()) {
        <div class="rounded-lg border border-(--ui-danger-soft) bg-(--ui-danger-soft) p-4 text-sm text-(--ui-danger)">{{ error() }}</div>
      }

      @if (loading() && !overview()) {
        <div class="rounded-xl bg-(--ui-surface) p-6 shadow-sm">Yükleniyor...</div>
      } @else if (overview(); as data) {
        <div class="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-4">
          @for (card of cards(data); track card.label) {
            <admin-stat [label]="card.label" [value]="card.value" [tone]="card.tone" />
          }
        </div>

        <!-- Lively design (21.11.2026): the three rates as filling bars, values in their accessible names. -->
        <div class="rates">
          <div class="rate"><span>Ödev teslimi</span><admin-meter [value]="data.submittedAssignmentStudents" [max]="data.totalAssignmentStudents" tone="info"
            [label]="'Ödev teslimi: ' + data.submittedAssignmentStudents + '/' + data.totalAssignmentStudents" /><strong>{{ percentage(data.submittedAssignmentStudents, data.totalAssignmentStudents) }}</strong></div>
          <div class="rate"><span>Tamamlanan hedef</span><admin-meter [value]="data.completedGoals" [max]="data.totalGoals" tone="success"
            [label]="'Tamamlanan hedef: ' + data.completedGoals + '/' + data.totalGoals" /><strong>{{ percentage(data.completedGoals, data.totalGoals) }}</strong></div>
          <div class="rate"><span>Biten ödev</span><admin-meter [value]="data.completedAssignments" [max]="data.totalAssignments" tone="honey"
            [label]="'Biten ödev: ' + data.completedAssignments + '/' + data.totalAssignments" /><strong>{{ percentage(data.completedAssignments, data.totalAssignments) }}</strong></div>
        </div>

        <div class="space-y-5 rounded-xl border border-(--ui-border) bg-(--ui-surface) p-5 shadow-sm">
          <div>
            <h2 class="font-semibold text-(--ui-text)">Kurum / sınıf karşılaştırması</h2>
            <p class="mt-1 text-sm text-(--ui-text-muted)">Yalnızca aktif bir kuruma bağlı öğrenciler sayılır.</p>
          </div>
          <div class="grid gap-4 md:grid-cols-4">
            <label class="text-sm text-(--ui-text)">Kurum adıyla ara
              <span class="mt-1 flex gap-2"><input [(ngModel)]="institutionSearch" (keyup.enter)="loadInstitutions()" maxlength="100" class="w-full rounded border px-3 py-2" placeholder="Kurum adı" /><button type="button" class="rounded border px-3 py-2" (click)="loadInstitutions()">Ara</button></span>
            </label>
            <label class="text-sm text-(--ui-text)">Kurum
              <select [(ngModel)]="selectedInstitutionId" (ngModelChange)="onInstitutionChange()" class="mt-1 w-full rounded border px-3 py-2">
                <option value="">Kurum seçin</option>
                @for (institution of institutions(); track institution.id) { <option [value]="institution.id">{{ institution.name }}</option> }
              </select>
            </label>
            <label class="text-sm text-(--ui-text)">Sınıf
              <select [(ngModel)]="selectedGradeLevel" class="mt-1 w-full rounded border px-3 py-2">
                <option [ngValue]="null">Tüm sınıflar</option>
                @for (grade of grades; track grade) { <option [ngValue]="grade">{{ grade }}. sınıf</option> }
              </select>
            </label>
            <label class="text-sm text-(--ui-text)">Başlangıç
              <input [(ngModel)]="fromDate" type="date" class="mt-1 w-full rounded border px-3 py-2" />
            </label>
            <label class="text-sm text-(--ui-text)">Bitiş
              <input [(ngModel)]="toDate" type="date" class="mt-1 w-full rounded border px-3 py-2" />
            </label>
          </div>
          <button type="button" (click)="loadComparison()" [disabled]="comparisonLoading() || !selectedInstitutionId" class="rounded-lg bg-(--ui-brand) px-4 py-2 text-sm font-medium text-(--ui-brand-contrast) disabled:opacity-50">{{ comparisonLoading() ? 'Rapor hazırlanıyor…' : 'Karşılaştırmayı getir' }}</button>
          @if (comparisonError()) { <div class="rounded-lg border border-(--ui-danger-soft) bg-(--ui-danger-soft) p-3 text-sm text-(--ui-danger)">{{ comparisonError() }}</div> }
          @if (comparison(); as report) {
            <div class="flex flex-wrap gap-2">
              <button type="button" (click)="exportComparison(report)" class="rounded border px-3 py-2 text-sm">Getirilen kurum özetini CSV indir</button>
              <button type="button" (click)="printComparison(report)" class="rounded border px-3 py-2 text-sm">Getirilen kurum özetini yazdır</button>
            </div>
            <div class="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
              <div class="rounded-lg bg-(--ui-surface-muted) p-4"><p class="text-xs text-(--ui-text-muted)">Aktif öğrenci</p><p class="mt-1 text-2xl font-bold">{{ report.studentCount }}</p></div>
              <div class="rounded-lg bg-(--ui-surface-muted) p-4"><p class="text-xs text-(--ui-text-muted)">Ödev teslim oranı</p><p class="mt-1 text-2xl font-bold">{{ percentage(report.submittedAssignmentCount, report.assignedAssignmentCount) }}</p></div>
              <div class="rounded-lg bg-(--ui-surface-muted) p-4"><p class="text-xs text-(--ui-text-muted)">Ödev ortalaması</p><p class="mt-1 text-2xl font-bold">{{ formatPercent(report.averageAssignmentPercentage) }}</p></div>
              <div class="rounded-lg bg-(--ui-surface-muted) p-4"><p class="text-xs text-(--ui-text-muted)">Sınav ortalaması</p><p class="mt-1 text-2xl font-bold">{{ formatPercent(report.averageExamPercentage) }}</p></div>
              <div class="rounded-lg bg-(--ui-surface-muted) p-4"><p class="text-xs text-(--ui-text-muted)">Katılım oranı</p><p class="mt-1 text-2xl font-bold">{{ formatPercent(report.attendancePercentage) }}</p></div>
              <div class="rounded-lg bg-(--ui-surface-muted) p-4"><p class="text-xs text-(--ui-text-muted)">Hedef ilerlemesi</p><p class="mt-1 text-2xl font-bold">%{{ report.averageGoalProgress }}</p></div>
              <div class="rounded-lg bg-(--ui-surface-muted) p-4"><p class="text-xs text-(--ui-text-muted)">Tamamlanan hedef</p><p class="mt-1 text-2xl font-bold">{{ report.completedGoalCount }}/{{ report.goalCount }}</p></div>
              <div class="rounded-lg bg-(--ui-surface-muted) p-4"><p class="text-xs text-(--ui-text-muted)">Seans</p><p class="mt-1 text-2xl font-bold">{{ report.sessionCount }}</p></div>
            </div>
          }
        </div>

        <div class="space-y-5 rounded-xl border border-(--ui-border) bg-(--ui-surface) p-5 shadow-sm">
          <div class="flex flex-wrap items-start justify-between gap-3">
            <div>
              <h2 class="font-semibold text-(--ui-text)">Erken uyarılar</h2>
              <p class="mt-1 text-sm text-(--ui-text-muted)">Kural tabanlı göstergeler; sonuçlar öğrenci etkinlikleri ve aktif Identity kapsamı ile sınırlıdır.</p>
            </div>
            <button type="button" (click)="loadEarlyWarnings()" [disabled]="earlyWarningsLoading() || !selectedInstitutionId"
              class="rounded-lg border border-(--ui-brand-soft) px-4 py-2 text-sm font-medium text-(--ui-brand-text) disabled:opacity-50">
              {{ earlyWarningsLoading() ? 'Hesaplanıyor…' : 'Erken uyarıları getir' }}
            </button>
          </div>
          @if (earlyWarningsError()) { <div class="rounded-lg border border-(--ui-danger-soft) bg-(--ui-danger-soft) p-3 text-sm text-(--ui-danger)">{{ earlyWarningsError() }}</div> }
          @if (earlyWarnings(); as warnings) {
            <div class="flex flex-wrap gap-2">
              <button type="button" (click)="exportEarlyWarnings(warnings)" [disabled]="earlyWarningsExporting()" class="rounded border px-3 py-2 text-sm disabled:opacity-50">{{ earlyWarningsExporting() ? 'Tam rapor hazırlanıyor…' : 'Filtreli tüm uyarıları CSV indir' }}</button>
              <button type="button" (click)="printEarlyWarnings(warnings)" [disabled]="earlyWarningsExporting()" class="rounded border px-3 py-2 text-sm disabled:opacity-50">Filtreli tüm uyarıları yazdır</button>
            </div>
            <div class="flex flex-wrap items-center justify-between gap-3 text-sm text-(--ui-text-muted)">
              <span>{{ warnings.totalCount }} aktif öğrenci · {{ warnings.pageNumber }}/{{ warnings.totalPages || 1 }}. sayfa</span>
              <div class="flex gap-2">
                <button type="button" (click)="loadEarlyWarnings(warnings.pageNumber - 1)" [disabled]="warnings.pageNumber <= 1 || earlyWarningsLoading()" class="rounded border px-3 py-1 disabled:opacity-50">Önceki</button>
                <button type="button" (click)="loadEarlyWarnings(warnings.pageNumber + 1)" [disabled]="warnings.pageNumber >= warnings.totalPages || earlyWarningsLoading()" class="rounded border px-3 py-1 disabled:opacity-50">Sonraki</button>
              </div>
            </div>
            <div class="overflow-x-auto">
              <table class="min-w-full divide-y divide-(--ui-border) text-sm">
                <thead class="bg-(--ui-surface-muted) text-left text-xs text-(--ui-text-muted)">
                  <tr><th class="px-4 py-3">Öğrenci</th><th class="px-4 py-3">Öğretmen</th><th class="px-4 py-3">Risk</th><th class="px-4 py-3">Puan</th><th class="px-4 py-3">Sinyaller</th><th class="px-4 py-3">Ödev</th><th class="px-4 py-3">Katılım</th><th class="px-4 py-3">Hedef</th><th class="px-4 py-3">İşlem</th></tr>
                </thead>
                <tbody class="divide-y divide-(--ui-border)">
                  @for (warning of warnings.items; track warning.studentId) {
                    <tr>
                      <td class="px-4 py-3 text-(--ui-text)"><span class="font-medium">{{ warning.studentName || 'Ad bilgisi yok' }}</span><span class="block text-xs text-(--ui-text-muted)">{{ warning.studentEmail || 'E-posta bilgisi yok' }}{{ warning.gradeLevel ? ' · ' + warning.gradeLevel + '. sınıf' : '' }}</span></td>
                      <td class="px-4 py-3">@if (warning.teacherUserId) { <button type="button" class="text-(--ui-brand-text) underline" (click)="openTeacher(warning)">{{ warning.teacherName || 'Öğretmen' }}</button> } @else { {{ warning.teacherName || 'Atanmamış' }} }</td>
                      <td class="px-4 py-3"><span class="rounded-full px-2 py-1 text-xs font-semibold" [class]="riskBadgeClass(warning)">{{ riskLabel(warning) }}</span></td>
                      <td class="px-4 py-3 font-semibold">{{ warning.riskScore }}/100</td>
                      <td class="max-w-xs px-4 py-3 text-xs text-(--ui-text-muted)">{{ reasonLabels(warning) }}</td>
                      <td class="px-4 py-3">{{ warning.submittedAssignmentCount }}/{{ warning.assignmentCount }}</td>
                      <td class="px-4 py-3">{{ formatPercent(warning.attendancePercentage) }}</td>
                      <td class="px-4 py-3">%{{ warning.averageGoalProgress }}</td>
                      <td class="px-4 py-3">@if (canViewStudentDetails()) { <button type="button" class="text-(--ui-brand-text) underline" (click)="openStudent(warning)">Ayrıntı</button> }</td>
                    </tr>
                  } @empty {
                    <tr><td colspan="9" class="px-4 py-8 text-center text-(--ui-text-muted)">Bu sayfada öğrenci bulunamadı.</td></tr>
                  }
                </tbody>
              </table>
            </div>
          }
          @if (selectedStudent(); as student) {
            <section class="rounded-lg border border-(--ui-brand-soft) p-4" aria-label="Öğrenci koçluk ayrıntısı">
              <div class="flex items-start justify-between gap-3"><div><h3 class="font-semibold">{{ student.studentName || 'Öğrenci' }}</h3><p class="text-sm text-(--ui-text-muted)">{{ student.studentEmail }} · {{ student.teacherName || 'Öğretmen atanmamış' }}</p></div><button type="button" class="rounded border px-2 py-1 text-sm" (click)="closeStudent()">Kapat</button></div>
              @if (studentDetailLoading()) { <p class="mt-3 text-sm">Ayrıntılar yükleniyor…</p> }
              @if (studentDetailError()) { <p class="mt-3 text-sm text-(--ui-danger)" role="alert">{{ studentDetailError() }}</p> }
              @if (studentProgress(); as progress) {
                <div class="mt-4 grid gap-3 sm:grid-cols-2 lg:grid-cols-4 text-sm">
                  <p>Ödev: {{ progress.submittedAssignments }}/{{ progress.totalAssignments }} teslim · {{ progress.gradedAssignments }} notlandırıldı</p>
                  <p>Ödev ortalaması: {{ formatPercent(progress.averageAssignmentPercentage) }}</p>
                  <p>Sınav: {{ progress.totalExams }} · ortalama {{ formatPercent(progress.averageExamPercentage) }}</p>
                  <p>Seans: {{ progress.totalSessions }} · katılım {{ formatPercent(progress.attendancePercentage) }}</p>
                  <p>Hedef: {{ progress.completedGoals }}/{{ progress.totalGoals }} tamamlandı · ilerleme %{{ progress.averageGoalProgress }}</p>
                </div>
                <div class="mt-4 grid gap-4 lg:grid-cols-3 text-sm">
                  <div><h4 class="font-semibold">Son ödevler</h4>@for (assignment of studentAssignments(); track assignment.id) { <p class="mt-2">{{ assignment.title }} · {{ assignment.status | coachingLabel }} · {{ assignment.score ?? 'Not yok' }}</p> } @empty { <p class="mt-2 text-(--ui-text-muted)">Ödev yok.</p> }</div>
                  <div><h4 class="font-semibold">Sınav sonuçları</h4>@for (exam of studentExams(); track exam.examId) { <p class="mt-2">{{ exam.examTitle }} · {{ exam.score ?? 'puan yok' }}/{{ exam.maxScore }}</p> } @empty { <p class="mt-2 text-(--ui-text-muted)">Sonuç yok.</p> }</div>
                  <div><h4 class="font-semibold">Hedefler</h4>@for (goal of studentGoals(); track goal.id) { <p class="mt-2">{{ goal.title }} · {{ goal.progress }}%</p> } @empty { <p class="mt-2 text-(--ui-text-muted)">Hedef yok.</p> }</div>
                </div>
              }
            </section>
          }
          @if (selectedTeacherName(); as teacherName) {
            <section class="rounded-lg border border-(--ui-brand-soft) p-4" aria-label="Öğretmen koçluk özeti">
              <div class="flex items-center justify-between"><h3 class="font-semibold">{{ teacherName }} · Koçluk özeti</h3><button type="button" class="rounded border px-2 py-1 text-sm" (click)="closeTeacher()">Kapat</button></div>
              @if (teacherOverviewLoading()) { <p class="mt-3 text-sm">Öğretmen verileri yükleniyor…</p> }
              @if (teacherOverviewError()) { <p class="mt-3 text-sm text-(--ui-danger)" role="alert">{{ teacherOverviewError() }}</p> }
              @if (teacherOverview(); as teacher) {
                <div class="mt-3 grid gap-3 sm:grid-cols-3 text-sm">
                  <p>Ödev: {{ teacher.totalAssignments }}</p>
                  <p>Teslim: {{ teacher.submittedAssignmentStudents }}/{{ teacher.totalAssignmentStudents }}</p>
                  <p>Sınav: {{ teacher.totalExams }}</p>
                  <p>Seans: {{ teacher.totalSessions }}</p>
                </div>
              }
            </section>
          }
        </div>

        <div class="rounded-xl border border-(--ui-border) bg-(--ui-surface) shadow-sm">
          <div class="border-b border-(--ui-border) px-5 py-4">
            <h2 class="font-semibold text-(--ui-text)">Son ödevler</h2>
          </div>
          <div class="overflow-x-auto">
            <table class="min-w-full divide-y divide-(--ui-border) text-sm">
              <thead class="bg-(--ui-surface-muted) text-left text-xs text-(--ui-text-muted)">
                <tr><th class="px-5 py-3">Başlık</th><th class="px-5 py-3">Durum</th><th class="px-5 py-3">Öğrenci</th><th class="px-5 py-3">Teslim</th><th class="px-5 py-3">Teslim tarihi</th></tr>
              </thead>
              <tbody class="divide-y divide-(--ui-border)">
                @for (assignment of data.recentAssignments; track assignment.id) {
                  <tr><td class="px-5 py-3 font-medium text-(--ui-text)">{{ assignment.title }}</td><td class="px-5 py-3">{{ assignment.status | coachingLabel }}</td><td class="px-5 py-3">{{ assignment.studentCount }}</td><td class="px-5 py-3">{{ assignment.submittedStudentCount }}</td><td class="px-5 py-3">{{ assignment.dueDate | date:'shortDate' }}</td></tr>
                } @empty {
                  <tr><td colspan="5" class="px-5 py-8 text-center text-(--ui-text-muted)">Kayıt bulunamadı.</td></tr>
                }
              </tbody>
            </table>
          </div>
        </div>
      }
    </section>
  `,
  styles: [`
    .rates { display: grid; gap: 12px; padding: 18px 20px; border-radius: 18px; background: var(--ds-surface); box-shadow: var(--ds-shadow-raised, var(--ds-shadow)); }
    .rate { display: grid; grid-template-columns: 10rem minmax(0, 1fr) 3.2rem; align-items: center; gap: 12px; font-size: .9rem; }
    .rate span { color: var(--ds-muted); }
    .rate strong { text-align: right; color: var(--ds-text); font-variant-numeric: tabular-nums; }
    @media (max-width: 560px) { .rate { grid-template-columns: 7.5rem minmax(0, 1fr) 2.8rem; } }
  `]
})
export class CoachingOverviewComponent implements OnInit {
  private readonly service = inject(CoachingAdminService);
  private readonly institutionService = inject(InstitutionService);
  private readonly portalService = inject(CoachingPortalService);
  private readonly authService = inject(AuthService);
  private readonly platformId = inject(PLATFORM_ID);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly overview = signal<CoachingAdminOverview | null>(null);
  readonly institutions = signal<InstitutionDto[]>([]);
  readonly comparison = signal<InstitutionCoachingComparison | null>(null);
  readonly comparisonLoading = signal(false);
  readonly comparisonError = signal<string | null>(null);
  private comparisonRequestId = 0;
  readonly earlyWarnings = signal<InstitutionEarlyWarningReport | null>(null);
  readonly earlyWarningsLoading = signal(false);
  readonly earlyWarningsExporting = signal(false);
  readonly earlyWarningsError = signal<string | null>(null);
  private earlyWarningRequestId = 0;
  readonly selectedStudent = signal<StudentEarlyWarning | null>(null);
  readonly studentProgress = signal<StudentProgressSummary | null>(null);
  readonly studentAssignments = signal<StudentAssignment[]>([]);
  readonly studentExams = signal<ExamResult[]>([]);
  readonly studentGoals = signal<Goal[]>([]);
  readonly studentDetailLoading = signal(false);
  readonly studentDetailError = signal<string | null>(null);
  private studentDetailRequestId = 0;
  readonly selectedTeacherName = signal<string | null>(null);
  readonly teacherOverview = signal<TeacherCoachingOverview | null>(null);
  readonly teacherOverviewLoading = signal(false);
  readonly teacherOverviewError = signal<string | null>(null);
  private teacherRequestId = 0;
  readonly grades = Array.from({ length: 12 }, (_, index) => index + 1);
  selectedInstitutionId = '';
  institutionSearch = '';
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
      error: () => this.error.set('Koçluk özeti yüklenemedi. Sayfayı yenileyip yeniden deneyin.')
    });
  }

  loadInstitutions() {
    this.institutionService.getAll(1, 100, this.institutionSearch.trim(), true).subscribe({
      next: response => this.institutions.set(response.items ?? []),
      error: () => this.comparisonError.set('Kurum listesi yüklenemedi. Sayfayı yenileyip yeniden deneyin.')
    });
  }

  onInstitutionChange() {
    ++this.comparisonRequestId;
    ++this.earlyWarningRequestId;
    this.comparison.set(null);
    this.earlyWarnings.set(null);
    this.comparisonLoading.set(false);
    this.earlyWarningsLoading.set(false);
    this.closeStudent();
    this.closeTeacher();
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
    const requestId = ++this.comparisonRequestId;
    this.service.getInstitutionComparison(this.selectedInstitutionId, {
      gradeLevel: this.selectedGradeLevel ?? undefined,
      fromDate,
      toDate
    }).pipe(finalize(() => { if (requestId === this.comparisonRequestId) this.comparisonLoading.set(false); })).subscribe({
      next: report => { if (requestId === this.comparisonRequestId) this.comparison.set(report); },
      error: () => { if (requestId === this.comparisonRequestId) this.comparisonError.set('Karşılaştırmalı rapor yüklenemedi. Sayfayı yenileyip yeniden deneyin.'); }
    });
  }

  exportComparison(report: InstitutionCoachingComparison): void {
    downloadCoachingHistoryCsv(coachingReportCsv(['Gösterge', 'Değer'], this.comparisonRows(report)),
      `kocluk-kurum-${report.institutionId}-ozet.csv`);
  }

  printComparison(report: InstitutionCoachingComparison): void {
    const target = window.open('', '_blank');
    if (!target) {
      this.comparisonError.set('Yazdırma penceresi açılamadı. Tarayıcınızın açılır pencere iznini kontrol edin.');
      return;
    }
    target.opener = null;
    renderCoachingReportPrint(target, 'Kurum koçluk özeti', ['Gösterge', 'Değer'], this.comparisonRows(report));
  }

  comparisonRows(report: InstitutionCoachingComparison): (string | number | null | undefined)[][] {
    return [
      ['Kurum kimliği', report.institutionId],
      ['Sınıf', report.gradeLevel ?? 'Tümü'],
      ['Başlangıç', report.fromDate],
      ['Bitiş', report.toDate],
      ['Aktif öğrenci', report.studentCount],
      ['Atanan ödev', report.assignedAssignmentCount],
      ['Teslim edilen ödev', report.submittedAssignmentCount],
      ['Değerlendirilen ödev', report.gradedAssignmentCount],
      ['Ödev ortalaması (%)', report.averageAssignmentPercentage],
      ['Sınav sonucu', report.examResultCount],
      ['Sınav ortalaması (%)', report.averageExamPercentage],
      ['Seans', report.sessionCount],
      ['Kaydedilen katılım', report.attendanceRecordedCount],
      ['Katılım oranı (%)', report.attendancePercentage],
      ['Hedef', report.goalCount],
      ['Tamamlanan hedef', report.completedGoalCount],
      ['Hedef ilerlemesi (%)', report.averageGoalProgress]
    ];
  }

  async exportEarlyWarnings(report: InstitutionEarlyWarningReport): Promise<void> {
    if (this.earlyWarningsExporting()) return;
    this.earlyWarningsExporting.set(true);
    this.earlyWarningsError.set(null);
    try {
      const students = await this.collectEarlyWarnings(report);
      downloadCoachingHistoryCsv(coachingReportCsv(this.earlyWarningColumns(), this.earlyWarningRows(report, students)),
        `kocluk-kurum-${report.institutionId}-erken-uyarilar.csv`);
    } catch {
      this.earlyWarningsError.set('Tam uyarı raporu indirilemedi. Kayıtlar değişmiş veya bağlantı kesilmiş olabilir; yeniden deneyin.');
    } finally {
      this.earlyWarningsExporting.set(false);
    }
  }

  async printEarlyWarnings(report: InstitutionEarlyWarningReport): Promise<void> {
    if (this.earlyWarningsExporting()) return;
    const target = window.open('', '_blank');
    if (!target) {
      this.earlyWarningsError.set('Yazdırma penceresi açılamadı. Tarayıcınızın açılır pencere iznini kontrol edin.');
      return;
    }
    target.opener = null;
    this.earlyWarningsExporting.set(true);
    this.earlyWarningsError.set(null);
    try {
      const students = await this.collectEarlyWarnings(report);
      renderCoachingReportPrint(target, 'Kurum koçluk erken uyarıları',
        this.earlyWarningColumns(), this.earlyWarningRows(report, students));
    } catch {
      target.close();
      this.earlyWarningsError.set('Tam uyarı raporu yazdırılamadı. Kayıtlar değişmiş veya bağlantı kesilmiş olabilir; yeniden deneyin.');
    } finally {
      this.earlyWarningsExporting.set(false);
    }
  }

  private collectEarlyWarnings(report: InstitutionEarlyWarningReport): Promise<StudentEarlyWarning[]> {
    return collectCoachingReportPages((pageNumber, pageSize) =>
      this.service.getInstitutionEarlyWarnings(report.institutionId, {
        pageNumber, pageSize, gradeLevel: report.gradeLevel,
        fromDate: report.fromDate, toDate: report.toDate
      }), student => student.studentId);
  }

  private earlyWarningColumns(): string[] {
    return ['Kurum', 'Başlangıç', 'Bitiş', 'Sınıf', 'Öğrenci', 'E-posta', 'Öğretmen', 'Risk', 'Puan', 'Sinyaller', 'Ödev', 'Katılım (%)', 'Hedef (%)'];
  }

  private earlyWarningRows(report: InstitutionEarlyWarningReport, students: StudentEarlyWarning[]): (string | number | null | undefined)[][] {
    if (students.length === 0) {
      return [[report.institutionId, report.fromDate, report.toDate, report.gradeLevel ?? 'Tümü']];
    }
    return students.map(student => [report.institutionId, report.fromDate, report.toDate,
      report.gradeLevel ?? 'Tümü', student.studentName, student.studentEmail,
      student.teacherName, student.riskLevel, student.riskScore,
      student.reasonCodes.join(', '), `${student.submittedAssignmentCount}/${student.assignmentCount}`,
      student.attendancePercentage, student.averageGoalProgress]);
  }

  loadEarlyWarnings(pageNumber = 1) {
    if (!this.selectedInstitutionId) return;
    const fromDate = this.toUtcDate(this.fromDate, false);
    const toDate = this.toUtcDate(this.toDate, true);
    if (!fromDate || !toDate || fromDate > toDate) {
      this.earlyWarningsError.set('Erken uyarı tarih aralığı geçersiz.');
      return;
    }

    this.earlyWarningsLoading.set(true);
    this.earlyWarningsError.set(null);
    const requestId = ++this.earlyWarningRequestId;
    this.service.getInstitutionEarlyWarnings(this.selectedInstitutionId, {
      pageNumber,
      pageSize: 25,
      gradeLevel: this.selectedGradeLevel ?? undefined,
      fromDate,
      toDate
    }).pipe(finalize(() => { if (requestId === this.earlyWarningRequestId) this.earlyWarningsLoading.set(false); })).subscribe({
      next: report => { if (requestId === this.earlyWarningRequestId) this.earlyWarnings.set(report); },
      error: () => { if (requestId === this.earlyWarningRequestId) this.earlyWarningsError.set('Erken uyarı raporu yüklenemedi. Sayfayı yenileyip yeniden deneyin.'); }
    });
  }

  openStudent(student: StudentEarlyWarning) {
    if (!this.canViewStudentDetails()) return;
    const requestId = ++this.studentDetailRequestId;
    this.selectedStudent.set(student);
    this.studentProgress.set(null);
    this.studentDetailError.set(null);
    this.studentDetailLoading.set(true);
    forkJoin({
      progress: this.portalService.getStudentProgress(student.studentId),
      assignments: this.portalService.getStudentAssignments(student.studentId, 1, 10),
      exams: this.portalService.getStudentExamResults(student.studentId, 1, 10),
      goals: this.portalService.getStudentGoals(student.studentId, 1, 10)
    }).subscribe({
      next: result => {
        if (requestId !== this.studentDetailRequestId) return;
        this.studentProgress.set(result.progress);
        this.studentAssignments.set(result.assignments.items);
        this.studentExams.set(result.exams.items);
        this.studentGoals.set(result.goals.items);
        this.studentDetailLoading.set(false);
      },
      error: () => {
        if (requestId !== this.studentDetailRequestId) return;
        this.studentDetailError.set('Öğrenci koçluk ayrıntısı yüklenemedi. Sayfayı yenileyip yeniden deneyin.');
        this.studentDetailLoading.set(false);
      }
    });
  }

  canViewStudentDetails() {
    return this.authService.userProfile()?.roles.includes('SystemAdmin') === true;
  }

  closeStudent() {
    ++this.studentDetailRequestId;
    this.selectedStudent.set(null);
    this.studentProgress.set(null);
    this.studentDetailLoading.set(false);
  }

  openTeacher(student: StudentEarlyWarning) {
    if (!student.teacherUserId) return;
    const requestId = ++this.teacherRequestId;
    this.selectedTeacherName.set(student.teacherName || 'Öğretmen');
    this.teacherOverview.set(null);
    this.teacherOverviewError.set(null);
    this.teacherOverviewLoading.set(true);
    this.service.getTeacherOverview(student.teacherUserId).subscribe({
      next: overview => {
        if (requestId !== this.teacherRequestId) return;
        this.teacherOverview.set(overview);
        this.teacherOverviewLoading.set(false);
      },
      error: () => {
        if (requestId !== this.teacherRequestId) return;
        this.teacherOverviewError.set('Öğretmen koçluk özeti yüklenemedi. Sayfayı yenileyip yeniden deneyin.');
        this.teacherOverviewLoading.set(false);
      }
    });
  }

  closeTeacher() {
    ++this.teacherRequestId;
    this.selectedTeacherName.set(null);
    this.teacherOverview.set(null);
    this.teacherOverviewLoading.set(false);
  }

  percentage(numerator: number, denominator: number) {
    return denominator === 0 ? '—' : `%${Math.round(numerator / denominator * 100)}`;
  }

  formatPercent(value?: number) {
    return value === undefined || value === null ? '—' : `%${value.toLocaleString('tr-TR', { maximumFractionDigits: 1 })}`;
  }

  riskLabel(warning: StudentEarlyWarning) {
    if (warning.riskLevel === 'High' || warning.riskLevel === 2) return 'Yüksek';
    if (warning.riskLevel === 'Medium' || warning.riskLevel === 1) return 'Orta';
    return 'Düşük';
  }

  riskBadgeClass(warning: StudentEarlyWarning) {
    if (warning.riskLevel === 'High' || warning.riskLevel === 2) {
      return 'bg-(--ui-danger-soft) text-(--ui-danger)';
    }
    if (warning.riskLevel === 'Medium' || warning.riskLevel === 1) {
      return 'bg-(--ui-warning-soft) text-(--ui-warning)';
    }
    return 'bg-(--ui-success-soft) text-(--ui-success)';
  }

  reasonLabels(warning: StudentEarlyWarning) {
    return warning.reasonCodes.map(reason => this.reasonLabel(reason)).join(', ') || 'Sinyal yok';
  }

  reasonLabel(reason: string) {
    const labels: Record<string, string> = {
      low_assignment_submission: 'ödev teslimi düşük',
      low_assignment_performance: 'ödev başarısı düşük',
      low_attendance: 'katılım düşük',
      low_goal_progress: 'hedef ilerlemesi düşük',
      no_recent_activity: 'son etkinlik eski'
    };
    return labels[reason] ?? reason;
  }

  private toUtcDate(value: string, endOfDay: boolean) {
    if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) return null;
    const suffix = endOfDay ? 'T23:59:59.999Z' : 'T00:00:00.000Z';
    const date = new Date(`${value}${suffix}`);
    return Number.isNaN(date.getTime()) ? null : date.toISOString();
  }

  cards(data: CoachingAdminOverview) {
    return [
      { label: 'Toplam ödev', value: data.totalAssignments, tone: 'info' as AdminTone },
      { label: 'Etkin ödev', value: data.activeAssignments, tone: 'info' as AdminTone },
      { label: 'Toplam sınav', value: data.totalExams, tone: 'violet' as AdminTone },
      { label: 'Sınav sonucu', value: data.totalExamResults, tone: 'violet' as AdminTone },
      { label: 'Toplam görüşme', value: data.totalSessions, tone: 'orange' as AdminTone },
      { label: 'Yaklaşan görüşme', value: data.upcomingSessions, tone: 'orange' as AdminTone },
      { label: 'Toplam hedef', value: data.totalGoals, tone: 'success' as AdminTone },
      { label: 'Tamamlanan hedef', value: data.completedGoals, tone: 'success' as AdminTone }
    ];
  }
}
