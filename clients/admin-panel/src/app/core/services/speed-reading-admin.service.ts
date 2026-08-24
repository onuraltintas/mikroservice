import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { environment } from '../../../environments/environment';

export interface SpeedReadingCapabilities {
  mode: 'Standalone' | 'Platform' | string;
  coachingIntegrationEnabled: boolean;
  notificationIntegrationEnabled: boolean;
  subscriptionIntegrationEnabled: boolean;
}

export interface SpeedReadingPage<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
}

export interface SpeedReadingExerciseType {
  id: string;
  name: string;
  displayName: string;
  description: string;
  iconName: string;
  colorCode: string;
  sortOrder: number;
  isActive: boolean;
  engineType: string;
  categoryId: string | null;
}

export interface SpeedReadingExerciseTypeRequest {
  name: string;
  displayName: string;
  description?: string | null;
  iconName?: string | null;
  colorCode?: string | null;
  sortOrder: number;
  isActive: boolean;
  engineType: string;
  categoryId?: string | null;
}

export interface SpeedReadingExercise {
  id: string;
  title: string;
  description: string;
  difficultyLevel: number;
  exerciseTypeId: string;
  exerciseTypeName: string;
  configurationJson: string;
  targetAgeGroupConfigurationId: string | null;
}

export interface SpeedReadingExerciseRequest {
  title: string;
  description?: string | null;
  difficultyLevel: number;
  exerciseTypeId: string;
  configurationJson: string;
  targetAgeGroupConfigurationId?: string | null;
}

export interface SpeedReadingReadingText {
  id: string;
  title: string;
  wordCount: number;
  category: string;
  difficultyLevel: number;
  language: string;
  isActive: boolean;
  exerciseId: string | null;
}

export interface SpeedReadingReadingTextRequest {
  title: string;
  content: string;
  wordCount: number;
  category: string;
  difficultyLevel: number;
  targetAgeGroupConfigurationId?: string | null;
  language: string;
  isActive: boolean;
  tags?: string | null;
  recommendedMinLevel: number;
  recommendedMaxLevel: number;
  exerciseId?: string | null;
}

export interface SpeedReadingReadingTextDetails extends SpeedReadingReadingText {
  content: string;
  targetAgeGroupConfigurationId: string | null;
  tags: string[];
  recommendedMinLevel: number;
  recommendedMaxLevel: number;
  questions: SpeedReadingReadingQuestion[];
}

export interface SpeedReadingReadingQuestion {
  id: string;
  questionText: string;
  type: number;
  bloomLevel: number;
  difficultyLevel: number;
  explanation: string | null;
  optionA: string;
  optionB: string;
  optionC: string;
  optionD: string;
  correctAnswer: string;
  orderIndex: number;
}

export interface SpeedReadingReadingQuestionRequest {
  readingTextId: string;
  questionText: string;
  type: number;
  bloomLevel: number;
  difficultyLevel: number;
  explanation?: string | null;
  optionA: string;
  optionB: string;
  optionC: string;
  optionD: string;
  correctAnswer: string;
  orderIndex: number;
}

export interface SpeedReadingReadingQuestionUpdateRequest
  extends Omit<SpeedReadingReadingQuestionRequest, 'readingTextId'> {}

export interface SpeedReadingProgramTemplate {
  id: string;
  name: string;
  description: string;
  targetAgeGroupConfigurationId: string;
  minAssessmentScore: number;
  maxAssessmentScore: number;
  weeklyPatternJson: string;
  initialDifficultyLevel: number;
  weeksPerDifficultyIncrease: number;
  maxDifficultyLevel: number;
  totalWeeks: number;
  totalDays: number;
  isActive: boolean;
  displayOrder: number;
  programType: number;
  examType: string | null;
  isAssessment: boolean;
}

