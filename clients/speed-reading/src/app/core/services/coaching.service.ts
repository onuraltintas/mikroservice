import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

const BASE = `${environment.apiUrl}/v1/coaching`;

// ── Models ───────────────────────────────────────────────────────────────────

export type CoachingStatus = 'Active' | 'Paused' | 'Completed' | 'Cancelled';
export type GoalLevel = 'Yearly' | 'Monthly' | 'Weekly' | 'Daily';
export type GoalStatus = 'Active' | 'Completed' | 'Failed' | 'Cancelled' | 'Paused';
export type StudyType = 'Reading' | 'Practice' | 'Review' | 'PastExam' | 'Video' | 'Other';
export type SessionStatus = 'Scheduled' | 'Completed' | 'Cancelled' | 'NoShow' | 'Rescheduled';
export type SessionType = 'Regular' | 'GoalReview' | 'ExamReview' | 'Emergency' | 'Closing';
export type AssignmentStatus = 'Pending' | 'Completed' | 'PartiallyCompleted' | 'Overdue' | 'Cancelled';
export type SnapshotType = 'Weekly' | 'Monthly';

export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

export interface CoachingRelationship {
  id: string;
  studentId: string;
  studentName?: string;
  coachId: string;
  coachName?: string;
  status: CoachingStatus;
  startDate: string;
  endDate?: string;
  initialNotes?: string;
  institutionId?: string;
  createdAt: string;
}

export interface Goal {
  id: string;
  studentId: string;
  studentName?: string;
  coachId?: string;
  parentGoalId?: string;
  level: GoalLevel;
  title: string;
  description?: string;
  targetDate: string;
  status: GoalStatus;
  completionPercentage: number;
  isCreatedByCoach: boolean;
  successCriteria?: string;
  subGoalCount: number;
}

export interface StudySession {
  id: string;
  studentId: string;
  subjectId: string;
  topicId?: string;
  date: string;
  plannedMinutes: number;
  actualMinutes: number;
  questionsSolved: number;
  correctAnswers?: number;
  wrongAnswers?: number;
  studyType: StudyType;
  notes?: string;
  isVerifiedByCoach: boolean;
  createdAt: string;
}

export interface ExamResult {
  id: string;
  studentId: string;
  studentName?: string;
  examName: string;
  examDate: string;
  examType: string;
  recordedById: string;
  isRecordedByCoach: boolean;
  totalNet?: number;
  estimatedScore?: number;
  notes?: string;
  subjectCount: number;
  createdAt: string;
}

export interface CoachingSession {
  id: string;
  coachId: string;
  coachName?: string;
  studentId: string;
  studentName?: string;
  coachingRelationshipId: string;
  scheduledAt: string;
  plannedDurationMinutes: number;
  actualDurationMinutes?: number;
  status: SessionStatus;
  sessionType: SessionType;
  sharedNotes?: string;
  coachPrivateNotes?: string;
  actionItems?: string;
  studentRating?: number;
  meetingLink?: string;
}

export interface Assignment {
  id: string;
  studentId: string;
  studentName?: string;
  assignedById: string;
  title: string;
  description?: string;
  subjectId?: string;
  topicId?: string;
  dueDate: string;
  status: AssignmentStatus;
  completionNote?: string;
  completedAt?: string;
  targetQuestions?: number;
  targetMinutes?: number;
  isApprovedByCoach?: boolean | null;
  coachFeedback?: string;
  createdAt: string;
}

export interface WeakSubjectAnalysis {
  subjectId: string;
  subjectName: string;
  dataPoints: { examDate: string; net: number; targetNet?: number }[];
  averageNet: number;
  targetNet: number;
  deficit: number;
  trend: 'improving' | 'declining' | 'stable';
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
  assignmentsSubmitted: { id: string; title: string; status: string; isApprovedByCoach?: boolean | null; completionNote?: string }[];
  examsEntered: { id: string; examName: string; examDate: string; totalNet?: number }[];
  alertFlags: string[];
}

export interface AtRiskStudent {
  studentId: string;
  studentName?: string;
  lastStudyDate?: string;
  daysSinceLastStudy?: number;
}

export interface Subject {
  id: string;
  name: string;
  examType: string;
  isSystem: boolean;
  color?: string;
  sortOrder: number;
  totalQuestionsInExam?: number;
  topics: Topic[];
}

export interface Topic {
  id: string;
  name: string;
  orderIndex: number;
  isSystem: boolean;
  isActive: boolean;
}

export interface StudentSnapshot {
  id: string;
  studentId: string;
  coachId?: string;
  snapshotType: SnapshotType;
  periodStart: string;
  periodEnd: string;
  totalStudyMinutes: number;
  totalQuestionsSolved: number;
  totalCorrect: number;
  totalStudySessions: number;
  goalsCompleted: number;
  goalsTotal: number;
  assignmentsCompleted: number;
  assignmentsTotal: number;
  examsRecorded: number;
  averageNetChange?: number;
  coachSummaryNote?: string;
}

