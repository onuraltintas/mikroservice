import { CommonModule } from '@angular/common';
import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { FormsModule } from '@angular/forms';
import { Subscription, finalize } from 'rxjs';
import { AuthService } from '../../../core/services/auth.service';
import { ReportsService } from '../../../core/services/reports.service';
import { TeacherAssignmentReport } from '../../../core/models/report.model';

@Component({
  selector: 'app-teacher-assignment-report',
  standalone: true,
  imports: [CommonModule, FormsModule, MatButtonModule, MatCardModule, MatIconModule, MatProgressSpinnerModule],
  templateUrl: './teacher-assignment-report.component.html',
  styleUrls: ['./teacher-assignment-report.component.scss']
})
export class TeacherAssignmentReportComponent implements OnInit {
  private readonly reportsService = inject(ReportsService);
  private readonly authService = inject(AuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);
  private reportSubscription?: Subscription;
  private routeParams: Record<string, string> = {};

  readonly report = signal<TeacherAssignmentReport | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly startDate = signal(this.toDateInputValue(new Date(Date.now() - 30 * 24 * 60 * 60 * 1000)));
  readonly endDate = signal(this.toDateInputValue(new Date()));
  readonly maxDate = this.toDateInputValue(new Date());

  ngOnInit(): void {
    this.route.queryParams
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(params => {
        this.routeParams = params;
        this.loadReport();
      });
  }

  loadReport(): void {
    const start = this.parseDate(this.startDate(), false);
    const end = this.parseDate(this.endDate(), true);
    if (!start || !end || start > end) {
      this.error.set('Başlangıç ve bitiş tarihlerini kontrol edin.');
      this.report.set(null);
      return;
    }
    if (end.getTime() - start.getTime() > 366 * 24 * 60 * 60 * 1000) {
      this.error.set('Rapor tarih aralığı en fazla 366 gün olabilir.');
      this.report.set(null);
      return;
    }

    const user = this.authService.currentUserValue;
    const isInstitutionManager = this.authService.hasRole('InstitutionAdmin')
      || this.authService.hasRole('InstitutionOwner');
    const selectedTeacherId = this.routeParams['teacherId'];
    let request;

    if (isInstitutionManager && this.routeParams['mode'] === 'teacher') {
      if (!selectedTeacherId) {
        this.error.set('Öğretmen raporunu görmek için bir öğretmen seçin.');
        this.report.set(null);
        return;
      }
      request = this.reportsService.getAdminTeacherAssignmentReport(selectedTeacherId, start, end);
    } else if (isInstitutionManager) {
      if (!user?.institutionId) {
        this.error.set('Kurum bilgisi bulunamadı. Lütfen yeniden giriş yapın.');
        this.report.set(null);
        return;
      }
      request = this.reportsService.getInstitutionAssignmentReport(user.institutionId, start, end);
    } else {
      if (!user?.id) {
        this.error.set('Öğretmen bilgisi bulunamadı. Lütfen yeniden giriş yapın.');
        this.report.set(null);
        return;
      }
      request = this.reportsService.getTeacherAssignmentReport(user.id, start, end);
    }

    this.reportSubscription?.unsubscribe();
    this.loading.set(true);
    this.error.set(null);
    this.reportSubscription = request
      .pipe(finalize(() => this.loading.set(false)), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: report => this.report.set(report),
        error: () => {
          this.report.set(null);
          this.error.set('Ödev raporu yüklenemedi. Lütfen yeniden deneyin.');
        }
      });
  }

  statusLabel(status: string): string {
    switch (status) {
      case 'completed': return 'Tamamlandı';
      case 'in-progress': return 'Devam ediyor';
      default: return 'Başlamadı';
    }
  }

  distributionWidth(value: number): number {
    const buckets = this.report()?.scoreDistribution.data ?? [];
    const max = Math.max(0, ...buckets.map(bucket => bucket.series[0]?.value ?? 0));
    return max === 0 ? 0 : (value / max) * 100;
  }

  private parseDate(value: string, endOfDay: boolean): Date | null {
    if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) return null;
    const date = new Date(`${value}T${endOfDay ? '23:59:59.999' : '00:00:00.000'}Z`);
    return Number.isNaN(date.getTime()) ? null : date;
  }

  private toDateInputValue(date: Date): string {
    return date.toISOString().slice(0, 10);
  }
}
