import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { TargetPage } from './coaching-study-planning.service';

export interface LessonAnswers { lessonId: string; topicId: string | null; questionCount: number; correct: number; wrong: number; empty: number; lessonName?: string | null; topicName?: string | null }
export type StudentExamType = 'Mock' | 'Weekly' | 'Monthly' | 'LGS' | 'YKS' | 'MidTerm' | 'Final' | 'Quiz';
export interface StudentExamInput { title: string; examType: StudentExamType; examDate: string; score: number; maxScore: number; correctAnswers: number; wrongAnswers: number; emptyAnswers: number; lessons: LessonAnswers[] }
export interface StudentExam extends StudentExamInput { id: string; version: number; source: 'StudentReported' }
interface ApiResult<T> { success: boolean; data: T }

@Injectable({ providedIn: 'root' })
export class CoachingStudentExamsService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}/coaching/study-planning/exams`;
  list(pageNumber = 1) {
    const params = new HttpParams().set('pageNumber', pageNumber).set('pageSize', 20);
    return this.http.get<ApiResult<TargetPage<StudentExam>>>(this.url, { params }).pipe(map(x => x.data));
  }
  get(id: string) { return this.http.get<ApiResult<StudentExam>>(`${this.url}/${encodeURIComponent(id)}`).pipe(map(x => x.data)); }
  create(exam: StudentExamInput) { return this.http.post<ApiResult<StudentExam>>(this.url, exam).pipe(map(x => x.data)); }
  replace(id: string, expectedVersion: number, exam: StudentExamInput) {
    return this.http.put<ApiResult<StudentExam>>(`${this.url}/${encodeURIComponent(id)}`, { expectedVersion, exam }).pipe(map(x => x.data));
  }
  delete(id: string, expectedVersion: number) {
    return this.http.delete<void>(`${this.url}/${encodeURIComponent(id)}`, { params: new HttpParams().set('expectedVersion', expectedVersion) });
  }
}
