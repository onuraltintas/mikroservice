import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideCharts, withDefaultRegisterables } from 'ng2-charts';
import { ReportsService } from '../../../core/services/reports.service';
import { AuthService } from '../../../core/services/auth.service';
import { StudentReadingService } from '../../../core/services/student-reading.service';
import { StudentDashboardReportComponent } from './student-dashboard-report.component';
import { StudentReadingSpeedReportComponent } from './student-reading-speed-report.component';
import { StudentComprehensionReportComponent } from './student-comprehension-report.component';

describe('reports without measured speed', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [StudentDashboardReportComponent, StudentReadingSpeedReportComponent, StudentComprehensionReportComponent],
      providers: [provideRouter([]), provideCharts(withDefaultRegisterables()), ...[ReportsService, AuthService, StudentReadingService].map(provide => ({ provide, useValue: {} }))]
    });
    spyOn(StudentDashboardReportComponent.prototype, 'ngOnInit');
    spyOn(StudentReadingSpeedReportComponent.prototype, 'ngOnInit');
    spyOn(StudentComprehensionReportComponent.prototype, 'ngOnInit');
  });

  it('shows no data instead of a zero speed gauge, benchmarks or statistics', () => {
    const fixture = TestBed.createComponent(StudentReadingSpeedReportComponent);
    fixture.componentInstance.loading.set(false);
    fixture.componentInstance.report.set({ currentWPM: 0, wpmBenchmarks: [],
      wpmTrendChart: { data: [], labels: [] }, categoryWPMChart: { data: [] },
      statistics: { averageWPM: 0, medianWPM: 0, minWPM: 0, maxWPM: 0, standardDeviation: 0, improvementRate: 0, totalReadings: 0 }
    } as any);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Veri yok');
    expect(fixture.nativeElement.querySelector('app-gauge-chart')).toBeNull();
    expect(fixture.componentInstance.statsDataSource()).toEqual([]);
  });

  it('keeps real speed measurements visible', () => {
    const fixture = TestBed.createComponent(StudentReadingSpeedReportComponent);
    fixture.componentInstance.loading.set(false);
    fixture.componentInstance.report.set({ currentWPM: 150, wpmBenchmarks: [],
      wpmTrendChart: { data: [], labels: [] }, categoryWPMChart: { data: [] },
      statistics: { averageWPM: 150, medianWPM: 150, minWPM: 150, maxWPM: 150, standardDeviation: 0, improvementRate: 0, totalReadings: 1 }
    } as any);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('app-gauge-chart')).not.toBeNull();
    expect(fixture.componentInstance.statsDataSource()[0].value).toBe('150');
    for (const label of ['Ortalama hız', 'Ortanca hız', 'En düşük hız', 'En yüksek hız', 'Standart sapma', 'Gelişim oranı', 'Toplam okuma']) {
      expect(fixture.nativeElement.textContent).toContain(label);
    }
    expect(fixture.nativeElement.textContent).not.toContain('Average WPM');
  });

  it('does not plot unmeasured or zero-speed history and shows no data in the summary speed card', () => {
    const fixture = TestBed.createComponent(StudentDashboardReportComponent);
    fixture.componentInstance.loading.set(false);
    fixture.componentInstance.report.set({ currentWPM: 0 } as any);
    fixture.componentInstance.historyData.set([
      { isMeasured: false, calculatedWPM: 999, completedAt: '2026-10-16T08:00:00Z' },
      { isMeasured: true, calculatedWPM: 0, completedAt: '2026-10-16T09:00:00Z' }
    ] as any);
    fixture.detectChanges();
    const card = Array.from(fixture.nativeElement.querySelectorAll('.stat-card'))
      .find((item: any) => item.textContent.includes('Okuma Hızı')) as HTMLElement;
    expect(card.querySelector('.stat-value')?.textContent).toContain('Veri yok');
    expect(fixture.componentInstance.wpmProgressData()).toEqual([]);
  });

  for (const available of [false, true]) {
    it(`distinguishes missing comprehension from a measured zero (available=${available})`, () => {
      const fixture = TestBed.createComponent(StudentComprehensionReportComponent);
      fixture.componentInstance.loading.set(false);
      fixture.componentInstance.report.set({ dataAvailable: available, overallComprehension: 0,
        comprehensionBenchmarks: [], questionTypeChart: { data: [] }, bloomLevelChart: { data: [] },
        categoryComprehensionChart: { data: [] }, categoryBreakdown: [], improvementAreas: [] } as any);
      fixture.detectChanges();
      expect(!!fixture.nativeElement.querySelector('app-number-card')).toBe(available);
      if (!available) expect(fixture.nativeElement.textContent).toContain('Veri yok');
    });
  }
});
