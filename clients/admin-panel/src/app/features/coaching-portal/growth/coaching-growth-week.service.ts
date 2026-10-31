import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { CoachingMotivationApi } from '../../../../../shared/coaching-motivation.component';
import { WeekInput, weekRanges } from './growth-model';

interface StudyWeekTotals { fromDate: string; toDate: string; completedTasks: number; scheduledTasks: number; actualMinutes: number | null }

/** T6: study minutes and finished tasks for the last weeks, and the motivation summary. T7: all weeks in one request. */
@Injectable({ providedIn: 'root' })
export class CoachingGrowthWeekService {
  private readonly http = inject(HttpClient);
  private readonly motivationApi = new CoachingMotivationApi(this.http, environment.apiUrl);

  weeks(count = 6) {
    const today = new Intl.DateTimeFormat('en-CA', { timeZone: 'Europe/Istanbul', year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date());
    const ranges = weekRanges(today, count);
    return this.http.get<{ data: StudyWeekTotals[] }>(`${environment.apiUrl}/coaching/study-planning/reports/weeks`,
      { params: new HttpParams().set('fromDate', ranges[0].from).set('weeks', count) }).pipe(
      map(response => ranges.map((range, index): WeekInput => {
        const week = response.data.find(row => row.fromDate === range.from) ?? response.data[index];
        return { label: range.label, minutes: week?.actualMinutes ?? 0, tasks: week?.completedTasks ?? 0, scheduled: week?.scheduledTasks ?? 0 };
      })));
  }

  motivation() { return this.motivationApi.get('me'); }
}
