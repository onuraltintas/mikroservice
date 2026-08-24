import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReportsService } from '../../../core/services/reports.service';
import { AuthService } from '../../../core/services/auth.service';
import { StudentReadingService } from '../../../core/services/student-reading.service';
import { StudentDashboardReport } from '../../../core/models/report.model';
import { ReadingSession } from '../../../core/models/reading-text.model';
import { format } from 'date-fns';
import { LineChartComponent } from '../../../shared/components/charts/line-chart.component';
import { PieChartComponent } from '../../../shared/components/charts/pie-chart.component';

import { RouterModule } from '@angular/router';

@Component({
  selector: 'app-student-dashboard-report',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    LineChartComponent,
    PieChartComponent
  ],
  templateUrl: './student-dashboard-report.component.html',
  styleUrls: ['./student-dashboard-report.component.scss']
})
export class StudentDashboardReportComponent implements OnInit {
  private reportsService = inject(ReportsService);
  private authService = inject(AuthService);
  private readingService = inject(StudentReadingService);

  report = signal<StudentDashboardReport | null>(null);
  historyData = signal<ReadingSession[]>([]);
  loading = signal(true);

  ngOnInit(): void {
    this.loadReport();
  }

  loadReport(): void {
    const currentUser = this.authService.currentUserValue;
    if (!currentUser) {
      console.error('No authenticated user found');
      this.loading.set(false);
      return;
    }

    const studentId = currentUser.id;
    const endDate = new Date();
    const startDate = new Date(0); // 1970-01-01

    // Load main report
    this.reportsService.getStudentDashboardReport(studentId, startDate, endDate).subscribe({
      next: (data) => {
        this.report.set(data);
        this.loading.set(false);
      },
      error: (error) => {
        console.error('Failed to load dashboard report', error);
        this.loading.set(false);
      }
    });

    // Load full history for charts (since progression endpoints are missing)
    this.readingService.getHistory().subscribe({
      next: (sessions) => {
        // Sort by date ascending for charts
        const sortedSessions = sessions.sort((a, b) =>
          new Date(a.completedAt).getTime() - new Date(b.completedAt).getTime()
        );
        this.historyData.set(sortedSessions);
      },
      error: (err) => console.error('Failed to load reading history', err)
    });
  }

  // Transform pie chart data - pie charts need {name, value} format
  pieChartData = computed(() => {
    const data = this.report()?.activityDistribution;
    if (!data || data.length === 0) return [];

    // Flatten: take first series value from each item
    return data.map(item => ({
      name: this.getTurkishExerciseName(item.name),
      value: item.series?.[0]?.value || 0
    }));
  });

  private getTurkishExerciseName(englishName: string): string {
    const translations: Record<string, string> = {
      'Schulte Table': 'Schulte Tablosu',
      'Visual Expansion': 'Görsel Genişleme',
      'Tachistoscope': 'Takistoskop',
      'Scanning': 'Tarama (Scanning)',
      'Subvocalization Reduction': 'Alt Seslendirme Azaltma',
      'Text Fading': 'Metin Solma',
      'Fixation Exercise': 'Fiksasyon Egzersizi',
      'Saccade Exercise': 'Sakkad Hareketi',
      'Regression Reduction': 'Geriye Dönüş Azaltma',
      'Focus Exercise': 'Odak Egzersizi',
      'Visualization Exercise': 'Görselleştirme',
      'Vocabulary Builder': 'Kelime Hazinesi',
      'Skimming': 'Gözden Geçirme (Skimming)',
      'Exam Simulation': 'Paragraf Simülasyonu',
      'Eye Tracking': 'Göz Takibi (Eye Tracking)',
      'Chunking': 'Bloklama (Chunking)',
      'Rapid Serial': 'Seri Okuma (RSVP)',
      'Vertical Reading': 'Dikey Okuma',
      'Visual Span': 'Görsel Alan',
      'Word Groups': 'Kelime Grupları',
      'Speed Reading': 'Hızlı Okuma'
    };

    return translations[englishName] || englishName;
  }

  // Transform line chart data - line charts need [{name: seriesName, series: [{name: x, value: y}]}]
  activityTrendData = computed(() => {
    const data = this.report()?.activityTrend;
    if (!data || data.length === 0) return [];

    // Transform from backend format to chart format
    return [{
      name: 'Aktiviteler',
      series: data.map(item => ({
        name: item.name,
        value: item.series?.[0]?.value || 0
      }))
    }];
  });

  wpmProgressData = computed(() => {
    const sessions = this.historyData();
    if (!sessions || sessions.length === 0) return [];

    const recentSessions = sessions.slice(-20);
    return [{
      name: 'WPM',
      series: recentSessions
        .filter(s => s.completedAt && !isNaN(new Date(s.completedAt).getTime()))
        .map(session => ({
          name: format(new Date(session.completedAt), 'MMM d'),
          value: session.calculatedWPM
        }))
    }];
  });

  comprehensionTrendData = computed(() => {
    const sessions = this.historyData();
    if (!sessions || sessions.length === 0) return [];

    const recentSessions = sessions.slice(-20);
    return [{
      name: 'Anlama Oranı',
      series: recentSessions
        .filter(s => s.completedAt && !isNaN(new Date(s.completedAt).getTime()))
        .map(session => ({
          name: format(new Date(session.completedAt), 'MMM d'),
          value: Math.round(session.comprehensionRate)
        }))
    }];
  });

  getMilestoneIcon(type: string): string {
    const icons: Record<string, string> = {
      speed: 'speed',
      comprehension: 'psychology',
      streak: 'local_fire_department',
      completion: 'check_circle',
      achievement: 'emoji_events'
    };
    return icons[type] || 'star';
  }

  formatDate(date: Date | string | null | undefined): string {
    if (!date) return '';
    const d = new Date(date);
    if (isNaN(d.getTime())) return '';
    return format(d, 'MMM d, yyyy');
  }
}
