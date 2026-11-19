import { Component, OnInit, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import { MeasuredProgressService } from '../../../../core/services/measured-progress.service';

interface PerformanceMetric {
  label: string;
  current: number;
  previous: number;
  unit: string;
  percentage: number;
  trend: 'up' | 'down' | 'stable';
  change: number;
  /** False when the student has no measurement for this metric yet: the card says "Ölçülmedi" instead of 0. */
  measured: boolean;
}

interface PerformanceData {
  readingSpeed: PerformanceMetric;
  comprehension: PerformanceMetric;
  eyeMovement: PerformanceMetric;
  concentration: PerformanceMetric;
}

@Component({
  selector: 'app-performance-analysis-widget',
  standalone: true,
  imports: [
    CommonModule
  ],
  templateUrl: './performance-analysis-widget.component.html',
  styleUrls: ['./performance-analysis-widget.component.scss']
})
export class PerformanceAnalysisWidgetComponent implements OnInit {
  private measured = inject(MeasuredProgressService);

  loading = signal(true);
  performanceData = signal<PerformanceData | null>(null);
  hasRealData = signal(false);

  ngOnInit(): void {
    this.loadPerformanceData();
  }

  loadPerformanceData(): void {
    this.measured.load().subscribe({
      next: (measured) => {
        const data: any = measured.raw;
        const currentSpeed = measured.readingSpeed ?? 0;
        const initialSpeed = measured.initialReadingSpeed ?? 0;
        const successRate = data?.averageSuccessRate;
        const completedExercises = data?.completedExercises ?? 0;

        if (completedExercises === 0 && measured.readingSpeed === null) {
          // No program exercises completed yet — don't show misleading values
          this.hasRealData.set(false);
          this.loading.set(false);
          return;
        }

        this.hasRealData.set(true);
        const performance: PerformanceData = {
          readingSpeed: {
            label: 'Okuma Hızı',
            current: currentSpeed ?? 0,
            previous: initialSpeed ?? 0,
            unit: 'KDK',
            percentage: Math.min(((currentSpeed ?? 0) / 600) * 100, 100),
            trend: (currentSpeed ?? 0) >= (initialSpeed ?? 0) ? 'up' : 'down',
            change: initialSpeed ? Math.round(((currentSpeed - initialSpeed) / initialSpeed) * 100) : 0,
            measured: measured.readingSpeed !== null
          },
          comprehension: {
            label: 'Kavrama Oranı',
            current: Math.round(successRate ?? 0),
            previous: 0,
            unit: '%',
            percentage: successRate ?? 0,
            trend: 'stable',
            change: 0,
            measured: measured.comprehension !== null
          },
          eyeMovement: {
            label: 'Göz Hareketi',
            current: data?.eyeMovementScore ?? 0,
            previous: 0,
            unit: '%',
            percentage: data?.eyeMovementScore ?? 0,
            trend: 'stable',
            change: 0,
            measured: (data?.eyeMovementScore ?? 0) > 0
          },
          concentration: {
            label: 'Konsantrasyon',
            current: data?.concentrationScore ?? 0,
            previous: 0,
            unit: '%',
            percentage: data?.concentrationScore ?? 0,
            trend: 'stable',
            change: 0,
            measured: (data?.concentrationScore ?? 0) > 0
          }
        };

        this.performanceData.set(performance);
        this.loading.set(false);
      },
      error: () => {
        this.hasRealData.set(false);
        this.loading.set(false);
      }
    });
  }

  private router = inject(Router);

  navigateToReports(): void {
    this.router.navigate(['/student/reports']);
  }

  getTrendIcon(trend: 'up' | 'down' | 'stable'): string {
    switch (trend) {
      case 'up':     return 'trending_up';
      case 'down':   return 'trending_down';
      case 'stable': return 'trending_flat';
    }
  }

  /** SVG daire progress için stroke-dashoffset hesapla (r=20 → circumference=125.7) */
  getRingOffset(percentage: number): number {
    const circumference = 2 * Math.PI * 20; // 125.66
    return circumference * (1 - Math.min(percentage, 100) / 100);
  }

  readonly ringCircumference = 2 * Math.PI * 20; // 125.66

  getComprehensionMessage(percentage: number): string {
    if (percentage >= 90) return 'Mükemmel! Harika gidiyorsun!';
    if (percentage >= 80) return 'Çok iyi! Devam et!';
    if (percentage >= 70) return 'İyi! Biraz daha çalışmalısın';
    return 'Gelişme gösteriyor, devam et!';
  }

  getEyeMovementMessage(change: number): string {
    if (change >= 10) return 'Çok iyi gelişme!';
    if (change >= 5) return 'İyi gidiyorsun!';
    return 'Devam et!';
  }

  getConcentrationMessage(percentage: number): string {
    if (percentage >= 90) return 'Mükemmel konsantrasyon!';
    if (percentage >= 80) return 'İyi seviyede!';
    return 'Gelişmeye devam!';
  }
}
