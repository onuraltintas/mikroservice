import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { environment } from '../../../environments/environment';
import { DailyAgendaItem } from './coaching-daily-agenda.service';

/** T3 (31.10.2026): one summary for the student's "Bugün" screen, worked out in the student's own time zone. */
export interface StudentTodayView {
  today: string; timeZoneId: string; generatedAt: string;
  nextStep: { item: DailyAgendaItem; overdue: boolean } | null;
  items: DailyAgendaItem[]; overdue: DailyAgendaItem[];
  todayCount: number; overdueCount: number; upcomingCount: number;
  exam: { name: string; date: string; daysLeft: number; source: 'Student' | 'Catalog' } | null;
  streak: { currentStreak: number; longestStreak: number; activeToday: boolean };
  week: { weekStart: string; planned: number; completed: number };
  goal: { goalId: string; title: string; examCode: string; targetNet: number; latestNet: number; remainingNet: number } | null;
  weeklyReview: { needed: boolean; weekStart: string };
  nextSession: { id: string; title: string; scheduledDate: string; durationMinutes: number; meetingLink: string | null } | null;
  hasActivePlan: boolean;
}

@Injectable({ providedIn: 'root' })
export class CoachingStudentTodayService {
  private readonly http = inject(HttpClient);
  get() { return this.http.get<StudentTodayView>(`${environment.apiUrl}/coaching/students/me/today`); }
}
