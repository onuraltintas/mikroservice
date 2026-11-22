import { CommonModule } from '@angular/common';
import { Component, DestroyRef, EventEmitter, Input, OnChanges, Output, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Subject, finalize, forkJoin, takeUntil } from 'rxjs';
import { CoachingAdminService, InstitutionCoachingComparison, InstitutionEarlyWarningReport, StudentEarlyWarning } from '../../../../../../src/app/core/services/coaching-admin.service';
import { CoachingInstitutionStudent } from './coaching-institution.service';
import { StaffMeterComponent } from '../../shared/staff-charts';

@Component({
  selector: 'staff-coaching-institution-analytics', standalone: true, imports: [CommonModule, FormsModule, StaffMeterComponent],
  template: `
    <section aria-labelledby="institution-analytics-title">
      <h3 id="institution-analytics-title">Kurum analizi ve erken uyarılar</h3>
      <p>Seçilen dönemde kurumunuzun öğrencilerine ait ödev, sınav, seans ve hedef kayıtları. Uyarılar koçun incelemesine yardımcı olan sinyallerdir.</p>
      <form (ngSubmit)="load()" class="filters">
        <label>Başlangıç tarihi<input type="date" [(ngModel)]="fromDate" (ngModelChange)="filtersChanged()" name="fromDate" required></label>
        <label>Bitiş tarihi<input type="date" [(ngModel)]="toDate" (ngModelChange)="filtersChanged()" name="toDate" required></label>
        <label>Sınıf<select [(ngModel)]="gradeLevel" (ngModelChange)="filtersChanged()" name="grade"><option [ngValue]="null">Tüm sınıflar</option>@for (grade of grades; track grade) { <option [ngValue]="grade">{{ grade }}. sınıf</option> }</select></label>
        <button type="submit" [disabled]="loading()">{{ loading() ? 'Yükleniyor…' : 'Raporu göster' }}</button>
      </form>
      @if (error()) { <p role="alert">{{ error() }}</p> }
      @if (comparison(); as data) {
        <p role="status">Uygulanan dönem: {{ data.fromDate | date:'dd.MM.yyyy' }} – {{ data.toDate | date:'dd.MM.yyyy' }}, {{ data.gradeLevel ? data.gradeLevel + '. sınıf' : 'Tüm sınıflar' }}</p>
        <dl class="metrics">
          <div class="tone-info"><dt>Öğrenci</dt><dd>{{ data.studentCount }}</dd></div>
          <div class="tone-success"><dt>Ödev teslimi</dt><dd>{{ data.submittedAssignmentCount }} / {{ data.assignedAssignmentCount }}</dd><staff-meter tone="success" [value]="data.submittedAssignmentCount" [max]="data.assignedAssignmentCount" [label]="'Ödev teslimi: ' + data.submittedAssignmentCount + ' / ' + data.assignedAssignmentCount" /></div>
          <div class="tone-violet"><dt>Sınav ortalaması</dt><dd>{{ data.averageExamPercentage == null ? 'Veri yok' : '%' + data.averageExamPercentage }}</dd>@if (data.averageExamPercentage != null) { <staff-meter tone="violet" [value]="data.averageExamPercentage" [max]="100" [label]="'Sınav ortalaması: %' + data.averageExamPercentage" /> }</div>
          <div class="tone-orange"><dt>Seans katılımı</dt><dd>{{ data.attendancePercentage == null ? 'Veri yok' : '%' + data.attendancePercentage }}</dd>@if (data.attendancePercentage != null) { <staff-meter tone="orange" [value]="data.attendancePercentage" [max]="100" [label]="'Seans katılımı: %' + data.attendancePercentage" /> }</div>
          <div class="tone-accent"><dt>Tamamlanan hedef</dt><dd>{{ data.completedGoalCount }} / {{ data.goalCount }}</dd><staff-meter tone="accent" [value]="data.completedGoalCount" [max]="data.goalCount" [label]="'Tamamlanan hedef: ' + data.completedGoalCount + ' / ' + data.goalCount" /></div>
        </dl>
      }
      @if (warnings(); as report) {
        <h4>Öğrenci uyarıları ({{ report.totalCount }} kayıt)</h4>
        @if (!report.items.length) { <p>Bu filtrelerde uyarı bulunmuyor.</p> }
        @else {
          <div class="table-scroll"><table><thead><tr><th>Öğrenci</th><th>Öğretmen</th><th>Risk</th><th>Sinyaller</th><th>İşlem</th></tr></thead>
            <tbody>@for (warning of report.items; track warning.studentId) {
              <tr><td>{{ warning.studentName || 'Öğrenci adı bulunamadı' }}</td><td>{{ warning.teacherName || 'Atanmamış' }}</td>
                <td><span class="risk" [class.high]="riskLabel(warning) === 'Yüksek'" [class.medium]="riskLabel(warning) === 'Orta'">{{ riskLabel(warning) }}</span> <span class="score">{{ warning.riskScore }}</span></td><td>{{ reasons(warning) }}</td>
                <td><button type="button" (click)="openStudent(warning)">Öğrenci raporu</button></td></tr>
            }</tbody></table></div>
        }
        <div class="pagination"><button type="button" (click)="load(report.pageNumber - 1)" [disabled]="loading() || report.pageNumber <= 1">Önceki</button>
          <span>{{ report.pageNumber }} / {{ report.totalPages || 1 }}</span>
          <button type="button" (click)="load(report.pageNumber + 1)" [disabled]="loading() || report.pageNumber >= report.totalPages">Sonraki</button></div>
      }
    </section>`,
  styles: [`
    :host { display: block; } section { padding: 1.25rem; border: 1px solid var(--ui-border); border-radius: 1rem; background: var(--ui-surface); box-shadow: var(--ds-shadow); }
    h3 { margin: 0; font-size: 1.25rem; } h4 { margin: 1.4rem 0 .6rem; font-size: 1rem; }
    p { color: var(--ui-text-muted); font-size: .875rem; line-height: 1.6; } .filters { display: flex; flex-wrap: wrap; gap: .75rem; align-items: end; margin: 1.25rem 0; }
    label { display: grid; gap: .4rem; font-size: .8125rem; font-weight: 700; color: var(--ui-text-muted); }
    input, select, button { min-height: 2.75rem; padding: .55rem .85rem; border: 1px solid var(--ui-border); border-radius: .65rem; background: var(--ui-surface); color: var(--ui-text); font: inherit; }
    button { cursor: pointer; font-weight: 700; } form button[type=submit] { border-color: transparent; background: var(--ds-primary); color: var(--ds-on-primary); }
    button:disabled { opacity: .5; cursor: default; }
    .metrics { display: grid; grid-template-columns: repeat(auto-fit, minmax(9rem, 1fr)); gap: .75rem; margin: 0; }
    .metrics div { display: grid; align-content: start; gap: .45rem; padding: 1rem; border-radius: .9rem; background: var(--tone-soft); --tone: var(--ds-info); --tone-soft: var(--ds-info-soft); --meter-track: var(--ui-surface);
      animation: staff-rise .5s cubic-bezier(.2, .8, .2, 1) both; }
    .metrics div:nth-child(2) { animation-delay: 60ms; } .metrics div:nth-child(3) { animation-delay: 120ms; } .metrics div:nth-child(4) { animation-delay: 180ms; } .metrics div:nth-child(5) { animation-delay: 240ms; }
    .metrics .tone-success { --tone: var(--ds-success); --tone-soft: var(--ds-success-soft); } .metrics .tone-violet { --tone: var(--ds-violet); --tone-soft: var(--ds-violet-soft); }
    .metrics .tone-orange { --tone: var(--ds-orange); --tone-soft: var(--ds-orange-soft); } .metrics .tone-accent { --tone: var(--ds-warning); --tone-soft: var(--ds-warning-soft); }
    dt { font-size: .8125rem; font-weight: 700; color: var(--ui-text); } dd { margin: 0; color: var(--tone); font-size: 1.6rem; font-weight: 750; font-variant-numeric: tabular-nums; }
    .table-scroll { overflow-x: auto; } table { width: 100%; border-collapse: collapse; font-size: .875rem; text-align: left; } th, td { padding: .75rem; border-bottom: 1px solid var(--ui-border); }
    th { color: var(--ui-text-muted); font-size: .8125rem; }
    .risk { display: inline-flex; padding: .15rem .55rem; border-radius: 999px; background: var(--ds-success-soft); color: var(--ds-success); font-weight: 750; }
    .risk.medium { background: var(--ds-orange-soft); color: var(--ds-orange); } .risk.high { background: var(--ds-danger-soft); color: var(--ds-danger); }
    .score { color: var(--ui-text-muted); font-variant-numeric: tabular-nums; }
    .pagination { display: flex; justify-content: space-between; align-items: center; gap: .75rem; margin-top: 1rem; } [role=alert] { color: var(--ui-danger); }
    @keyframes staff-rise { from { opacity: 0; transform: translateY(10px); } }
    @media (prefers-reduced-motion: reduce) { .metrics div { animation: none; } }
  `]
})
export class CoachingInstitutionAnalyticsComponent implements OnChanges {
  @Input({ required: true }) institutionId = '';
  @Output() readonly studentSelected = new EventEmitter<CoachingInstitutionStudent>();
  private readonly service = inject(CoachingAdminService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly changed = new Subject<void>();
  readonly loading = signal(false);
  readonly error = signal('');
  readonly comparison = signal<InstitutionCoachingComparison | null>(null);
  readonly warnings = signal<InstitutionEarlyWarningReport | null>(null);
  // Live test 13.11.2026: the product starts at grade 5; grades 1–4 were listed.
  readonly grades = Array.from({ length: 8 }, (_, index) => index + 5);
  fromDate = this.localDate(new Date(Date.now() - 29 * 86_400_000));
  toDate = this.localDate(new Date());
  gradeLevel: number | null = null;

  ngOnChanges(): void { this.load(); }
  filtersChanged(): void {
    this.changed.next(); this.comparison.set(null); this.warnings.set(null); this.error.set(''); this.loading.set(false);
  }
  load(pageNumber = 1): void {
    this.changed.next(); this.comparison.set(null); this.warnings.set(null); this.error.set('');
    if (!this.institutionId) return;
    const from = new Date(`${this.fromDate}T00:00:00`);
    const end = new Date(`${this.toDate}T00:00:00`); end.setDate(end.getDate() + 1);
    const to = new Date(end.getTime() - 1);
    if (Number.isNaN(from.getTime()) || Number.isNaN(to.getTime()) || from > to || to.getTime() - from.getTime() > 366 * 86_400_000
      || (this.gradeLevel !== null && (!Number.isInteger(this.gradeLevel) || this.gradeLevel < 1 || this.gradeLevel > 12))) {
      this.error.set('Geçerli bir sınıf ve en fazla 366 günlük tarih aralığı seçin.'); return;
    }
    // K2: plain dates; the server reads them as whole Europe/Istanbul days whatever the browser's time zone is.
    const filters = { fromDate: this.fromDate, toDate: this.toDate, ...(this.gradeLevel !== null ? { gradeLevel: this.gradeLevel } : {}) };
    this.loading.set(true);
    forkJoin({ comparison: this.service.getInstitutionComparison(this.institutionId, filters),
      warnings: this.service.getInstitutionEarlyWarnings(this.institutionId, { ...filters, pageNumber, pageSize: 25 }) })
      .pipe(takeUntil(this.changed), takeUntilDestroyed(this.destroyRef), finalize(() => this.loading.set(false))).subscribe({
        next: data => { this.comparison.set(data.comparison); this.warnings.set(data.warnings); },
        error: err => this.error.set(err.status === 403 ? 'Bu kurum için yetkiniz doğrulanamadı. Oturumunuzu yenileyin.' : 'Kurum raporu yüklenemedi. Yeniden deneyebilirsiniz.')
      });
  }
  openStudent(warning: StudentEarlyWarning): void {
    if (!this.warnings()?.items.some(item => item.studentId === warning.studentId)) return;
    this.studentSelected.emit({ userId: warning.studentId, firstName: warning.studentName || 'Öğrenci', lastName: '',
      email: warning.studentEmail || '', gradeLevel: warning.gradeLevel, teacherName: warning.teacherName, teacherUserId: warning.teacherUserId });
  }
  riskLabel(warning: StudentEarlyWarning): string {
    return warning.riskLevel === 'High' || warning.riskLevel === 2 ? 'Yüksek' : warning.riskLevel === 'Medium' || warning.riskLevel === 1 ? 'Orta' : 'Düşük';
  }
  reasons(warning: StudentEarlyWarning): string {
    const labels: Record<string, string> = { low_assignment_submission: 'Ödev teslimi düşük', low_assignment_performance: 'Ödev başarısı düşük',
      low_attendance: 'Katılım düşük', low_goal_progress: 'Hedef ilerlemesi düşük', no_recent_activity: 'Son etkinlik eski' };
    return warning.reasonCodes.map(reason => labels[reason] ?? reason).join(', ') || 'Sinyal yok';
  }
  private localDate(date: Date): string { return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`; }
}
