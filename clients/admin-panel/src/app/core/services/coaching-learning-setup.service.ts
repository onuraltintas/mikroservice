import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { StudyTopic, TargetPage } from './coaching-study-planning.service';

export type LearningPurpose = 'School' | 'Lgs' | 'Yks' | 'General' | 'Undecided';
export type TopicDeclaration = 'NotStudied' | 'Studied' | 'NeedsReview';
export interface LearningCourse { lessonId: string; priority: number; selfReportedDifficulty: number | null }
export interface LearningTopic { topicId: string; declaredStatus: TopicDeclaration }
export interface LearningContext {
  id: string; version: number; academicYearStart: number; purpose: LearningPurpose; targetExamYear: number | null;
  primaryScoreType: string | null; foreignLanguage: string | null; curriculumVersionId: string | null;
  coursePreferences: LearningCourse[]; topicStates: LearningTopic[]; startingStatusReviewed: boolean;
}
export interface LearningSetupView { context: LearningContext | null; nextStep: string; isReady: boolean; missingFields: string[]; warnings: string[] }
export interface LearningSetupUpdate {
  expectedVersion: number | null; academicYearStart: number; purpose: LearningPurpose; targetExamYear: number | null;
  primaryScoreType: string | null; foreignLanguage: string | null; courses: LearningCourse[]; topics: LearningTopic[]; startingStatusReviewed: boolean;
}
export interface CurriculumVersion { id: string; version: number; code: string; name: string; versionLabel: string; validFromYear: number; validToYear: number; source: string; sourceUrl: string | null; isPublished: boolean }
export interface LearningLesson { id: string; name: string; gradeNumber: number | null; examCode: string | null; topicCount: number; missingDurationCount: number }
export interface LearningCatalog { curriculum: CurriculumVersion | null; lessons: LearningLesson[]; topics: TargetPage<StudyTopic>; missingDurationCount: number; warnings: string[] }

@Injectable({ providedIn: 'root' })
export class CoachingLearningSetupService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}/coaching/study-planning/setup`;
  get() { return this.http.get<{ data: LearningSetupView }>(this.url).pipe(map(r => r.data)); }
  save(request: LearningSetupUpdate) { return this.http.put<{ data: LearningSetupView }>(this.url, request).pipe(map(r => r.data)); }
  catalog(lessonId: string | null = null, pageNumber = 1) {
    let params = new HttpParams().set('pageNumber', pageNumber);
    if (lessonId) params = params.set('lessonId', lessonId);
    return this.http.get<{ data: LearningCatalog }>(`${this.url}/catalog`, { params }).pipe(map(r => r.data));
  }
}