// ── Service ──────────────────────────────────────────────────────────────────

@Injectable({ providedIn: 'root' })
export class CoachingService {
  private http = inject(HttpClient);

  // ── Relationships ─────────────────────────────────────────────────────────
  getRelationships(params: { status?: string; studentId?: string; page?: number; pageSize?: number } = {}): Observable<PagedResult<CoachingRelationship>> {
    let p = new HttpParams()
      .set('page', params.page ?? 1)
      .set('pageSize', params.pageSize ?? 20);
    if (params.status) p = p.set('status', params.status);
    if (params.studentId) p = p.set('studentId', params.studentId);
    return this.http.get<PagedResult<CoachingRelationship>>(`${BASE}/relationships`, { params: p });
  }

  createRelationship(data: any): Observable<any> {
    return this.http.post(`${BASE}/relationships`, data);
  }

  updateRelationshipStatus(id: string, data: any): Observable<any> {
    return this.http.put(`${BASE}/relationships/${id}/status`, data);
  }

  deleteRelationship(id: string): Observable<any> {
    return this.http.delete(`${BASE}/relationships/${id}`);
  }

  getAtRiskStudents(inactiveDays = 7): Observable<AtRiskStudent[]> {
    return this.http.get<AtRiskStudent[]>(`${BASE}/relationships/at-risk-students`, {
      params: new HttpParams().set('inactiveDays', inactiveDays)
    });
  }

  // ── Goals ─────────────────────────────────────────────────────────────────
  getGoals(params: { studentId?: string; level?: string; status?: string; page?: number; pageSize?: number } = {}): Observable<PagedResult<Goal>> {
    let p = new HttpParams()
      .set('page', params.page ?? 1)
      .set('pageSize', params.pageSize ?? 20);
    if (params.studentId) p = p.set('studentId', params.studentId);
    if (params.level) p = p.set('level', params.level);
    if (params.status) p = p.set('status', params.status);
    return this.http.get<PagedResult<Goal>>(`${BASE}/goals`, { params: p });
  }

  createGoal(data: any): Observable<any> {
    return this.http.post(`${BASE}/goals`, data);
  }

  checkInGoal(id: string, data: { newPercentage: number; note?: string }): Observable<any> {
    return this.http.post(`${BASE}/goals/${id}/check-in`, data);
  }

  deleteGoal(id: string): Observable<any> {
    return this.http.delete(`${BASE}/goals/${id}`);
  }

  // ── Study Sessions ────────────────────────────────────────────────────────
  getStudySessions(params: { studentId?: string; subjectId?: string; studyType?: string; from?: string; to?: string; page?: number; pageSize?: number } = {}): Observable<PagedResult<StudySession>> {
    let p = new HttpParams()
      .set('page', params.page ?? 1)
      .set('pageSize', params.pageSize ?? 30);
    if (params.studentId) p = p.set('studentId', params.studentId);
    if (params.subjectId) p = p.set('subjectId', params.subjectId);
    if (params.studyType) p = p.set('studyType', params.studyType);
    if (params.from) p = p.set('from', params.from);
    if (params.to) p = p.set('to', params.to);
    return this.http.get<PagedResult<StudySession>>(`${BASE}/study-sessions`, { params: p });
  }

  createStudySession(data: any): Observable<any> {
    return this.http.post(`${BASE}/study-sessions`, data);
  }

  verifyStudySession(id: string): Observable<any> {
    return this.http.put(`${BASE}/study-sessions/${id}/verify`, {});
  }

  deleteStudySession(id: string): Observable<any> {
    return this.http.delete(`${BASE}/study-sessions/${id}`);
  }

  // ── Exam Results ──────────────────────────────────────────────────────────
  getExamResults(params: { studentId?: string; examType?: string; from?: string; to?: string; page?: number; pageSize?: number } = {}): Observable<PagedResult<ExamResult>> {
    let p = new HttpParams()
      .set('page', params.page ?? 1)
      .set('pageSize', params.pageSize ?? 20);
    if (params.studentId) p = p.set('studentId', params.studentId);
    if (params.examType) p = p.set('examType', params.examType);
    if (params.from) p = p.set('from', params.from);
    if (params.to) p = p.set('to', params.to);
    return this.http.get<PagedResult<ExamResult>>(`${BASE}/exam-results`, { params: p });
  }

  getExamResultDetail(id: string): Observable<any> {
    return this.http.get(`${BASE}/exam-results/${id}`);
  }

  createExamResult(data: any): Observable<any> {
    return this.http.post(`${BASE}/exam-results`, data);
  }