export interface SpeedReadingProgramTemplateRequest {
  name: string;
  description: string;
  targetAgeGroupConfigurationId: string;
  minAssessmentScore: number;
  maxAssessmentScore: number;
  weeklyPatternJson: string;
  initialDifficultyLevel: number;
  weeksPerDifficultyIncrease: number;
  maxDifficultyLevel: number;
  totalWeeks: number;
  totalDays: number;
  isActive: boolean;
  displayOrder: number;
  programType: number;
  examType?: string | null;
  isAssessment: boolean;
}

export interface SpeedReadingLearningPathTemplate {
  id: string;
  name: string;
  targetAgeGroupConfigurationId: string | null;
  description: string | null;
  totalNodes: number;
  estimatedDays: number;
  isActive: boolean;
}

export interface SpeedReadingLearningPathTemplateRequest {
  name: string;
  targetAgeGroupConfigurationId?: string | null;
  description?: string | null;
  estimatedDays: number;
  isActive: boolean;
}

@Injectable({ providedIn: 'root' })
export class SpeedReadingAdminService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}/speed-reading`;

  getCapabilities() {
    return this.http.get<SpeedReadingCapabilities>(`${this.url}/capabilities`);
  }

  getExerciseTypes(pageNumber = 1, pageSize = 20) {
    const params = new HttpParams()
      .set('pageNumber', pageNumber)
      .set('pageSize', pageSize);

    return this.http.get<SpeedReadingPage<SpeedReadingExerciseType>>(
      `${this.url}/exercise-types`,
      { params }
    );
  }

  createExerciseType(request: SpeedReadingExerciseTypeRequest, idempotencyKey?: string) {
    return this.http.post<SpeedReadingExerciseType>(
      `${this.url}/exercise-types`,
      request,
      { headers: this.idempotencyHeaders(idempotencyKey) }
    );
  }

  updateExerciseType(
    id: string,
    request: SpeedReadingExerciseTypeRequest,
    idempotencyKey?: string
  ) {
    return this.http.put<SpeedReadingExerciseType>(
      `${this.url}/exercise-types/${id}`,
      request,
      { headers: this.idempotencyHeaders(idempotencyKey) }
    );
  }

  deleteExerciseType(id: string, idempotencyKey?: string) {
    return this.http.delete<void>(
      `${this.url}/exercise-types/${id}`,
      { headers: this.idempotencyHeaders(idempotencyKey) }
    );
  }

  getExercises(pageNumber = 1, pageSize = 50) {
    const params = new HttpParams()
      .set('pageNumber', pageNumber)
      .set('pageSize', pageSize);
    return this.http.get<SpeedReadingPage<SpeedReadingExercise>>(
      `${this.url}/exercises`,
      { params }
    );
  }

  createExercise(request: SpeedReadingExerciseRequest, idempotencyKey?: string) {
    return this.http.post<SpeedReadingExercise>(
      `${this.url}/exercises`,
      request,
      { headers: this.idempotencyHeaders(idempotencyKey) }
    );
  }

  updateExercise(id: string, request: SpeedReadingExerciseRequest, idempotencyKey?: string) {
    return this.http.put<SpeedReadingExercise>(
      `${this.url}/exercises/${id}`,
      request,
      { headers: this.idempotencyHeaders(idempotencyKey) }
    );
  }

  deleteExercise(id: string, idempotencyKey?: string) {
    return this.http.delete<void>(
      `${this.url}/exercises/${id}`,
      { headers: this.idempotencyHeaders(idempotencyKey) }
    );
  }

  getReadingTexts(exerciseId?: string) {
    let params = new HttpParams();
    if (exerciseId) {
      params = params.set('exerciseId', exerciseId);
    }
    return this.http.get<SpeedReadingReadingText[]>(
      `${this.url}/reading-texts`,
      { params }
    );
  }

  getReadingText(id: string) {
    return this.http.get<SpeedReadingReadingTextDetails>(
      `${this.url}/reading-texts/${id}`
    );
  }

  createReadingText(request: SpeedReadingReadingTextRequest, idempotencyKey?: string) {
    return this.http.post<SpeedReadingReadingText>(
      `${this.url}/reading-texts`,
      request,
      { headers: this.idempotencyHeaders(idempotencyKey) }
    );
  }

  updateReadingText(
    id: string,
    request: SpeedReadingReadingTextRequest,
    idempotencyKey?: string
  ) {
    return this.http.put<SpeedReadingReadingText>(
      `${this.url}/reading-texts/${id}`,
      request,
      { headers: this.idempotencyHeaders(idempotencyKey) }
    );
  }

  deleteReadingText(id: string, idempotencyKey?: string) {
    return this.http.delete<void>(
      `${this.url}/reading-texts/${id}`,
      { headers: this.idempotencyHeaders(idempotencyKey) }
    );
  }

  createReadingQuestion(request: SpeedReadingReadingQuestionRequest, idempotencyKey?: string) {
    return this.http.post<SpeedReadingReadingQuestion>(
      `${this.url}/reading-questions`,
      request,
      { headers: this.idempotencyHeaders(idempotencyKey) }
    );
  }

  updateReadingQuestion(
    id: string,
    request: SpeedReadingReadingQuestionUpdateRequest,
    idempotencyKey?: string
  ) {
    return this.http.put<SpeedReadingReadingQuestion>(
      `${this.url}/reading-questions/${id}`,
      request,
      { headers: this.idempotencyHeaders(idempotencyKey) }
    );
  }

  deleteReadingQuestion(id: string, idempotencyKey?: string) {
    return this.http.delete<void>(
      `${this.url}/reading-questions/${id}`,
      { headers: this.idempotencyHeaders(idempotencyKey) }
    );
  }

  getProgramTemplates() {
    return this.http.get<SpeedReadingProgramTemplate[]>(`${this.url}/program-templates/admin`);
  }

  createProgramTemplate(request: SpeedReadingProgramTemplateRequest, idempotencyKey?: string) {
    return this.http.post<SpeedReadingProgramTemplate>(
      `${this.url}/program-templates`,
      request,
      { headers: this.idempotencyHeaders(idempotencyKey) }
    );
  }

  updateProgramTemplate(
    id: string,
    request: SpeedReadingProgramTemplateRequest,
    idempotencyKey?: string
  ) {
    return this.http.put<SpeedReadingProgramTemplate>(
      `${this.url}/program-templates/${id}`,
      request,
      { headers: this.idempotencyHeaders(idempotencyKey) }
    );
  }

  deleteProgramTemplate(id: string, idempotencyKey?: string) {
    return this.http.delete<void>(
      `${this.url}/program-templates/${id}`,
      { headers: this.idempotencyHeaders(idempotencyKey) }
    );
  }

  getLearningPathTemplates() {
    return this.http.get<SpeedReadingLearningPathTemplate[]>(`${this.url}/learning-paths/templates/admin`);
  }

  createLearningPathTemplate(request: SpeedReadingLearningPathTemplateRequest, idempotencyKey?: string) {
    return this.http.post<SpeedReadingLearningPathTemplate>(
      `${this.url}/learning-paths/templates`,
      request,
      { headers: this.idempotencyHeaders(idempotencyKey) }
    );
  }

  updateLearningPathTemplate(
    id: string,
    request: SpeedReadingLearningPathTemplateRequest,
    idempotencyKey?: string
  ) {
    return this.http.put<SpeedReadingLearningPathTemplate>(
      `${this.url}/learning-paths/templates/${id}`,
      request,
      { headers: this.idempotencyHeaders(idempotencyKey) }
    );
  }

  deleteLearningPathTemplate(id: string, idempotencyKey?: string) {
    return this.http.delete<void>(
      `${this.url}/learning-paths/templates/${id}`,
      { headers: this.idempotencyHeaders(idempotencyKey) }
    );
  }

  private idempotencyHeaders(idempotencyKey?: string): HttpHeaders {
    return new HttpHeaders({
      'Idempotency-Key': idempotencyKey ?? this.createIdempotencyKey()
    });
  }

  private createIdempotencyKey(): string {
    return globalThis.crypto?.randomUUID?.()
      ?? `${Date.now()}-${Math.random().toString(36).slice(2)}`;
  }
}
