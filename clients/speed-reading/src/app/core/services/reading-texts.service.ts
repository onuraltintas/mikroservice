import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map, switchMap } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import {
  ReadingText,
  ReadingQuestion,
  QuestionType,
  CreateReadingTextDto,
  UpdateReadingTextDto,
  CreateReadingQuestionDto,
  UpdateReadingQuestionDto,
  ImportReadingTextDto,
  ShortReadingText
} from '../models/reading-text.model';

@Injectable({
  providedIn: 'root'
})
export class ReadingTextsService {
  private readonly http = inject(HttpClient);
  private apiUrl = `${environment.speedReadingApiUrl}/reading-texts`;
  private legacyApiUrl = `${environment.apiUrl}/v1/reading-texts`;



  // ...

  // Admin method: Get all reading texts
  getAllTexts(
    category?: string,
    level?: number,
    ageGroupId?: string,
    isActive?: boolean,
    searchTerm?: string
  ): Observable<ReadingText[]> {
    let params = new HttpParams();
    if (category) params = params.set('category', category);
    if (level !== undefined) params = params.set('difficultyLevel', level.toString());
    if (ageGroupId) params = params.set('targetAgeGroupId', ageGroupId);
    if (isActive !== undefined) params = params.set('isActive', isActive.toString());
    if (searchTerm) params = params.set('searchTerm', searchTerm);

    return this.http.get<CentralReadingTextSummary[]>(this.apiUrl, { params }).pipe(
      map(items => items.map(item => this.toReadingText(item)))
    );
  }

  // Get distinct levels from database
  getLevels(): Observable<number[]> {
    return this.http.get<number[]>(`${this.apiUrl}/levels`);
  }

  // Get active age groups
  getAgeGroups(): Observable<{ id: string, name: string, displayName: string }[]> {
    return this.http.get<{ id: string, name: string, displayName: string }[]>(`${environment.apiUrl}/v1/age-group-configurations/active`);
  }

  // Simplified method for compatibility - Uses exercise endpoint for students
  getReadingTexts(difficultyLevel?: number, onlyWithQuestions: boolean = false): Observable<ReadingText[]> {
    let params = new HttpParams();
    if (difficultyLevel !== undefined) {
      params = params.set('difficultyLevel', difficultyLevel.toString());
    }
    if (onlyWithQuestions) {
      params = params.set('onlyWithQuestions', 'true');
    }
    return this.http.get<CentralReadingTextSummary[]>(this.apiUrl, { params }).pipe(
      map(items => items.map(item => this.toReadingText(item)))
    );
  }

  getTextById(id: string): Observable<ReadingText> {
    return this.http.get<CentralReadingTextDetails>(`${this.apiUrl}/${id}?includeQuestions=true`).pipe(
      map(details => this.toReadingText(details))
    );
  }

  // Admin methods
  createText(dto: CreateReadingTextDto, idempotencyKey?: string): Observable<ReadingText> {
    return this.http.post<ReadingText>(this.apiUrl, this.toReadingTextRequest(dto), {
      headers: this.idempotencyHeaders(idempotencyKey)
    });
  }

  updateText(id: string, dto: UpdateReadingTextDto, idempotencyKey?: string): Observable<ReadingText> {
    return this.http.get<CentralReadingTextDetails>(`${this.apiUrl}/${id}?includeQuestions=false`).pipe(
      switchMap(existing => this.http.put<ReadingText>(`${this.apiUrl}/${id}`, this.toReadingTextRequest(dto, existing), {
        headers: this.idempotencyHeaders(idempotencyKey)
      }))
    );
  }

