import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTableModule } from '@angular/material/table';
import {
  TeacherReportService,
  TeacherStudentReadingSpeedReport,
  TeacherStudentComprehensionReport,
  TeacherStudentActivityReport
} from '../../../core/services/teacher-report.service';
import { StudentsService } from '../../../core/services/students.service';
import { LineChartComponent } from '../../../shared/components/charts/line-chart.component';

@Component({
  selector: 'app-teacher-reports',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatCardModule,
    MatSelectModule,
    MatFormFieldModule,
    MatButtonModule,
    MatProgressSpinnerModule,
    MatTabsModule,
    MatTableModule,
    LineChartComponent
  ],
  templateUrl: './reports.component.html',
  styleUrls: ['./reports.component.scss']
})
export class ReportsComponent implements OnInit {
  private readonly reportService = inject(TeacherReportService);
  private readonly studentsService = inject(StudentsService);

  selectedStudentControl = new FormControl('');
  loading = false;
  loadingStudents = false;

  kdpReport: TeacherStudentReadingSpeedReport | null = null;
  comprehensionReport: TeacherStudentComprehensionReport | null = null;
  activityReport: TeacherStudentActivityReport | null = null;

  typeStatsColumns = ['type', 'count', 'averageKDP', 'averageComprehension'];

  mockStudents: any[] = [];

  kdpChartData: any[] = [];

  comprehensionChartData: any[] = [];

  ngOnInit(): void {
    this.loadStudents();
  }

  loadStudents(): void {
    this.loadingStudents = true;
    this.studentsService.getStudents().subscribe({
      next: (data) => {
        this.mockStudents = data.map(s => ({
          id: s.id,
          name: `${s.firstName} ${s.lastName}`
        }));
        this.loadingStudents = false;
      },
      error: (err) => {
        console.error('Error loading students:', err);
        this.loadingStudents = false;
      }
    });
  }

  onStudentSelect(): void {
    const studentId = this.selectedStudentControl.value;
    if (!studentId) return;

    this.loading = true;

    // Load KDP Trend (Reading Speed)
    this.reportService.getStudentReadingSpeedTrend(studentId, 30).subscribe({
      next: (data) => {
        this.kdpReport = data;
        // Transform chart data from backend format
        this.kdpChartData = data.wpmOverTime.map((chartData: any) => ({
          name: chartData.name,
          series: chartData.series.map((dp: any) => ({
            name: dp.name,
            value: dp.value
          }))
        }));
      },
      error: (err) => console.error('Error loading KDP trend:', err)
    });

    // Load Comprehension Trend
    this.reportService.getStudentComprehensionTrend(studentId, 30).subscribe({
      next: (data) => {
        this.comprehensionReport = data;
        this.comprehensionChartData = data.comprehensionOverTime.map((d: any, index: any) => ({
          name: d.name,
          value: d.series?.[0]?.value || 0
        }));
      },
      error: (err) => console.error('Error loading comprehension trend:', err)
    });

    // Load Detailed Report (Activity Report)
    this.reportService.getStudentActivityReport(studentId).subscribe({
      next: (data) => {
        this.activityReport = data;
        this.loading = false;
      },
      error: (err) => {
        console.error('Error loading activity report:', err);
        this.loading = false;
      }
    });
  }

  getTrendText(trend: string): string {
    switch (trend) {
      case 'Improving': return 'Yükseliyor ↗';
      case 'Declining': return 'Düşüyor ↘';
      case 'Stable': return 'Sabit →';
      default: return trend;
    }
  }

  getExerciseTypeName(type: string): string {
    const types: { [key: string]: string } = {
      '0': 'Hızlı Okuma',
      '1': 'Göz Takibi',
      '2': 'Kelime Algılama',
      '3': 'Odaklanma',
      '4': 'Hafıza'
    };
    return types[type] || type;
  }
}
