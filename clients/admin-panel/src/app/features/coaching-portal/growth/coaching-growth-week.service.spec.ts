import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { CoachingGrowthWeekService } from './coaching-growth-week.service';
import { weekRanges } from './growth-model';

// T7 (01.11.2026): Haftan reads the last six weeks with one request instead of one report per week.
describe('CoachingGrowthWeekService', () => {
  it('reads all weeks in one request and keeps the oldest-to-newest order', async () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const http = TestBed.inject(HttpTestingController);
    const today = new Intl.DateTimeFormat('en-CA', { timeZone: 'Europe/Istanbul', year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date());
    const ranges = weekRanges(today, 6);
    const result = firstValueFrom(TestBed.inject(CoachingGrowthWeekService).weeks(6));
    const request = http.expectOne(r => r.url.endsWith('/coaching/study-planning/reports/weeks'));
    expect(request.request.params.get('fromDate')).toBe(ranges[0].from);
    expect(request.request.params.get('weeks')).toBe('6');
    request.flush({ data: ranges.map((range, index) => ({ fromDate: range.from, toDate: range.to, scheduledTasks: 10, completedTasks: index, actualMinutes: index ? index * 30 : null })) });
    const weeks = await result;
    expect(weeks.map(w => w.tasks)).toEqual([0, 1, 2, 3, 4, 5]);
    expect(weeks[0].minutes).toBe(0);
    expect(weeks[5]).toEqual({ label: 'Bu hafta', minutes: 150, tasks: 5, scheduled: 10 });
    http.verify();
  });
});
