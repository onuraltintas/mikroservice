import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

// ── Models ────────────────────────────────────────────────────────────────────

export interface CoachingRelationship {
  id: string;
  studentId: string;
  studentName: string;
  coachId: string;
  coachName: string;
  status: 'Active' | 'Paused' | 'Completed' | 'Cancelled';
  startDate: string;
  endDate: string | null;
  initialNotes: string | null;
  institutionId: string | null;
  createdAt: string;
  recentSessions?: { id: string; scheduledAt: string; status: string }[];
  pendingAssignments?: number;
}

export interface CoachingGoal {
  id: string;
  studentId: string;
  studentName: string;
  coachId: string | null;
  parentGoalId: string | null;
  level: 'Yearly' | 'Monthly' | 'Weekly' | 'Daily';
  title: string;
  description: string | null;
  targetDate: string;
  status: 'Active' | 'Completed' | 'Failed' | 'Cancelled' | 'Paused';
  completionPercentage: number;
  isCreatedByCoach: boolean;
  successCriteria: string | null;
  subGoalCount?: number;
  subGoals?: CoachingGoal[];
  checkIns?: GoalCheckIn[];
}

export interface GoalCheckIn {
  id: string;
  checkedById: string;
  previousPercentage: number;
  newPercentage: number;
  note: string | null;
  checkedAt: string;
}

export interface CoachingSession {
  id: string;
  coachId: string;
  studentId: string;
  studentName?: string;
  coachName?: string;
  coachingRelationshipId: string;
  scheduledAt: string;
  plannedDurationMinutes: number;
  actualDurationMinutes: number | null;
  status: 'Scheduled' | 'Completed' | 'Cancelled' | 'NoShow' | 'Rescheduled';
  sessionType: 'Regular' | 'GoalReview' | 'ExamReview' | 'Emergency' | 'Closing';
  sharedNotes: string | null;
  coachPrivateNotes: string | null;
  actionItems: string | null;
  studentRating: number | null;
  meetingLink: string | null;
}

export interface CoachingAssignment {
  id: string;
  studentId: string;
  studentName?: string;
  assignedById: string;
  title: string;
  description: string | null;
  subjectId: string | null;
  topicId: string | null;
  dueDate: string;
  status: 'Pending' | 'Completed' | 'PartiallyCompleted' | 'Overdue' | 'Cancelled';
  completionNote: string | null;
  completedAt: string | null;
  targetQuestions: number | null;
  targetMinutes: number | null;
  isApprovedByCoach: boolean | null;
  coachFeedback: string | null;
  createdAt: string;
}

export interface ExamResult {
  id: string;
  studentId: string;
  examName: string;
  examDate: string;
  examType: string;
  totalNet: number | null;
  estimatedScore: number | null;
  notes: string | null;
  isRecordedByCoach: boolean;
  createdAt: string;
  subjectResults?: ExamSubjectResult[];
}

export interface ExamSubjectResult {
  id: string;
  subjectId: string;
  subjectName?: string;
  totalQuestions: number;
  correct: number;
  wrong: number;
  empty: number;
  net: number;
  targetNet: number | null;
}

export interface StudySession {
  id: string;
  studentId: string;
  studentName?: string;
  subjectId: string;
  topicId: string | null;
  date: string;
  plannedMinutes: number;
  actualMinutes: number;
  questionsSolved: number;
  correctAnswers: number | null;
  wrongAnswers: number | null;
  studyType: string;
  notes: string | null;
  isVerifiedByCoach: boolean;
  createdAt: string;
}

export interface CoachingSubject {
  id: string;
  name: string;
  examType: string;
  isSystem: boolean;
  color: string | null;
  sortOrder: number;
  totalQuestionsInExam: number | null;
  topics?: { id: string; name: string; orderIndex: number }[];
}

export interface SessionBriefing {
  studentName: string;
  periodStart: string;
  daysSinceLastSession: number;
  studySummary: {
    totalMinutes: number;
    totalQuestions: number;
    totalSessions: number;
    subjectsWorked: string[];
  };
  goalsCompleted: { id: string; title: string; level: string }[];
  goalsStalled: { id: string; title: string; level: string; completionPercentage: number; targetDate: string }[];
  assignmentsSubmitted: { id: string; title: string; status: string; isApprovedByCoach: boolean | null; completionNote: string | null }[];
  examsEntered: { id: string; examName: string; examDate: string; totalNet: number | null; estimatedScore: number | null }[];
  alertFlags: string[];
}

export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

// ── Service ───────────────────────────────────────────────────────────────────

