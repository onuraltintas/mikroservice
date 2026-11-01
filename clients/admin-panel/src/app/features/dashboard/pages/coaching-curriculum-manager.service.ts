import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../../environments/environment';

/** KM7 (01.11.2026): API contract of the single curriculum editor (Müfredat Yönetimi). Routes live under api/coaching-admin. */
export type BulkKind = 'lessons' | 'units' | 'topics' | 'outcomes' | 'exams';
export type MappingStatus = 'pending' | 'approved' | 'rejected' | 'all';
export type MappingDecision = 'approve' | 'unapprove' | 'reject';
export type QualityIssue = 'noMinutes' | 'noOutcomes' | 'noYksScope' | 'inactiveParent' | 'pendingLinks';
export interface Skipped { id: string; kind?: string; reason: string }
export interface BulkResult { changed: number; skipped: Skipped[] }
export interface BulkStatusRequest { kind: BulkKind; ids: string[]; active: boolean; includeDescendants: boolean; reason: string }
export interface MappingRow { topicId: string; topicPath: string; lessonName: string; examCode: string; outcomeId: string; outcomeCode: string; outcomeText: string;
  evidence: string; source: string; status: Exclude<MappingStatus, 'all'> | string; reviewedAt: string | null }
export interface MappingQuery { status?: MappingStatus; examCode?: string; lessonId?: string; source?: string; search?: string; page?: number; pageSize?: number }
export interface MappingKey { topicId: string; outcomeId: string }
export interface QualityCounts { [issue: string]: number }
export interface QualityReport { counts: QualityCounts; byLesson: { lessonId: string; lessonName: string; examCode: string; counts: QualityCounts }[];
  items: { topicId: string; topicPath: string; issue: QualityIssue | string }[]; totalCount?: number }
export interface QualityQuery { examCode?: string; lessonId?: string; issue?: QualityIssue | ''; page?: number; pageSize?: number }
export interface TopicPatch { id: string; version: string | number; estimatedMinutes?: number | null; yieldWeight?: number | null }
export interface TopicPatchResult { changed: number; conflicts: { id: string }[] }
export interface OutcomeTopic { topicId: string; topicPath: string }

/** Largest batch the bulk endpoints accept; bigger selections are sent in several requests. */
export const BULK_LIMIT = 500;
export const PATCH_LIMIT = 200;

function params(query: object) {
  let p = new HttpParams();
  for (const [key, value] of Object.entries(query)) if (value !== undefined && value !== null && value !== '') p = p.set(key, String(value));
  return p;
}
const unwrap = <T>(source: Observable<{ data: T }>) => source.pipe(map(r => r.data));

@Injectable({ providedIn: 'root' })
export class CoachingCurriculumManagerService {
  private readonly http = inject(HttpClient);
  private readonly admin = `${environment.apiUrl}/coaching-admin`;
  private readonly cm = `${this.admin}/curriculum-management`;

  bulkStatus(request: BulkStatusRequest) { return unwrap(this.http.post<{ data: BulkResult }>(`${this.cm}/bulk/status`, request)); }
  patchTopics(items: TopicPatch[], reason: string) { return unwrap(this.http.patch<{ data: TopicPatchResult }>(`${this.cm}/topics/bulk`, { items, reason })); }
  quality(query: QualityQuery) { return unwrap(this.http.get<{ data: QualityReport }>(`${this.cm}/quality`, { params: params(query) })); }
  qualityCsv(query: QualityQuery) { return this.http.get(`${this.cm}/quality/export.csv`, { params: params({ ...query, page: undefined, pageSize: undefined }), responseType: 'blob' }); }
  mappings(query: MappingQuery) { return unwrap(this.http.get<{ data: { items: MappingRow[]; totalCount: number } }>(`${this.admin}/outcomes/mappings`, { params: params(query) })); }
  pendingCount() { return this.mappings({ status: 'pending', page: 1, pageSize: 1 }).pipe(map(r => r.totalCount)); }
  reviewMappings(items: MappingKey[], decision: MappingDecision, reason: string) {
    return unwrap(this.http.post<{ data: BulkResult }>(`${this.admin}/outcomes/mappings/bulk`, { items, decision, reason }));
  }
  deleteMapping(key: MappingKey, reason: string) { return this.http.delete(`${this.admin}/outcomes/mappings/${key.topicId}/${key.outcomeId}`, { body: { reason } }); }
  outcomeTopics(outcomeId: string) { return unwrap(this.http.get<{ data: OutcomeTopic[] }>(`${this.admin}/outcomes/${outcomeId}/topics`)); }
}

/** Splits a list into request-sized chunks. */
export function chunk<T>(rows: T[], size: number): T[][] {
  const out: T[][] = [];
  for (let i = 0; i < rows.length; i += size) out.push(rows.slice(i, i + size));
  return out;
}
