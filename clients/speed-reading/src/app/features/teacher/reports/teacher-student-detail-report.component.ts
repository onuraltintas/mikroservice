import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatTabsModule } from '@angular/material/tabs';
import { MatListModule } from '@angular/material/list';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatIconModule } from '@angular/material/icon';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatInputModule } from '@angular/material/input';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { ReportsService } from '../../../core/services/reports.service';
import { TeacherStudentDetailReport } from '../../../core/models/report.model';
import { RadarChartComponent } from '../../../shared/components/charts/radar-chart.component';
import { AuthService } from '../../../core/services/auth.service';
import { TeachersService } from '../../../core/services/teachers.service';
import { StudentsService } from '../../../core/services/students.service';
import { catchError, debounceTime, finalize, map, startWith, switchMap, takeUntil, tap } from 'rxjs/operators';
import { Observable, of, Subject } from 'rxjs';

type DateRangePreset = '7days' | '30days' | '90days' | 'thisMonth' | 'thisSemester' | 'custom';

interface StudentOption {
  id: string;
  name: string;
}

@Component({
  selector: 'app-teacher-student-detail-report',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatSelectModule,
    MatFormFieldModule,
    MatTabsModule,
    MatListModule,
    MatProgressSpinnerModule,
    ReactiveFormsModule,
    RadarChartComponent,
    MatIconModule,
    MatAutocompleteModule,
    MatInputModule,
    MatDatepickerModule,
    MatNativeDateModule,
    MatButtonModule,
    MatTooltipModule
  ],
  templateUrl: './teacher-student-detail-report.component.html',
  styleUrls: ['./teacher-student-detail-report.component.scss']
})
export class TeacherStudentDetailReportComponent implements OnInit, OnDestroy {
  private reportsService = inject(ReportsService);
  private authService = inject(AuthService);
  private teachersService = inject(TeachersService);
  private studentsService = inject(StudentsService);
  private route = inject(ActivatedRoute);
  private readonly destroy$ = new Subject<void>();
  private activeTeacherId: string | null = null;
  private routeInitialized = false;

  report = signal<TeacherStudentDetailReport | null>(null);
  loading = signal(false);
  loadingStudents = signal(false);
  privacyRestricted = signal(false);
  reportError = signal<string | null>(null);

  // Student autocomplete
  studentSearchControl = new FormControl<string | StudentOption>('');
  selectedStudentId = signal<string | null>(null);
  students: StudentOption[] = [];
  filteredStudents$!: Observable<StudentOption[]>;

  // Date range
  selectedDateRange = signal<DateRangePreset>('30days');
  customStartDate = signal<Date | null>(null);
  customEndDate = signal<Date | null>(null);
  showCustomDatePicker = signal(false);
  maxDate = new Date();

  dateRangeOptions = [
    { value: '7days' as DateRangePreset, label: 'Son 7 Gün', icon: 'today' },
    { value: '30days' as DateRangePreset, label: 'Son 30 Gün', icon: 'date_range' },
    { value: '90days' as DateRangePreset, label: 'Son 90 Gün', icon: 'calendar_month' },
    { value: 'thisMonth' as DateRangePreset, label: 'Bu Ay', icon: 'event' },
    { value: 'thisSemester' as DateRangePreset, label: 'Bu Dönem', icon: 'school' },
    { value: 'custom' as DateRangePreset, label: 'Özel Tarih', icon: 'edit_calendar' }
  ];

