import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, map, of, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ExamLearningPurpose, LearningExamSession } from './coaching-learning-setup.service';

/** Paket C (#3): the student's first-login wizard (api/coaching/students/me/onboarding). */
export type OnboardingStatus = 'NotStarted' | 'InProgress' | 'Completed';
export type EducationStatus = 'Unknown' | 'Student' | 'Graduate';
export interface OnboardingTarget {
  kind: 'school' | 'program' | 'score'; targetId: string | null; name: string; detail: string | null; minimumScore: number | null;
  scoreYear: number | null; scoreType: string | null; targetScore: number | null; canEdit: boolean;
}
export interface OnboardingAnswers {
  birthDate: string | null; homeProvinceId: string | null; homeCity: string | null; homeDistrictId: string | null; homeDistrict: string | null;
  educationStatus: EducationStatus; gradeLevel: number | null; graduationYear: number | null; schoolName: string | null;
  purpose: ExamLearningPurpose | null; primaryScoreType: string | null; foreignLanguage: string | null; targetExamSessionId: string | null;
  targetExamYear: number | null; diplomaGrade: number | null; diplomaGradeEstimated: boolean; target: OnboardingTarget | null;
}
export interface OnboardingView {
  status: OnboardingStatus; nextStep: number; savedSteps: number[]; completedAt: string | null; academicYearStart: number;
  answers: OnboardingAnswers; examSessions: LearningExamSession[];
}
export interface OnboardingPersonalStep {
  birthDate: string; provinceId: string; districtId: string; educationStatus: EducationStatus; gradeLevel: number | null;
  graduationYear: number | null; schoolName: string | null;
}
export interface OnboardingExamStep {
  purpose: ExamLearningPurpose; primaryScoreType: string | null; foreignLanguage: string | null; targetExamSessionId: string | null;
  targetExamYear: number | null; diplomaGrade: number | null; diplomaGradeEstimated: boolean;
}
export interface OnboardingTargetStep { kind: 'none' | 'school' | 'program' | 'score'; targetId: string | null; scoreType: string | null; targetScore: number | null }
export interface OnboardingOption { value: string; count: number }
export interface OnboardingSchoolOption { id: string; name: string; district: string | null; minimumScore: number | null; scoreYear: number | null }
export interface OnboardingProgramOption {
  id: string; name: string; university: string; faculty: string | null; scoreType: string | null; programType: string | null;
  minimumScore: number | null; rank: number | null; scoreYear: number | null; educationType: string | null; scholarship: string | null; language: string | null;
}
export interface OnboardingPage<T> { items: T[]; totalCount: number; pageNumber: number; pageSize: number }
export interface OnboardingCompletion { state: OnboardingView; needsSubscription: boolean }

@Injectable({ providedIn: 'root' })
export class CoachingOnboardingService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}/coaching/students/me/onboarding`;
  /** The last known status; the guard asks the server once per session until the wizard is completed. */
  readonly status = signal<OnboardingStatus | null>(null);

  get(): Observable<OnboardingView> { return this.http.get<{ data: OnboardingView }>(this.url).pipe(map(r => r.data), tap(v => this.status.set(v.status))); }
  /** Cached: a completed wizard is never asked again in this session. */
  currentStatus(): Observable<OnboardingStatus> {
    const known = this.status();
    return known === 'Completed' ? of(known) : this.get().pipe(map(v => v.status));
  }
  savePersonal(request: OnboardingPersonalStep) { return this.put('steps/personal', request); }
  saveExam(request: OnboardingExamStep) { return this.put('steps/exam', request); }
  saveTarget(request: OnboardingTargetStep) { return this.put('steps/target', request); }
  passStep(step: 4 | 5 | 6) { return this.put(`steps/${step}`, {}); }
  complete(): Observable<OnboardingCompletion> {
    return this.http.post<{ data: OnboardingCompletion }>(`${this.url}/complete`, {}).pipe(map(r => r.data), tap(r => this.status.set(r.state.status)));
  }

  schoolCities() { return this.list<OnboardingOption[]>('options/schools/cities'); }
  schoolTypes(city: string) { return this.list<OnboardingOption[]>('options/schools/types', { city }); }
  schools(city: string, type: string | null, search: string, page = 1) {
    return this.list<OnboardingPage<OnboardingSchoolOption>>('options/schools', { city, type, search, page });
  }
  programCities(programType: string, scoreType: string | null) { return this.list<OnboardingOption[]>('options/programs/cities', { programType, scoreType }); }
  universities(programType: string, scoreType: string | null, city: string, search = '') {
    return this.list<OnboardingOption[]>('options/programs/universities', { programType, scoreType, city, search });
  }
  programs(programType: string, scoreType: string | null, city: string, university: string, search: string, page = 1) {
    return this.list<OnboardingPage<OnboardingProgramOption>>('options/programs', { programType, scoreType, city, university, search, page });
  }

  private put(path: string, body: unknown): Observable<OnboardingView> {
    return this.http.put<{ data: OnboardingView }>(`${this.url}/${path}`, body).pipe(map(r => r.data), tap(v => this.status.set(v.status)));
  }
  private list<T>(path: string, query: Record<string, string | number | null> = {}): Observable<T> {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(query)) if (value !== null && value !== '') params = params.set(key, value);
    return this.http.get<{ data: T }>(`${this.url}/${path}`, { params }).pipe(map(r => r.data));
  }
}
