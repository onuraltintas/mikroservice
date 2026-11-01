import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map } from 'rxjs';
import { environment } from '../../../environments/environment';

/** KM7 (01.11.2026): typed client for the catalog administration API (api/coaching-admin). */
export type LinkStatus = 'pending' | 'approved' | 'rejected';
export type LinkDecision = 'approve' | 'unapprove' | 'reject';
export interface BulkSkip { id: string; kind: string; reason: string }
export interface MappingRow {
  topicId: string; topicPath: string; lessonName: string; examCode: string | null; outcomeId: string; outcomeCode: string;
  outcomeText: string; evidence: string; source: string; status: LinkStatus; reviewedAt: string | null;
}
export interface MappingPage { items: MappingRow[]; totalCount: number }
export interface MappingKey { topicId: string; outcomeId: string }
export interface MappingSkip { topicId: string; outcomeId: string; reason: string }
export interface MappingFilter { status?: LinkStatus | 'all'; examCode?: string; lessonId?: string; source?: string; search?: string; page?: number; pageSize?: number }
export interface YksScope { scoreType: string; testCode: string; examCode: string; foreignLanguage: string | null; excludedOutcomeIds: string[] }
export interface YksTestCode { code: string; examCode: string; label: string }
export interface OutcomeSourceId { source: string; sourceId: string }
export interface OutcomeSource { outcomeId: string; sourceKind: string; sourceUrl: string | null; sourceIds: OutcomeSourceId[] }
export interface OutcomeTopic { topicId: string; topicPath: string; status: LinkStatus; isActive: boolean }
export interface UniversityProgram {
  id: string; universityName: string; name: string; programCode: string | null; facultyName: string | null; programType: string | null;
  scoreType: string | null; city: string | null; quota: number | null; placed: number | null; minimumScore: number | null; maximumScore: number | null;
  rank: number | null; year: number | null; catalogYear: number | null; isActive: boolean; source: string; sourceUrl: string | null; updatedAt: string | null;
}
export interface UniversityProgramDocument { fingerprint: string; data: UniversityProgram; goalCount: number }
export interface UniversityProgramFilter { search?: string; scoreType?: string; city?: string; year?: number | null; active?: boolean | null; page?: number; pageSize?: number }
export interface UniversityProgramSave {
  reason: string; fingerprint?: string; name?: string; facultyName?: string | null; minimumScore?: number | null; maximumScore?: number | null;
  rank?: number | null; quota?: number | null; placed?: number | null; year?: number | null; isActive?: boolean;
  universityName?: string; programCode?: string | null; programType?: string | null; scoreType?: string; city?: string | null;
}
export interface Page<T> { items: T[]; totalCount: number; pageNumber: number; pageSize: number }

@Injectable({ providedIn: 'root' })
export class CoachingCatalogAdminService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/coaching-admin`;

  bulkStatus(body: { kind: string; ids: string[]; active: boolean; includeDescendants: boolean; reason: string }) {
    return this.http.post<{ data: { changed: number; skipped: BulkSkip[] } }>(`${this.base}/curriculum-management/bulk/status`, body).pipe(map(r => r.data));
  }
  mappings(filter: MappingFilter) {
    return this.http.get<{ data: MappingPage }>(`${this.base}/outcomes/mappings`, { params: params(filter) }).pipe(map(r => r.data));
  }
  mappingSources() { return this.http.get<{ data: string[] }>(`${this.base}/outcomes/mappings/sources`).pipe(map(r => r.data)); }
  reviewMappings(items: MappingKey[], decision: LinkDecision, reason: string) {
    return this.http.post<{ data: { changed: number; skipped: MappingSkip[] } }>(`${this.base}/outcomes/mappings/bulk`, { items, decision, reason }).pipe(map(r => r.data));
  }
  deleteMapping(topicId: string, outcomeId: string, reason: string) {
    return this.http.delete<{ data: { deleted: boolean } }>(`${this.base}/outcomes/mappings/${topicId}/${outcomeId}`, { body: { reason } }).pipe(map(r => r.data));
  }
  outcomeTopics(outcomeId: string) { return this.http.get<{ data: OutcomeTopic[] }>(`${this.base}/outcomes/${outcomeId}/topics`).pipe(map(r => r.data)); }
  outcomeSource(outcomeId: string) { return this.http.get<{ data: OutcomeSource }>(`${this.base}/outcomes/${outcomeId}/source`).pipe(map(r => r.data)); }
  saveOutcomeSource(outcomeId: string, body: { sourceUrl: string | null; sourceIds: OutcomeSourceId[]; reason: string }) {
    return this.http.put<{ data: OutcomeSource }>(`${this.base}/outcomes/${outcomeId}/source`, body).pipe(map(r => r.data));
  }
  yksTestCodes() { return this.http.get<{ data: YksTestCode[] }>(`${this.base}/catalog/yks-test-codes`).pipe(map(r => r.data)); }
  yksScopes(topicId: string) { return this.http.get<{ data: YksScope[] }>(`${this.base}/catalog/topics/${topicId}/yks-scopes`).pipe(map(r => r.data)); }
  saveYksScopes(topicId: string, scopes: YksScope[], reason: string) {
    return this.http.put<{ data: YksScope[] }>(`${this.base}/catalog/topics/${topicId}/yks-scopes`, { scopes, reason }).pipe(map(r => r.data));
  }
  programs(filter: UniversityProgramFilter) {
    return this.http.get<{ data: Page<UniversityProgram> }>(`${this.base}/catalog/university-programs`, { params: params(filter) }).pipe(map(r => r.data));
  }
  program(id: string) { return this.http.get<{ data: UniversityProgramDocument }>(`${this.base}/catalog/university-programs/${id}`).pipe(map(r => r.data)); }
  createProgram(body: UniversityProgramSave) { return this.http.post<{ data: UniversityProgramDocument }>(`${this.base}/catalog/university-programs`, body).pipe(map(r => r.data)); }
  updateProgram(id: string, body: UniversityProgramSave) { return this.http.put<{ data: UniversityProgramDocument }>(`${this.base}/catalog/university-programs/${id}`, body).pipe(map(r => r.data)); }
  setProgramActive(id: string, body: { fingerprint: string; isActive: boolean; reason: string }) {
    return this.http.patch<{ data: UniversityProgramDocument }>(`${this.base}/catalog/university-programs/${id}/status`, body).pipe(map(r => r.data));
  }
  deleteProgram(id: string, body: { fingerprint: string; reason: string }) {
    return this.http.delete<{ data: { deleted: boolean } }>(`${this.base}/catalog/university-programs/${id}`, { body }).pipe(map(r => r.data));
  }
}

function params(values: object): HttpParams {
  let result = new HttpParams();
  for (const [key, value] of Object.entries(values)) if (value !== undefined && value !== null && value !== '') result = result.set(key, String(value));
  return result;
}

/** The Turkish message of an API error (never a raw status text). */
export function adminErrorMessage(error: unknown, fallback = 'İşlem tamamlanamadı. Yeniden deneyin.'): string {
  if (error instanceof HttpErrorResponse) {
    const body = error.error as { message?: string; detail?: string } | null;
    if (body && typeof body === 'object' && (body.message || body.detail)) return (body.message || body.detail)!;
    if (error.status === 403) return 'Bu işlem için yetkiniz yok.';
    if (error.status === 0) return 'Sunucuya ulaşılamadı. Bağlantınızı kontrol edin.';
  }
  return fallback;
}
