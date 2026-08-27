import { Component, OnInit, inject, signal, computed } from '@angular/core';
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
import { StudentsService } from '../../../core/services/students.service';
import { map, startWith } from 'rxjs/operators';
import { Observable } from 'rxjs';

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
export class TeacherStudentDetailReportComponent implements OnInit {
  private reportsService = inject(ReportsService);
  private authService = inject(AuthService);
  private studentsService = inject(StudentsService);
  private route = inject(ActivatedRoute);

  report = signal<TeacherStudentDetailReport | null>(null);
  loading = signal(false);
  loadingStudents = signal(false);
  privacyRestricted = signal(false);

  // Student autocomplete
  studentSearchControl = new FormControl('');
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
    this.loadStudents();

    // Setup autocomplete filter
    this.filteredStudents$ = this.studentSearchControl.valueChanges.pipe(
      startWith(''),
      map(value => this.filterStudents(value || ''))
    );

    // Check for route param
    const studentId = this.route.snapshot.paramMap.get('studentId');
    if (studentId) {
      this.selectedStudentId.set(studentId);
      this.loadReport();
    }
  }

  private filterStudents(value: string | StudentOption): StudentOption[] {
    // Handle both string input and object (when student is selected)
    const filterValue = typeof value === 'string'
      ? value.toLowerCase()
      : (value?.name || '').toLowerCase();
    return this.students.filter(s => s.name.toLowerCase().includes(filterValue));
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

  loadStudents(): void {
    this.loadingStudents.set(true);

    // Admin sees all students; teacher sees only their own students
    const isAdmin = this.authService.hasAdminAccess();
    const queryTeacherId = this.route.snapshot.queryParamMap.get('teacherId');
    const teacherId = queryTeacherId || (isAdmin ? undefined : this.authService.currentUserValue?.id);

    this.studentsService.getStudents(undefined, undefined, undefined, undefined, teacherId).subscribe({
      next: (data) => {
        this.students = data.map(s => ({
          id: s.id,
          name: `${s.firstName} ${s.lastName}`
        }));
        this.loadingStudents.set(false);

        // If we have a pre-selected student ID, find and set the display value
        const preSelectedId = this.selectedStudentId();
        if (preSelectedId) {
          const student = this.students.find(s => s.id === preSelectedId);
          if (student) {
            this.studentSearchControl.setValue(student as any);
          }
        }
      },
      error: (err) => {
        console.error('Error loading students:', err);
        this.loadingStudents.set(false);
      }
    });
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

    // Admin uses own ID; teacher uses own ID or override from query param
    const isAdmin = this.authService.hasAdminAccess();
    const effectiveId = this.route.snapshot.queryParamMap.get('teacherId') || this.authService.currentUserValue?.id;

    if (!effectiveId) {
      console.error('User ID not found in auth service');
      return;
    }
    const teacherId = effectiveId;

    this.loading.set(true);
    const { startDate, endDate } = this.getDateRange();

    this.reportsService.getTeacherStudentDetailReport(teacherId, studentId, startDate, endDate).subscribe({
      next: (data) => {
        const dashboard = data.studentReports?.dashboard;
        const isRestricted = dashboard &&
          dashboard.currentLevel === 0 &&
          dashboard.totalActivities === 0 &&
          dashboard.currentWPM === 0;

        this.privacyRestricted.set(!!isRestricted);
        this.report.set(data);
        this.loading.set(false);
      },
      error: (err) => {
        console.error('Error loading report:', err);
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
