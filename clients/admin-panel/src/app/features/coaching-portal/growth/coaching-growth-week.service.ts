import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { catchError, forkJoin, map, of } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { CoachingMotivationApi } from '../../../../../shared/coaching-motivation.component';
import { WeekInput, weekRanges } from './growth-model';

interface StudyReportTotals { completedTasks: number; scheduledTasks: number; actualMinutes: number | null }

/** T6: study minutes and finished tasks for the last weeks (one report per week), and the motivation summary. */
@Injectable({ providedIn: 'root' })
export class CoachingGrowthWeekService {
  private readonly http = inject(HttpClient);
  private readonly motivationApi = new CoachingMotivationApi(this.http, environment.apiUrl);

  weeks(count = 6) {
    const today = new Intl.DateTimeFormat('en-CA', { timeZone: 'Europe/Istanbul', year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date());
    const ranges = weekRanges(today, count);
    // A week whose report fails is shown as empty rather than hiding the others.
    return forkJoin(ranges.map(range => this.http.get<{ data: StudyReportTotals }>(`${environment.apiUrl}/coaching/study-planning/reports`,
      { params: new HttpParams().set('fromDate', range.from).set('toDate', range.to) }).pipe(map(x => x.data), catchError(() => of(null))))).pipe(
      map(reports => reports.map((report, index): WeekInput => ({ label: ranges[index].label, minutes: report?.actualMinutes ?? 0,
        tasks: report?.completedTasks ?? 0, scheduled: report?.scheduledTasks ?? 0 }))));
  }

  motivation() { return this.motivationApi.get('me'); }
}