  ngOnInit(): void {
    // Setup autocomplete filter
    this.filteredStudents$ = this.studentSearchControl.valueChanges.pipe(
      startWith(''),
      debounceTime(250),
      switchMap(value => typeof value === 'string'
        ? this.searchStudents(value)
        : of(value ? [value] : [])),
      takeUntil(this.destroy$)
    );

    const initialStudentId = this.route.snapshot.paramMap.get('studentId')
      ?? this.route.snapshot.queryParamMap.get('studentId');
    if (initialStudentId) this.selectedStudentId.set(initialStudentId);

    this.route.queryParams
      .pipe(takeUntil(this.destroy$))
      .subscribe(params => {
        const teacherId = params['teacherId'] ?? null;
        const studentId = params['studentId'] ?? initialStudentId;
        const teacherChanged = !this.routeInitialized || this.activeTeacherId !== teacherId;
        this.activeTeacherId = teacherId;
        this.routeInitialized = true;

        if (studentId && this.selectedStudentId() !== studentId) {
          this.selectedStudentId.set(studentId);
        }

        if (teacherChanged) {
          if (this.selectedStudentId()) {
            this.loadSelectedStudentOption();
          } else {
            this.studentSearchControl.setValue('', { emitEvent: true });
          }
          if (this.selectedStudentId()) this.loadReport();
        } else if (studentId && this.report() === null) {
          this.loadReport();
        }
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  displayStudent(student: StudentOption): string {
    return student ? student.name : '';
  }

  onStudentSelected(student: StudentOption): void {
    this.selectedStudentId.set(student.id);
    this.loadReport();
  }

  onInputFocus(): void {
    // Trigger filter to show all students when input is focused
    // If current value is empty string, emit to trigger update
    const currentValue = this.studentSearchControl.value;
    if (typeof currentValue === 'string' && currentValue === '') {
      this.studentSearchControl.setValue('');
    }
  }

  private searchStudents(searchTerm: string): Observable<StudentOption[]> {
    const term = searchTerm.trim();
    const selectedTeacherId = this.activeTeacherId ?? undefined;
    const page$ = this.isInstitutionViewer()
      ? this.studentsService.getInstitutionStudentsPage(1, 25, term || undefined, undefined, undefined, selectedTeacherId)
      : this.teachersService.getMyStudentsPage(1, 25, term || undefined);

    this.loadingStudents.set(true);
    return page$.pipe(
      map(page => page.items.map(student => ({
        id: student.id,
        name: `${student.firstName ?? ''} ${student.lastName ?? ''}`.trim()
      }))),
      tap(options => this.students = options),
      catchError(err => {
        console.error('Error loading students:', err);
        this.students = [];
        return of([]);
      }),
      finalize(() => this.loadingStudents.set(false))
    );
  }

  private loadSelectedStudentOption(): void {
    const studentId = this.selectedStudentId();
    if (!studentId) return;
    const selectedTeacherId = this.activeTeacherId ?? undefined;
    const student$ = this.isInstitutionViewer()
      ? this.studentsService.getInstitutionStudentById(studentId, selectedTeacherId)
      : this.teachersService.getMyStudentById(studentId);

    this.loadingStudents.set(true);
    student$.pipe(takeUntil(this.destroy$), finalize(() => this.loadingStudents.set(false))).subscribe({
      next: student => {
        this.students = student ? [{ id: student.id, name: `${student.firstName} ${student.lastName}`.trim() }] : [];
        if (this.students.length > 0) this.studentSearchControl.setValue(this.students[0], { emitEvent: true });
      },
      error: err => console.error('Error loading selected student:', err)
    });
  }

  private isInstitutionViewer(): boolean {
    return this.authService.hasRole('InstitutionAdmin') || this.authService.hasRole('InstitutionOwner');
  }

  onDateRangeChange(): void {
    if (this.selectedDateRange() === 'custom') {
      this.showCustomDatePicker.set(true);
      if (!this.customStartDate() || !this.customEndDate()) {
        return;
      }
    } else {
      this.showCustomDatePicker.set(false);
    }
    if (this.selectedStudentId()) {
      this.loadReport();
    }
  }

  onCustomDateChange(): void {
    if (this.customStartDate() && this.customEndDate() && this.selectedStudentId()) {
      this.loadReport();
    }
  }

  private getDateRange(): { startDate: Date; endDate: Date } {
    const endDate = new Date();
    const startDate = new Date();

    switch (this.selectedDateRange()) {
      case '7days':
        startDate.setDate(endDate.getDate() - 7);
        break;
      case '30days':
        startDate.setDate(endDate.getDate() - 30);
        break;
      case '90days':
        startDate.setDate(endDate.getDate() - 90);
        break;
      case 'thisMonth':
        startDate.setDate(1);
        break;
      case 'thisSemester':
        const month = endDate.getMonth();
        if (month >= 8) {
          startDate.setMonth(8, 1);
        } else if (month >= 1) {
          startDate.setMonth(1, 1);
        } else {
          startDate.setFullYear(endDate.getFullYear() - 1);
          startDate.setMonth(8, 1);
        }
        break;
      case 'custom':
        if (this.customStartDate() && this.customEndDate()) {
          return {
            startDate: this.customStartDate()!,
            endDate: this.customEndDate()!
          };
        }
        startDate.setDate(endDate.getDate() - 30);
        break;
      default:
        startDate.setDate(endDate.getDate() - 30);
    }

    return { startDate, endDate };
  }

  loadReport(): void {
    const studentId = this.selectedStudentId();
    if (!studentId) return;

    const selectedTeacherId = this.activeTeacherId;
    const teacherId = selectedTeacherId
      || (this.isInstitutionViewer() ? '' : this.authService.currentUserValue?.id ?? '');

    if (!teacherId && !this.isInstitutionViewer()) {
      console.error('User ID not found in auth service');
      return;
    }

    this.loading.set(true);
    this.privacyRestricted.set(false);
    this.reportError.set(null);
    const { startDate, endDate } = this.getDateRange();
    const institutionWide = this.isInstitutionViewer() && !selectedTeacherId;
    const institutionId = this.authService.currentUserValue?.institutionId;
    if (institutionWide && !institutionId) {
      this.reportError.set('Kurum bilgisi bulunamadı. Lütfen yeniden giriş yapın.');
      this.loading.set(false);
      return;
    }

    const reportRequest = institutionWide
      ? this.reportsService.getInstitutionStudentDetailReport(institutionId!, studentId, startDate, endDate)
      : this.reportsService.getTeacherStudentDetailReport(teacherId, studentId, startDate, endDate);

    reportRequest
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (data) => {
          this.report.set(data);
          this.loading.set(false);
        },
        error: (err) => {
          console.error('Error loading report:', err);
          this.report.set(null);
          this.privacyRestricted.set(err?.status === 403);
          if (err?.status !== 403) this.reportError.set('Öğrenci raporu yüklenemedi. Lütfen tekrar deneyin.');
          this.loading.set(false);
        }
      });
  }

  exportReport(): void {
    const studentId = this.selectedStudentId();
    if (!studentId || !this.report()) return;

    const { startDate, endDate } = this.getDateRange();
    const student = this.students.find(s => s.id === studentId);
    const studentName = student?.name || 'ogrenci';

    this.reportsService.exportReportToPdf({
      reportType: 'student-detail',
      title: `${studentName} - Öğrenci Raporu`,
      studentId,
      startDate,
      endDate,
      data: this.report()
    }).subscribe({
      next: (blob) => {
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `${studentName.replace(/\s+/g, '_')}-rapor-${new Date().toISOString().split('T')[0]}.pdf`;
        a.click();
        window.URL.revokeObjectURL(url);
      },
      error: (err) => {
        console.error('Export failed:', err);
      }
    });
  }
}
