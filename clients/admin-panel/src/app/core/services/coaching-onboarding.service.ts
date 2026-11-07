import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, map, of, tap } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { environment } from '../../../environments/environment';
import { ExamLearningPurpose, LearningExamSession } from './coaching-learning-setup.service';
import type { EducationStatus } from '../../features/coaching-portal/coaching-education';

/** Paket C (#3): the student's first-login wizard (api/coaching/students/me/onboarding). */
export type OnboardingStatus = 'NotStarted' | 'InProgress' | 'Completed';
export type { EducationStatus } from '../../features/coaching-portal/coaching-education';
/** A target score in one score type; YKS may have two (TYT and the field's). */
export interface OnboardingScore { scoreType: string; score: number }
export interface OnboardingTarget {
  kind: 'school' | 'program' | 'score'; targetId: string | null; name: string; detail: string | null; minimumScore: number | null;
  scoreYear: number | null; scoreType: string | null; scores: OnboardingScore[]; canEdit: boolean;
}
export interface OnboardingAnswers {
  birthDate: string | null; homeProvinceId: string | null; homeCity: string | null; homeDistrictId: string | null; homeDistrict: string | null;
  educationStatus: EducationStatus; gradeLevel: number | null; graduationYear: number | null; schoolName: string | null;
  purpose: ExamLearningPurpose | null; primaryScoreType: string | null; foreignLanguage: string | null; targetExamSessionId: string | null;
  targetExamYear: number | null; diplomaGrade: number | null; diplomaGradeEstimated: boolean; target: OnboardingTarget | null;
  /** The primary goal was set by a teacher: the wizard cannot change it. */
  targetLocked?: boolean;
}
export interface OnboardingView {
  status: OnboardingStatus; nextStep: number; savedSteps: number[]; completedAt: string | null; academicYearStart: number;
  answers: OnboardingAnswers; examSessions: LearningExamSession[];
  /** Paket D review: the exams that fit the saved grade (the server's rule); missing on an older API (then every exam is offered). */
  allowedPurposes?: string[];
  /** E4 #18: the invitations the student accepted (the invite step's answer when the wizard is resumed); missing on an older API. */
  acceptedInvitations?: OnboardingInvitation[];
}
export interface OnboardingInvitation { inviterEmail: string | null; type: 'StudentToTeacher' | 'StudentToInstitution' | string; acceptedAt: string }
export interface OnboardingPersonalStep {
  birthDate: string; provinceId: string; districtId: string; educationStatus: EducationStatus; gradeLevel: number | null;
  graduationYear: number | null;
  /** The student's own school from the school catalog; null keeps the saved one. */
  schoolId: string | null;
  /** G4 (#43): a school typed in because it is not in the list ("Okulum listede yok"); sent only without a schoolId. */
  schoolName?: string | null;
}
export interface OnboardingExamStep {
  purpose: ExamLearningPurpose; primaryScoreType: string | null; foreignLanguage: string | null; targetExamSessionId: string | null;
  targetExamYear: number | null; diplomaGrade: number | null; diplomaGradeEstimated: boolean;
}
export interface OnboardingTargetStep { kind: 'none' | 'school' | 'program' | 'score'; targetId: string | null; scores: OnboardingScore[] | null }
/** `value` is the catalog's spelling (sent back as the filter); `label` is for display. */
export interface OnboardingOption { value: string; label?: string; count: number }
export interface OnboardingSchoolOption { id: string; name: string; district: string | null; minimumScore: number | null; scoreYear: number | null;
  /** In the student's own district (those come first). */
  inHomeDistrict?: boolean;
  /** Paket D (#7): the catalog's school type (Anadolu Lisesi, Fen Lisesi, …). */
  type?: string | null }
export interface OnboardingProgramOption {
  id: string; name: string; university: string; faculty: string | null; scoreType: string | null; programType: string | null;
  minimumScore: number | null; rank: number | null; scoreYear: number | null; educationType: string | null; scholarship: string | null; language: string | null;
}
export interface OnboardingPage<T> { items: T[]; totalCount: number; pageNumber: number; pageSize: number }
export interface OnboardingCompletion { state: OnboardingView; needsSubscription: boolean }

@Injectable({ providedIn: 'root' })
export class CoachingOnboardingService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly url = `${environment.apiUrl}/coaching/students/me/onboarding`;
  /** The last known status of one user; the guard asks the server until that user's wizard is completed. */
  private readonly known = signal<{ userId: string; status: OnboardingStatus } | null>(null);
  /** The status of the signed-in user (null after sign-out or for another user). */
  status(): OnboardingStatus | null {
    const known = this.known(); const user = this.auth.userProfile()?.id ?? null;
    return known && user && known.userId === user ? known.status : null;
  }
  private remember(status: OnboardingStatus) {
    const user = this.auth.userProfile()?.id;
    this.known.set(user ? { userId: user, status } : null);
  }
  /** Sign-out forgets the status. */
  reset() { this.known.set(null); }

  get(): Observable<OnboardingView> { return this.http.get<{ data: OnboardingView }>(this.url).pipe(map(r => r.data), tap(v => this.remember(v.status))); }
  /** Cached per user: a completed wizard is not asked again until sign-out or another user signs in. */
  currentStatus(): Observable<OnboardingStatus> {
    const known = this.status();
    return known === 'Completed' ? of(known) : this.get().pipe(map(v => v.status));
  }
  savePersonal(request: OnboardingPersonalStep) { return this.put('steps/personal', request); }
  saveExam(request: OnboardingExamStep) { return this.put('steps/exam', request); }
  saveTarget(request: OnboardingTargetStep) { return this.put('steps/target', request); }
  passStep(step: 4 | 5 | 6) { return this.put(`steps/${step}`, {}); }
  complete(): Observable<OnboardingCompletion> {
    return this.http.post<{ data: OnboardingCompletion }>(`${this.url}/complete`, {}).pipe(map(r => r.data), tap(r => this.remember(r.state.status)));
  }

  schoolCities() { return this.list<OnboardingOption[]>('options/schools/cities'); }
  schoolTypes(city: string) { return this.list<OnboardingOption[]>('options/schools/types', { city }); }
  /** `district`: the student's own district, whose schools come first. */
  schools(city: string, type: string | null, search: string, page = 1, district: string | null = null) {
    return this.list<OnboardingPage<OnboardingSchoolOption>>('options/schools', { city, type, search, page, district });
  }
  programCities(programType: string, scoreType: string | null) { return this.list<OnboardingOption[]>('options/programs/cities', { programType, scoreType }); }
  universities(programType: string, scoreType: string | null, city: string, search = '') {
    return this.list<OnboardingOption[]>('options/programs/universities', { programType, scoreType, city, search });
  }
  programs(programType: string, scoreType: string | null, city: string, university: string, search: string, page = 1) {
    return this.list<OnboardingPage<OnboardingProgramOption>>('options/programs', { programType, scoreType, city, university, search, page });
  }

  private put(path: string, body: unknown): Observable<OnboardingView> {
    return this.http.put<{ data: OnboardingView }>(`${this.url}/${path}`, body).pipe(map(r => r.data), tap(v => this.remember(v.status)));
  }
  private list<T>(path: string, query: Record<string, string | number | null> = {}): Observable<T> {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(query)) if (value !== null && value !== '') params = params.set(key, value);
    return this.http.get<{ data: T }>(`${this.url}/${path}`, { params }).pipe(map(r => r.data));
  }
}
