import { Component, OnInit, OnDestroy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import { Subject, forkJoin, of } from 'rxjs';
import { takeUntil, catchError, finalize } from 'rxjs/operators';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTableModule } from '@angular/material/table';
import { ReportsService } from '../../../core/services/reports.service';
import { TeacherClassOverviewReport } from '../../../core/models/report.model';
import { StudentsService } from '../../../core/services/students.service';
import { InstitutionsService } from '../../../core/services/institutions.service';
import { AuthService } from '../../../core/services/auth.service';
import { AdminContextService } from '../../../core/services/admin-context.service';
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
  private studentsService  = inject(StudentsService);
  private institutionsService = inject(InstitutionsService);
  private authService      = inject(AuthService);
  private adminContext     = inject(AdminContextService);
  private router           = inject(Router);
  private destroy$         = new Subject<void>();

  overview: TeacherClassOverviewReport | null = null;
  students: Student[] = [];
  loading = true;
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
    if (this.adminContext.isImpersonatingTeacher()) {
      return this.adminContext.teacherId() ?? '';
    }
    return this.authService.currentUserValue?.id ?? '';
  }

  loadInstitutionCode(): void {
    const instId = this.adminContext.isImpersonatingInstitution()
      ? this.adminContext.institutionId()
      : (this.authService.currentUserValue as any)?.institutionId;

    if (instId) {
      this.institutionsService.getInstitutionById(instId)
        .pipe(takeUntil(this.destroy$))
        .subscribe({ next: (inst) => this.institutionCode = inst.code || null });
    }
  }

  loadDashboard(): void {
    this.loading = true;
    const tid = this.teacherId;
    if (!tid) { this.loading = false; return; }

    const endDate   = new Date();
    const startDate = new Date(endDate.getTime() - 90 * 24 * 60 * 60 * 1000);

    forkJoin({
      overview: this.reportsService.getTeacherClassOverviewReport(tid, startDate, endDate)
        .pipe(catchError(() => of(null))),
      students: this.studentsService.getStudents(undefined, undefined, undefined, undefined, tid)
        .pipe(catchError(() => of([]))),
    }).pipe(takeUntil(this.destroy$), finalize(() => this.loading = false))
      .subscribe(({ overview, students }) => {
        this.overview = overview;
        this.students = students ?? [];
      });
  }

  viewStudentDetails(studentId: string): void {
    this.router.navigate(['/teacher/students', studentId]);
  }

  getPerformanceLevel(kdp: number): string {
    if (kdp >= 300) return 'Mükemmel';
    if (kdp >= 200) return 'İyi';
    if (kdp >= 100) return 'Ortalama';
    return 'Geliştirilmeli';
  }

  getPerformanceLevelColor(level: string): string {
    switch (level) {
      case 'Mükemmel': return 'success';
      case 'İyi':      return 'primary';
      case 'Ortalama': return 'warning';
      default:         return 'danger';
    }
  }

  formatDate(dateString: string): string {
    return new Date(dateString).toLocaleDateString('tr-TR');
  }

  getDistributionPct(count: number): number {
    const total = this.overview?.totalStudents || 0;
    return total ? Math.round((count / total) * 100) : 0;
  }
}
