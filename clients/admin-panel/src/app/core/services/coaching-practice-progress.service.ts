import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface PracticeNetTrend {
  examCode: string; name: string; source: string; change: number | null;
  points: { examId: string; title: string; examDate: string; net: number }[];
  sections: { code: string; name: string; questionCount: number; averageNet: number; netPercentage: number; needsReview: boolean }[];
}
export interface PracticeGoal {
  goalId: string; title: string; examCode: string | null; targetNet: number | null; catalogName: string | null;
  catalogYear: number | null; scoreYear: number | null; placementStage: string | null; minimumScore: number | null;
  rank: number | null; percentile: number | null;
  comparisons: { source: string; examDate: string; latestNet: number; remainingNet: number }[];
}
export interface RecoveryProposal {
  planId: string; expectedVersion: number; taskIds: string[]; title: string; previousDate: string; previousStartMinute: number;
  plannedDate: string; startMinute: number; minutes: number;
}
export interface PracticeProgress {
  fromDate: string; toDate: string; today: string; timeZoneId: string; trends: PracticeNetTrend[]; goals: PracticeGoal[];
  scheduledTasks: number; completedTasks: number; overdueTasks: number; pinnedOverdueTasks: number;
  recovery: RecoveryProposal[]; recoveryReason: string;
}
@Injectable({ providedIn: 'root' })
export class CoachingPracticeProgressService {
  private readonly http = inject(HttpClient);
  get(studentId: string | null, fromDate = '', toDate = '') {
    const root = studentId ? `${environment.apiUrl}/coaching/staff/students/${encodeURIComponent(studentId)}/study` : `${environment.apiUrl}/coaching/study-planning`;
    let params = new HttpParams(); if (fromDate) params = params.set('fromDate', fromDate); if (toDate) params = params.set('toDate', toDate);
    return this.http.get<{ data: PracticeProgress }>(`${root}/practice-progress`, { params }).pipe(map(x => x.data));
  }
}
