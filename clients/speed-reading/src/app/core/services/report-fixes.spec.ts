import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { StudentProgramInfo, StudentProgramService } from './student-program.service';
import { ReportsService, fillDailyActivity } from './reports.service';
import { speedReadingReportLabel } from '../../../../../shared/utils/speed-reading-report-labels';

// H22 (report audit 30.10.2026).
describe('Report display fixes', () => {
  it('shows this week\'s completed days out of seven as weekly progress', () => {
    TestBed.configureTestingModule({ providers: [StudentProgramService, provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(StudentProgramService);
    const program = (week: number, done: number) => ({ currentWeek: week, currentDay: 1, totalDaysCompleted: done }) as StudentProgramInfo;

    expect(service.getProgressPercentage(program(2, 7))).toBe(0);   // week 2, day 1: nothing done this week yet
    expect(service.getProgressPercentage(program(2, 10))).toBe(43); // 3 of 7 days
    expect(service.getProgressPercentage(program(4, 28))).toBe(100);
    expect(service.getProgressPercentage(program(1, 0))).toBe(0);
  });

  it('translates exercise type names written in CamelCase', () => {
    for (const [input, expected] of [
      ['SchulteTable', 'Schulte tablosu'], ['TargetHunt', 'Hedef avı'], ['EyeTracking', 'Göz takibi'],
      ['RegressionReduction', 'Geriye dönüşü azaltma'], ['SubvocalizationReduction', 'İç ses azaltma'], ['toString', 'toString']
    ]) expect(speedReadingReportLabel(input)).withContext(input).toBe(expected);
  });

  it('fills days without activity with zero between the report dates', () => {
    expect(fillDailyActivity([{ date: '2026-10-02', value: 3 }, { date: '2026-10-05', value: 1 }], '2026-10-01', '2026-10-05'))
      .toEqual([
        { date: '2026-10-01', value: 0 }, { date: '2026-10-02', value: 3 }, { date: '2026-10-03', value: 0 },
        { date: '2026-10-04', value: 0 }, { date: '2026-10-05', value: 1 }
      ]);
  });

  it('builds the dashboard activity trend for every Istanbul day of the range', () => {
    TestBed.configureTestingModule({ providers: [ReportsService, provideHttpClient(), provideHttpClientTesting()] });
    const http = TestBed.inject(HttpTestingController);
    let report: any;
    TestBed.inject(ReportsService).getStudentDashboardReport('self', new Date('2026-10-01T00:00:00Z'), new Date('2026-10-03T00:00:00Z'))
      .subscribe(value => report = value);
    http.expectOne(request => request.url.endsWith('/student/summary')).flush({
      userId: 'self', dateFrom: '2026-09-30T21:00:00Z', dateTo: '2026-10-03T20:59:59Z',
      daily: [{ date: '2026-10-02', readingSessions: 1, exerciseCount: 2, averageWpm: 0, averageComprehension: 0 }]
    });

    expect(report.activityTrend.map((point: any) => [point.name, point.series[0].value]))
      .toEqual([['2026-10-01', 0], ['2026-10-02', 3], ['2026-10-03', 0]]);
  });
});
