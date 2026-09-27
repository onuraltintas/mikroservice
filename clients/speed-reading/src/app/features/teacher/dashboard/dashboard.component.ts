import { Component, OnInit, OnDestroy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import { Subject, forkJoin } from 'rxjs';
import { takeUntil, finalize } from 'rxjs/operators';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTableModule } from '@angular/material/table';
import { ReportsService } from '../../../core/services/reports.service';
import { TeacherReportService } from '../../../core/services/teacher-report.service';
import { TeacherClassOverviewReport, TeacherTimeBasedProgressReport } from '../../../core/models/report.model';
import { TeachersService } from '../../../core/services/teachers.service';
import { StudentsService } from '../../../core/services/students.service';
import { InstitutionsService } from '../../../core/services/institutions.service';
import { AuthService } from '../../../core/services/auth.service';
import { Student } from '../../../core/models/student.model';

@Component({
  selector: 'app-teacher-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    MatProgressBarModule,
    MatProgressSpinnerModule,
    MatTableModule
  ],
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.scss']
})
export class DashboardComponent implements OnInit, OnDestroy {
  private reportsService   = inject(ReportsService);
  private teacherReportService = inject(TeacherReportService);
  private teachersService  = inject(TeachersService);
  private studentsService  = inject(StudentsService);
  private institutionsService = inject(InstitutionsService);
  private authService      = inject(AuthService);
  private router           = inject(Router);
  private destroy$         = new Subject<void>();

  overview: TeacherClassOverviewReport | null = null;
  progress: TeacherTimeBasedProgressReport | null = null;
  students: Student[] = [];
  totalStudentCount = 0;
  loading = true;
  errorMessage: string | null = null;
  institutionCode: string | null = null;

  displayedColumns: string[] = ['studentName', 'exercises', 'kdp', 'comprehension', 'performance', 'actions'];

  ngOnInit(): void {
    this.loadDashboard();
    this.loadInstitutionCode();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private get teacherId(): string {
    return this.authService.currentUserValue?.id ?? '';
  }

  loadInstitutionCode(): void {
    const instId = (this.authService.currentUserValue as any)?.institutionId;

    if (instId) {
      this.institutionsService.getInstitutionById(instId)
        .pipe(takeUntil(this.destroy$))
        .subscribe({ next: (inst) => this.institutionCode = inst.code || null });
    }
  }

  loadDashboard(): void {
    this.loading = true;
    this.errorMessage = null;
    const tid = this.teacherId;
    const institutionViewer = this.isInstitutionViewer();
    const institutionId = this.authService.currentUserValue?.institutionId;
    if (!institutionViewer && !tid) { this.loading = false; return; }
    if (institutionViewer && !institutionId) {
      this.errorMessage = 'Kurum bilgisi alınamadı. Lütfen yeniden giriş yapın.';
      this.loading = false;
      return;
    }

    const endDate   = new Date();
    const startDate = new Date(endDate.getTime() - 90 * 24 * 60 * 60 * 1000);

    forkJoin({
      overview: institutionViewer
        ? this.reportsService.getInstitutionClassOverviewReport(institutionId!, startDate, endDate)
        : this.reportsService.getTeacherClassOverviewReport(tid, startDate, endDate),
      progress: institutionViewer
        ? this.teacherReportService.getInstitutionTimeBasedProgressReport(institutionId!, startDate, endDate)
        : this.reportsService.getTeacherTimeBasedProgressReport(tid, startDate, endDate),
      students: institutionViewer
        ? this.studentsService.getInstitutionStudentsPage(1, 10)
        : this.teachersService.getMyStudentsPage(1, 10),
    }).pipe(takeUntil(this.destroy$), finalize(() => this.loading = false))
      .subscribe(({ overview, progress, students }) => {
        this.overview = overview;
        this.progress = progress;
        this.students = students.items;
        this.totalStudentCount = students.totalCount;
      }, () => {
        this.errorMessage = 'Panel verileri yüklenemedi. Lütfen tekrar deneyin.';
      });
  }

  private isInstitutionViewer(): boolean {
    return this.authService.hasRole('InstitutionAdmin') || this.authService.hasRole('InstitutionOwner');
  }

  viewStudentDetails(studentId: string): void {
    this.router.navigate(['/teacher/students', studentId]);
  }

  getSchoolGradeLabel(gradeLevel?: number | null): string {
    return gradeLevel && gradeLevel >= 1 && gradeLevel <= 12
      ? `${gradeLevel}. sınıf`
      : 'Sınıf belirtilmedi';
  }

  formatDate(dateString: string): string {
    return new Date(dateString).toLocaleDateString('tr-TR');
  }

  getDistributionPct(count: number): number {
    const total = (this.overview?.studentsAboveAverage ?? 0)
      + (this.overview?.studentsAtAverage ?? 0)
      + (this.overview?.studentsBelowAverage ?? 0);
    return total ? Math.round((count / total) * 100) : 0;
  }
}