  deleteExamResult(id: string): Observable<any> {
    return this.http.delete(`${BASE}/exam-results/${id}`);
  }

  getWeakSubjectAnalysis(params: { studentId?: string; examType?: string; lastN?: number } = {}): Observable<WeakSubjectAnalysis[]> {
    let p = new HttpParams();
    if (params.studentId) p = p.set('studentId', params.studentId);
    if (params.examType) p = p.set('examType', params.examType);
    if (params.lastN) p = p.set('lastN', params.lastN);
    return this.http.get<WeakSubjectAnalysis[]>(`${BASE}/exam-results/analysis/weak-subjects`, { params: p });
  }

  // ── Coaching Sessions ─────────────────────────────────────────────────────
  getCoachingSessions(params: { studentId?: string; coachId?: string; status?: string; page?: number; pageSize?: number } = {}): Observable<PagedResult<CoachingSession>> {
    let p = new HttpParams()
      .set('page', params.page ?? 1)
      .set('pageSize', params.pageSize ?? 20);
    if (params.studentId) p = p.set('studentId', params.studentId);
    if (params.coachId) p = p.set('coachId', params.coachId);
    if (params.status) p = p.set('status', params.status);
    return this.http.get<PagedResult<CoachingSession>>(`${BASE}/sessions`, { params: p });
  }

  createCoachingSession(data: any): Observable<any> {
    return this.http.post(`${BASE}/sessions`, data);
  }

  updateCoachingSession(id: string, data: any): Observable<any> {
    return this.http.put(`${BASE}/sessions/${id}`, data);
  }

  deleteCoachingSession(id: string): Observable<any> {
    return this.http.delete(`${BASE}/sessions/${id}`);
  }

  getSessionBriefing(sessionId: string): Observable<SessionBriefing> {
    return this.http.get<SessionBriefing>(`${BASE}/sessions/${sessionId}/briefing`);
  }

  createAssignmentFromSession(sessionId: string, data: any): Observable<any> {
    return this.http.post(`${BASE}/sessions/${sessionId}/create-assignment`, data);
  }

  // ── Assignments ───────────────────────────────────────────────────────────
  getAssignments(params: { studentId?: string; status?: string; pendingReview?: boolean; page?: number; pageSize?: number } = {}): Observable<PagedResult<Assignment>> {
    let p = new HttpParams()
      .set('page', params.page ?? 1)
      .set('pageSize', params.pageSize ?? 20);
    if (params.studentId) p = p.set('studentId', params.studentId);
    if (params.status) p = p.set('status', params.status);
    if (params.pendingReview) p = p.set('pendingReview', 'true');
    return this.http.get<PagedResult<Assignment>>(`${BASE}/assignments`, { params: p });
  }

  createAssignment(data: any): Observable<any> {
    return this.http.post(`${BASE}/assignments`, data);
  }

  updateAssignment(id: string, data: any): Observable<any> {
    return this.http.put(`${BASE}/assignments/${id}`, data);
  }

  reviewAssignment(id: string, data: { isApproved: boolean; coachFeedback?: string }): Observable<any> {
    return this.http.put(`${BASE}/assignments/${id}/review`, data);
  }

  deleteAssignment(id: string): Observable<any> {
    return this.http.delete(`${BASE}/assignments/${id}`);
  }

  // ── Subjects ──────────────────────────────────────────────────────────────
  getSubjects(params: { examType?: string; includeCustom?: boolean } = {}): Observable<Subject[]> {
    let p = new HttpParams();
    if (params.examType) p = p.set('examType', params.examType);
    if (params.includeCustom !== undefined) p = p.set('includeCustom', params.includeCustom);
    return this.http.get<Subject[]>(`${BASE}/subjects`, { params: p });
  }

  createSubject(data: any): Observable<any> {
    return this.http.post(`${BASE}/subjects`, data);
  }

  createTopic(subjectId: string, data: any): Observable<any> {
    return this.http.post(`${BASE}/subjects/${subjectId}/topics`, data);
  }

  // ── Snapshots ─────────────────────────────────────────────────────────────
  getSnapshots(params: { studentId?: string; type?: string; snapshotType?: string; page?: number; pageSize?: number } = {}): Observable<PagedResult<StudentSnapshot>> {
    let p = new HttpParams()
      .set('page', params.page ?? 1)
      .set('pageSize', params.pageSize ?? 12);
    if (params.studentId) p = p.set('studentId', params.studentId);
    const snapType = params.snapshotType ?? params.type;
    if (snapType) p = p.set('snapshotType', snapType);
    return this.http.get<PagedResult<StudentSnapshot>>(`${BASE}/snapshots`, { params: p });
  }

  generateSnapshot(data: { studentId: string; snapshotType: string; periodStart: string; coachNote?: string }): Observable<any> {
    return this.http.post(`${BASE}/snapshots/generate`, data);
  }
}
