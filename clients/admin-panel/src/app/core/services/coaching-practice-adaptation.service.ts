import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PracticeExamInput } from '../models/practice-exam';
import { StudyPlan, StudyTask } from './coaching-study-planning.service';

export interface TopicAnswers { sectionCode: string; topicId: string; correct: number; wrong: number; empty: number; reason: string | null; topicName?: string | null; lessonName?: string | null }
export interface TopicAnalysis { topics: TopicAnswers[]; testIdentity: string | null }
export interface AnalysisExam { examId: string; title: string; examDate: string; source: string; fingerprint: string; input: PracticeExamInput; analysis: TopicAnalysis; canEdit: boolean; sectionNames?: Record<string, string> }
export interface TopicOption { id: string; lessonId: string; lessonName: string; name: string; sectionCode: string }
export interface AdviceTopic { evidence: TopicAnswers & { appearances: number; suggestedMinutes: number }; lessonName: string; topicName: string; alreadyPlanned: boolean }
export interface PracticeAdvice { examCode: string; source: string; examIds: string[]; topics: AdviceTopic[] }
export interface AdaptationReport { exams: AnalysisExam[]; advice: PracticeAdvice[] }
export interface AdjustmentRequest { examCode: string; source: string; endDate: string; allowDeadlineExtension: boolean; topics: { topicId: string; minutes: number }[] }
export interface AdjustmentPreview {
  fingerprint: string; planId: string; expectedVersion: number; canApply: boolean; teacherApprovalRequired: boolean;
  previousDeadline: string | null; proposedDeadline: string; timeZoneId: string; message: string;
  slots: { topicId: string; lessonId: string; lessonName: string; topicName: string; date: string; startMinute: number; minutes: number; evidenceKey: string }[];
  unscheduled: { topicId: string; minutes: number }[];
  impact: { lessonName: string; previousFinish: string | null; proposedFinish: string | null; addedMinutes: number }[];
  existingTasks?: StudyTask[];
}
@Injectable({ providedIn: 'root' })
export class CoachingPracticeAdaptationService {
  private readonly http = inject(HttpClient);
  private root(student: string | null) { return student ? `${environment.apiUrl}/coaching/staff/students/${encodeURIComponent(student)}/study` : `${environment.apiUrl}/coaching/study-planning`; }
  get(student: string | null) { return this.http.get<{ data: AdaptationReport }>(`${this.root(student)}/practice-analysis`).pipe(map(x => x.data)); }
  topics(examCode: string, sectionCode: string) {
    return this.http.get<{ data: TopicOption[] }>(`${this.root(null)}/practice-topics`, { params: new HttpParams().set('examCode', examCode).set('sectionCode', sectionCode) }).pipe(map(x => x.data));
  }
  save(student: string | null, exam: AnalysisExam, analysis: TopicAnalysis) {
    return this.http.put<{ data: AnalysisExam }>(`${this.root(student)}/practice-analysis/${encodeURIComponent(exam.examId)}`, { fingerprint: exam.fingerprint, analysis }).pipe(map(x => x.data));
  }
  preview(student: string | null, request: AdjustmentRequest) {
    return this.http.post<{ data: AdjustmentPreview }>(`${this.root(student)}/practice-adjustment/preview`, request).pipe(map(x => x.data));
  }
  apply(student: string | null, fingerprint: string, preview: AdjustmentRequest, confirmed: boolean) {
    return this.http.post<{ data: StudyPlan }>(`${this.root(student)}/practice-adjustment/apply`, { fingerprint, preview, confirmed }).pipe(map(x => x.data));
  }
}
