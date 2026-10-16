import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideCharts, withDefaultRegisterables } from 'ng2-charts';
import { ReportsService } from '../../../core/services/reports.service';
import { AuthService } from '../../../core/services/auth.service';
import { StudentReadingService } from '../../../core/services/student-reading.service';
import { StudentDashboardReportComponent } from './student-dashboard-report.component';
import { StudentReadingSpeedReportComponent } from './student-reading-speed-report.component';
import { StudentComprehensionReportComponent } from './student-comprehension-report.component';
import { StudentActivityReportComponent } from './student-activity-report.component';

describe('reports without measured speed', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [StudentDashboardReportComponent, StudentReadingSpeedReportComponent, StudentComprehensionReportComponent, StudentActivityReportComponent],
      providers: [provideRouter([]), provideCharts(withDefaultRegisterables()), ...[ReportsService, AuthService, StudentReadingService].map(provide => ({ provide, useValue: {} }))]
    });
    spyOn(StudentDashboardReportComponent.prototype, 'ngOnInit');
    spyOn(StudentReadingSpeedReportComponent.prototype, 'ngOnInit');
    spyOn(StudentComprehensionReportComponent.prototype, 'ngOnInit');
    spyOn(StudentActivityReportComponent.prototype, 'ngOnInit');
  });

  it('keeps exercise chart names and both trend month labels entirely Turkish', () => {
    const fixture = TestBed.createComponent(StudentDashboardReportComponent);
    fixture.componentInstance.report.set({ activityDistribution: ['Scanning', 'Skimming', 'Eye Tracking', 'Chunking'].map(name => ({ name, series: [{ value: 1 }] })) } as any);
    fixture.componentInstance.report.update(report => ({ ...report,
      wpmProgress: [{ name: '2026-10-16', series: [{ value: 150 }] }],
      comprehensionTrend: [{ name: '2026-10-16', series: [{ value: 0 }] }] } as any));
    expect(fixture.componentInstance.pieChartData().map(item => item.name)).toEqual(['Tarayarak okuma', 'Göz gezdirerek okuma', 'Göz takibi', 'Gruplayarak okuma']);
    expect(fixture.componentInstance.wpmProgressData()[0].series[0].name).toBe('16 Eki');
    expect(fixture.componentInstance.comprehensionTrendData()[0].series[0].name).toBe('16 Eki');
  });

  it('renders Turkish activity empty states and minute units', () => {
    const fixture = TestBed.createComponent(StudentActivityReportComponent);
    fixture.componentInstance.loading.set(false);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Etkinlik verisi bulunmuyor');
    fixture.componentInstance.report.set({ currentStreak: { days: 0, longestStreak: 0 }, activityHeatmap: { data: [] }, hourlyDistributionChart: { data: [] }, dailyDistributionChart: { data: [] }, studyTime: { totalMinutes: 75, averageSessionLength: 15, totalSessions: 5, mostActiveHour: 12 } } as any);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('15 dk');
    expect(fixture.nativeElement.textContent).not.toContain('min');
    expect(fixture.nativeElement.textContent).toContain('Daha az');
    expect(fixture.nativeElement.textContent).toContain('Daha fazla');
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

  it('plots the same period as summary cards instead of a capped history page', () => {
    const fixture = TestBed.createComponent(StudentDashboardReportComponent);
    fixture.componentInstance.report.set({ wpmProgress: [{ name: '2026-10-16', series: [{ value: 150 }] }],
      comprehensionTrend: [{ name: '2026-10-16', series: [{ value: 0 }] }] } as any);
    expect(fixture.componentInstance.wpmProgressData().length).toBe(1);
    expect(fixture.componentInstance.comprehensionTrendData().length).toBe(1);
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

  it('does not plot missing speed and shows no data in the summary speed card', () => {
    const fixture = TestBed.createComponent(StudentDashboardReportComponent);
    fixture.componentInstance.loading.set(false);
    fixture.componentInstance.report.set({ currentWPM: 0 } as any);
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
      if (available) expect(fixture.componentInstance.overallComprehensionData()[0].name).toBe('Anlama');
    });
  }
});