@Injectable({ providedIn: 'root' })
export class CoachingApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/v1/coaching`;

  // ── Relationships ─────────────────────────────────────────────────────────

  getRelationships(params?: { studentId?: string; status?: string; page?: number; pageSize?: number }): Observable<PagedResult<CoachingRelationship>> {
    let p = new HttpParams();
    if (params?.studentId) p = p.set('studentId', params.studentId);
    if (params?.status)    p = p.set('status', params.status);
    if (params?.page)      p = p.set('page', params.page.toString());
    if (params?.pageSize)  p = p.set('pageSize', params.pageSize.toString());
    return this.http.get<PagedResult<CoachingRelationship>>(`${this.base}/relationships`, { params: p });
  }

  getRelationship(id: string): Observable<CoachingRelationship> {
    return this.http.get<CoachingRelationship>(`${this.base}/relationships/${id}`);
  }

  createRelationship(dto: { studentId: string; coachId: string; institutionId?: string; startDate?: string; initialNotes?: string }): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.base}/relationships`, dto);
  }

  updateRelationshipStatus(id: string, dto: { status: string; endDate?: string }): Observable<void> {
    return this.http.put<void>(`${this.base}/relationships/${id}/status`, dto);
  }

  getAtRiskStudents(inactiveDays = 7): Observable<{ studentId: string; studentName: string; lastStudyDate: string | null; daysSinceLastStudy: number | null }[]> {
    return this.http.get<any[]>(`${this.base}/relationships/at-risk-students`, {
      params: new HttpParams().set('inactiveDays', inactiveDays.toString())
    });
  }

  // ── Goals ─────────────────────────────────────────────────────────────────

  getGoals(params?: { studentId?: string; level?: string; status?: string; parentGoalId?: string; page?: number; pageSize?: number }): Observable<PagedResult<CoachingGoal>> {
    let p = new HttpParams();
    if (params?.studentId)    p = p.set('studentId', params.studentId);
    if (params?.level)        p = p.set('level', params.level);
    if (params?.status)       p = p.set('status', params.status);
    if (params?.parentGoalId) p = p.set('parentGoalId', params.parentGoalId);
    if (params?.page)         p = p.set('page', params.page.toString());
    if (params?.pageSize)     p = p.set('pageSize', params.pageSize.toString());
    return this.http.get<PagedResult<CoachingGoal>>(`${this.base}/goals`, { params: p });
  }

  getGoal(id: string): Observable<CoachingGoal> {
    return this.http.get<CoachingGoal>(`${this.base}/goals/${id}`);
  }

  createGoal(dto: { studentId?: string; coachId?: string; parentGoalId?: string; level: string; title: string; description?: string; targetDate: string; successCriteria?: string }): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.base}/goals`, dto);
  }

  updateGoal(id: string, dto: { title?: string; description?: string; targetDate?: string; successCriteria?: string; status?: string }): Observable<void> {
    return this.http.put<void>(`${this.base}/goals/${id}`, dto);
  }

  goalCheckIn(id: string, dto: { newPercentage: number; note?: string }): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.base}/goals/${id}/check-in`, dto);
  }

  deleteGoal(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/goals/${id}`);
  }

  // ── Coaching Sessions ─────────────────────────────────────────────────────

  getSessions(params?: { studentId?: string; status?: string; from?: string; to?: string; page?: number; pageSize?: number }): Observable<PagedResult<CoachingSession>> {
    let p = new HttpParams();
    if (params?.studentId) p = p.set('studentId', params.studentId);
    if (params?.status)    p = p.set('status', params.status);
    if (params?.from)      p = p.set('from', params.from);
    if (params?.to)        p = p.set('to', params.to);
    if (params?.page)      p = p.set('page', params.page.toString());
    if (params?.pageSize)  p = p.set('pageSize', params.pageSize.toString());
    return this.http.get<PagedResult<CoachingSession>>(`${this.base}/sessions`, { params: p });
  }

  getSession(id: string): Observable<CoachingSession> {
    return this.http.get<CoachingSession>(`${this.base}/sessions/${id}`);
  }

  getSessionBriefing(id: string): Observable<SessionBriefing> {
    return this.http.get<SessionBriefing>(`${this.base}/sessions/${id}/briefing`);
  }

  createSession(dto: { coachId: string; studentId: string; scheduledAt: string; plannedDurationMinutes: number; sessionType: string; meetingLink?: string; sharedNotes?: string; coachPrivateNotes?: string }): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.base}/sessions`, dto);
  }

  updateSession(id: string, dto: { status?: string; scheduledAt?: string; actualDurationMinutes?: number; sharedNotes?: string; coachPrivateNotes?: string; actionItems?: string; meetingLink?: string; studentRating?: number }): Observable<void> {
    return this.http.put<void>(`${this.base}/sessions/${id}`, dto);
  }

  deleteSession(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/sessions/${id}`);
  }

  // ── Assignments ───────────────────────────────────────────────────────────

  getAssignments(params?: { studentId?: string; status?: string; pendingReview?: boolean; page?: number; pageSize?: number }): Observable<PagedResult<CoachingAssignment>> {
    let p = new HttpParams();
    if (params?.studentId)     p = p.set('studentId', params.studentId);
    if (params?.status)        p = p.set('status', params.status);
    if (params?.pendingReview) p = p.set('pendingReview', 'true');
    if (params?.page)          p = p.set('page', params.page.toString());
    if (params?.pageSize)      p = p.set('pageSize', params.pageSize.toString());
    return this.http.get<PagedResult<CoachingAssignment>>(`${this.base}/assignments`, { params: p });
  }

  createAssignment(dto: { studentId: string; relationshipId?: string; title: string; description?: string; subjectId?: string; topicId?: string; dueDate: string; targetQuestions?: number; targetMinutes?: number }): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.base}/assignments`, dto);
  }

  reviewAssignment(id: string, dto: { isApproved: boolean; coachFeedback?: string }): Observable<void> {
    return this.http.put<void>(`${this.base}/assignments/${id}/review`, dto);
  }

  deleteAssignment(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/assignments/${id}`);
  }

  // ── Exam Results ──────────────────────────────────────────────────────────

  getExamResults(params?: { studentId?: string; examType?: string; page?: number; pageSize?: number }): Observable<PagedResult<ExamResult>> {
    let p = new HttpParams();
    if (params?.studentId) p = p.set('studentId', params.studentId);
    if (params?.examType)  p = p.set('examType', params.examType);
    if (params?.page)      p = p.set('page', params.page.toString());
    if (params?.pageSize)  p = p.set('pageSize', params.pageSize.toString());
    return this.http.get<PagedResult<ExamResult>>(`${this.base}/exam-results`, { params: p });
  }

  getExamResult(id: string): Observable<ExamResult> {
    return this.http.get<ExamResult>(`${this.base}/exam-results/${id}`);
  }

  getWeakSubjects(params?: { studentId?: string; lastNExams?: number }): Observable<any[]> {
    let p = new HttpParams();
    if (params?.studentId)  p = p.set('studentId', params.studentId);
    if (params?.lastNExams) p = p.set('lastNExams', params.lastNExams.toString());
    return this.http.get<any[]>(`${this.base}/exam-results/analysis/weak-subjects`, { params: p });
  }

  createExamResult(dto: { studentId?: string; examName: string; examDate: string; examType: string; notes?: string; subjectResults?: any[] }): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.base}/exam-results`, dto);
  }

  // ── Study Sessions ────────────────────────────────────────────────────────

  getStudySessions(params?: { studentId?: string; subjectId?: string; from?: string; to?: string; page?: number; pageSize?: number }): Observable<PagedResult<StudySession>> {
    let p = new HttpParams();
    if (params?.studentId) p = p.set('studentId', params.studentId);
    if (params?.subjectId) p = p.set('subjectId', params.subjectId);
    if (params?.from)      p = p.set('from', params.from);
    if (params?.to)        p = p.set('to', params.to);
    if (params?.page)      p = p.set('page', params.page.toString());
    if (params?.pageSize)  p = p.set('pageSize', params.pageSize.toString());
    return this.http.get<PagedResult<StudySession>>(`${this.base}/study-sessions`, { params: p });
  }

  getStudySessionSummary(params?: { studentId?: string; from?: string; to?: string }): Observable<any> {
    let p = new HttpParams();
    if (params?.studentId) p = p.set('studentId', params.studentId);
    if (params?.from)      p = p.set('from', params.from);
    if (params?.to)        p = p.set('to', params.to);
    return this.http.get<any>(`${this.base}/study-sessions/summary`, { params: p });
  }

  verifyStudySession(id: string): Observable<void> {
    return this.http.put<void>(`${this.base}/study-sessions/${id}/verify`, {});
  }

  // ── Subjects ──────────────────────────────────────────────────────────────

  getSubjects(params?: { examType?: string; includeCustom?: boolean }): Observable<CoachingSubject[]> {
    let p = new HttpParams();
    if (params?.examType)      p = p.set('examType', params.examType);
    if (params?.includeCustom) p = p.set('includeCustom', 'true');
    return this.http.get<CoachingSubject[]>(`${this.base}/subjects`, { params: p });
  }
}