  deleteText(id: string, idempotencyKey?: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`, {
      headers: this.idempotencyHeaders(idempotencyKey)
    });
  }

  // Question Management
  getTextWithQuestions(readingTextId: string): Observable<ReadingText> {
    return this.getTextById(readingTextId);
  }

  createQuestion(dto: CreateReadingQuestionDto, idempotencyKey?: string): Observable<ReadingQuestion> {
    return this.http.post<ReadingQuestion>(`${environment.speedReadingApiUrl}/reading-questions`, {
      readingTextId: dto.readingTextId,
      questionText: dto.questionText,
      type: dto.type,
      bloomLevel: 1,
      difficultyLevel: 1,
      explanation: null,
      optionA: dto.optionA,
      optionB: dto.optionB,
      optionC: dto.optionC,
      optionD: dto.optionD,
      correctAnswer: dto.correctAnswer,
      orderIndex: dto.orderIndex
    }, { headers: this.idempotencyHeaders(idempotencyKey) });
  }

  updateQuestion(questionId: string, dto: UpdateReadingQuestionDto, idempotencyKey?: string): Observable<ReadingQuestion> {
    return this.getTextById(dto.readingTextId).pipe(
      switchMap(text => {
        const existing = text.readingQuestions?.find(question => question.id === questionId);
        if (!existing) {
          throw new Error(`Reading question ${questionId} was not found in text ${dto.readingTextId}`);
        }

        return this.http.put<ReadingQuestion>(`${environment.speedReadingApiUrl}/reading-questions/${questionId}`, {
          questionText: dto.questionText ?? existing.questionText,
          type: dto.type ?? existing.type ?? QuestionType.Literal,
          bloomLevel: dto.bloomLevel ?? existing.bloomLevel ?? 1,
          difficultyLevel: dto.difficultyLevel ?? existing.difficultyLevel ?? 1,
          explanation: dto.explanation ?? existing.explanation ?? null,
          optionA: dto.optionA ?? existing.optionA,
          optionB: dto.optionB ?? existing.optionB,
          optionC: dto.optionC ?? existing.optionC,
          optionD: dto.optionD ?? existing.optionD,
          correctAnswer: dto.correctAnswer ?? existing.correctAnswer,
          orderIndex: dto.orderIndex ?? existing.orderIndex
        }, { headers: this.idempotencyHeaders(idempotencyKey) });
      })
    );
  }

  deleteQuestion(questionId: string, idempotencyKey?: string): Observable<void> {
    return this.http.delete<void>(`${environment.speedReadingApiUrl}/reading-questions/${questionId}`, {
      headers: this.idempotencyHeaders(idempotencyKey)
    });
  }

  // Get categories from database
  getCategories(): Observable<string[]> {
    return this.http.get<string[]>(`${this.apiUrl}/categories`);
  }

  // Update reading text active/inactive status
  updateStatus(id: string, isActive: boolean): Observable<void> {
    return this.updateText(id, { isActive }).pipe(map(() => void 0));
  }

  // Get short texts for RSVP practice (under 200 words)
  getShortTexts(limit: number = 10): Observable<ShortReadingText[]> {
    const params = new HttpParams().set('limit', limit.toString());
    return this.http.get<ShortReadingText[]>(`${this.apiUrl}/short`, { params });
  }

  // Import functionality
  importFromCsv(file: File): Observable<any> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post(`${this.legacyApiUrl}/import/csv`, formData);
  }

  importFromExcel(file: File): Observable<any> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post(`${this.legacyApiUrl}/import/excel`, formData);
  }

  importBulk(texts: ImportReadingTextDto[]): Observable<any> {
    return this.http.post(`${this.legacyApiUrl}/import/bulk`, texts);
  }

  // Export functionality
  exportToPdf(id: string): Observable<Blob> {
    return this.http.get(`${this.legacyApiUrl}/${id}/export/pdf`, {
      responseType: 'blob'
    });
  }

  exportToDocx(id: string): Observable<Blob> {
    return this.http.get(`${this.legacyApiUrl}/${id}/export/docx`, {
      responseType: 'blob'
    });
  }

  exportMultipleToPdf(ids: string[]): Observable<Blob> {
    return this.http.post(`${this.legacyApiUrl}/export/pdf`, { ids }, {
      responseType: 'blob'
    });
  }

  exportMultipleToDocx(ids: string[]): Observable<Blob> {
    return this.http.post(`${this.legacyApiUrl}/export/docx`, { ids }, {
      responseType: 'blob'
    });
  }

  // Helper method for downloading files
  downloadFile(blob: Blob, fileName: string): void {
    const url = window.URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = fileName;
    link.click();
    window.URL.revokeObjectURL(url);
  }

  newIdempotencyKey(): string {
    return `speed-reading-${globalThis.crypto?.randomUUID?.()
      ?? `${Date.now()}-${Math.random().toString(36).slice(2)}`}`;
  }

  private idempotencyHeaders(idempotencyKey?: string): HttpHeaders {
    return new HttpHeaders({ 'Idempotency-Key': idempotencyKey ?? this.newIdempotencyKey() });
  }

  private toReadingTextRequest(
    dto: CreateReadingTextDto | UpdateReadingTextDto,
    existing?: CentralReadingTextDetails
  ): Record<string, unknown> {
    const content = dto.content ?? existing?.content ?? '';
    return {
      title: dto.title ?? existing?.title ?? '',
      content,
      wordCount: content.trim().split(/\s+/).filter(Boolean).length,
      category: dto.category ?? existing?.category ?? '',
      difficultyLevel: dto.difficultyLevel ?? existing?.difficultyLevel ?? 1,
      targetAgeGroupConfigurationId: existing?.targetAgeGroupConfigurationId ?? null,
      language: dto.language ?? existing?.language ?? 'tr',
      isActive: dto.isActive ?? existing?.isActive ?? true,
      tags: existing?.tags?.join(',') || null,
      recommendedMinLevel: existing?.recommendedMinLevel ?? 1,
      recommendedMaxLevel: existing?.recommendedMaxLevel ?? 10,
      exerciseId: existing?.exerciseId ?? null
    };
  }

  private toReadingText(value: CentralReadingTextSummary | CentralReadingTextDetails): ReadingText {
    const hasDetails = 'content' in value;
    return {
      id: value.id,
      title: value.title,
      content: hasDetails ? value.content : '',
      wordCount: value.wordCount,
      category: value.category,
      difficultyLevel: value.difficultyLevel,
      language: value.language,
      isActive: value.isActive,
      createdAt: value.createdAt ? new Date(value.createdAt) : new Date(0),
      questionCount: 'questions' in value ? value.questions.length : value.questionCount,
      estimatedMinutes: Math.ceil(value.wordCount / 200),
      readingQuestions: hasDetails ? value.questions.map(question => ({
        ...question,
        readingTextId: value.id,
        correctAnswer: question.correctAnswer ?? '',
        typeDisplay: String(question.type),
        bloomLevelDisplay: String(question.bloomLevel)
      })) : []
    };
  }
}

interface CentralReadingTextSummary {
  id: string;
  title: string;
  wordCount: number;
  category: string;
  difficultyLevel: number;
  language: string;
  isActive: boolean;
  exerciseId: string | null;
  targetAgeGroupConfigurationId: string | null;
  questionCount: number;
  createdAt: string;
  updatedAt: string | null;
}

interface CentralReadingTextDetails extends Omit<CentralReadingTextSummary, 'questionCount'> {
  content: string;
  tags: string[];
  recommendedMinLevel: number;
  recommendedMaxLevel: number;
  questions: Array<ReadingQuestion & { bloomLevel: number; difficultyLevel: number; explanation: string | null }>;
}
