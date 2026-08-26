import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface VocabularyItem {
  id: string;
  word: string;
  definition: string;
  exampleSentence?: string;
  synonyms?: string;
  antonyms?: string;
  targetAgeGroupId?: string;
  targetAgeGroup?: string;
  difficultyLevel: number;
  category: string;
  createdAt?: Date;
  updatedAt?: Date;
}

export interface VocabularyPagedResult {
  items: VocabularyItem[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
}

export interface CreateVocabularyItemRequest {
  word: string;
  definition: string;
  exampleSentence?: string;
  synonyms?: string;
  antonyms?: string;
  category: string;
  difficultyLevel: number;
  targetAgeGroupId: string;
}

export interface UpdateVocabularyItemRequest {
  word: string;
  definition: string;
  exampleSentence?: string;
  synonyms?: string;
  antonyms?: string;
  category: string;
  difficultyLevel: number;
  targetAgeGroupId: string;
}

export interface UserVocabulary {
  id: string;
  userId: string;
  vocabularyItemId: string;
  status: number;
  correctAttempts: number;
  incorrectAttempts: number;
  lastReviewedAt?: Date;
  nextReviewAt?: Date;
  createdAt: Date;
  vocabularyItem?: VocabularyItem;
}

@Injectable({
  providedIn: 'root'
})
export class VocabularyService {
  private apiUrl = `${environment.speedReadingApiUrl}/vocabulary`;

  constructor(private http: HttpClient) { }

  // Admin methods
  getAllVocabularyItems(
    search?: string,
    category?: string,
    difficultyLevel?: number,
    ageGroupId?: string,
    pageNumber: number = 1,
    pageSize: number = 10
  ): Observable<VocabularyPagedResult> {
    let params = new HttpParams()
      .set('pageNumber', pageNumber.toString())
      .set('pageSize', pageSize.toString());

    if (search) {
      params = params.set('search', search);
    }
    if (category) {
      params = params.set('category', category);
    }
    if (difficultyLevel !== undefined) {
      params = params.set('difficultyLevel', difficultyLevel.toString());
    }
    if (ageGroupId) {
      params = params.set('ageGroupId', ageGroupId);
    }

    return this.http.get<VocabularyPagedResult>(this.apiUrl, { params });
  }

  getVocabularyItemById(id: string): Observable<VocabularyItem> {
    return this.http.get<VocabularyItem>(`${this.apiUrl}/${id}`);
  }

  getCategories(): Observable<string[]> {
    return this.http.get<string[]>(`${this.apiUrl}/categories`);
  }

  createVocabularyItem(request: CreateVocabularyItemRequest): Observable<VocabularyItem> {
    return this.http.post<VocabularyItem>(this.apiUrl, this.toApiRequest(request));
  }

  updateVocabularyItem(id: string, request: UpdateVocabularyItemRequest): Observable<VocabularyItem> {
    return this.http.put<VocabularyItem>(`${this.apiUrl}/${id}`, this.toApiRequest(request));
  }

  deleteVocabularyItem(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }

  // Student methods (backward compatibility)
  getVocabularyItems(ageGroupId?: string, difficultyLevel?: number): Observable<VocabularyItem[]> {
    let params = new HttpParams()
      .set('pageNumber', '1')
      .set('pageSize', '100');

    if (ageGroupId) {
      params = params.set('ageGroupId', ageGroupId);
    }
    if (difficultyLevel !== undefined) {
      params = params.set('difficultyLevel', difficultyLevel.toString());
    }

    return new Observable(observer => {
      this.http.get<VocabularyPagedResult>(this.apiUrl, { params }).subscribe({
        next: (result) => observer.next(result.items),
        error: (err) => observer.error(err),
        complete: () => observer.complete()
      });
    });
  }

  getUserVocabulary(status?: number): Observable<UserVocabulary[]> {
    let params = new HttpParams();
    if (status !== undefined) {
      params = params.set('status', status.toString());
    }
    return this.http.get<UserVocabulary[]>(`${this.apiUrl}/user`, { params });
  }

  addToUserVocabulary(vocabularyItemId: string): Observable<UserVocabulary> {
    return this.http.post<UserVocabulary>(`${this.apiUrl}/user`, { vocabularyItemId });
  }

  updateUserVocabulary(id: string, isCorrect: boolean): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/user/${id}`, { isCorrect });
  }

  getDueForReview(): Observable<UserVocabulary[]> {
    return this.http.get<UserVocabulary[]>(`${this.apiUrl}/user/due`);
  }

  // Import/Export methods
  importVocabulary(file: File): Observable<ImportVocabularyResult> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<ImportVocabularyResult>(`${this.apiUrl}/import`, formData);
  }

  exportVocabulary(category?: string, difficultyLevel?: number, ageGroupId?: string): Observable<Blob> {
    let params = new HttpParams();
    if (category) {
      params = params.set('category', category);
    }
    if (difficultyLevel !== undefined) {
      params = params.set('difficultyLevel', difficultyLevel.toString());
    }
    if (ageGroupId) {
      params = params.set('ageGroupId', ageGroupId);
    }
    return this.http.get(`${this.apiUrl}/export`, { params, responseType: 'blob' });
  }

  downloadTemplate(): Observable<Blob> {
    return this.http.get(`${this.apiUrl}/download-template`, { responseType: 'blob' });
  }

  private toApiRequest(request: CreateVocabularyItemRequest | UpdateVocabularyItemRequest): object {
    const { targetAgeGroupId, ...rest } = request;
    return {
      ...rest,
      targetAgeGroupId: targetAgeGroupId || null
    };
  }
}

export interface ImportVocabularyResult {
  successCount: number;
  failureCount: number;
  errors: string[];
}
