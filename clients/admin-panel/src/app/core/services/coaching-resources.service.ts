import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map } from 'rxjs';
import { environment } from '../../../environments/environment';

/** KocApp uyumu (22.11.2026): what a resource's progress is counted in, chosen per resource. */
export type ResourceUnit = 'Page' | 'Test' | 'Question';
export type ResourceKind = 'QuestionBank' | 'TopicBook' | 'PracticeExams' | 'Other';

export interface StudentResourceSection { title: string; fromUnit: number; toUnit: number; topicId?: string | null; topicName?: string | null; doneUnits?: number }
export interface StudentResourceNext { fromUnit: number; toUnit: number; sectionTitle: string | null; topicId: string | null }
export interface StudentResource {
  id: string; version: number; name: string; publisher: string | null; lessonName: string | null; kind: ResourceKind; unit: ResourceUnit;
  totalUnits: number; doneBefore: number; doneUnits: number; percent: number; plannedUnits: number; isArchived: boolean; canDelete: boolean;
  createdAt: string; updatedAt: string | null; sections: StudentResourceSection[]; next: StudentResourceNext | null;
  /** The catalog lesson and the exam the resource belongs to. */
  lessonId?: string | null; examCode?: string | null;
}
export interface StudentResourceInput {
  name: string; publisher: string | null; lessonName: string | null; kind: ResourceKind; unit: ResourceUnit; totalUnits: number; doneBefore: number;
  sections: { title: string; fromUnit: number; toUnit: number; topicId?: string | null }[]; expectedVersion?: number | null;
  lessonId?: string | null; examCode?: string | null;
}
export interface ResourceCopyResult { copied: number; skipped: number }
export interface TeacherStudentChoice { userId: string; fullName: string }

/** Whose resources: the signed-in student (nothing set), a teacher's or institution's student, or a parent's child. */
export interface ResourceTarget { studentId?: string | null; parent?: boolean }

export const RESOURCE_KINDS: { value: ResourceKind; label: string }[] = [
  { value: 'QuestionBank', label: 'Soru bankası' },
  { value: 'TopicBook', label: 'Konu anlatımı' },
  { value: 'PracticeExams', label: 'Deneme seti' },
  { value: 'Other', label: 'Diğer' }
];
export const RESOURCE_UNITS: { value: ResourceUnit; label: string }[] = [
  { value: 'Page', label: 'Sayfa' }, { value: 'Test', label: 'Test' }, { value: 'Question', label: 'Soru' }
];

/** "sayfa", "test", "soru": the unit as a lower-case word (Turkish counts take the singular). */
export function resourceUnitWord(unit: ResourceUnit | string | null | undefined): string {
  return unit === 'Test' ? 'test' : unit === 'Question' ? 'soru' : 'sayfa';
}
/** "ÖABT", "YÖKDİL"; other exam codes (LGS, TYT, AYT, AGS, YDS) as they are. */
export function resourceExamLabel(code: string | null | undefined): string {
  if (!code) return '';
  if (code === 'OABT' || code.startsWith('OABT_')) return 'ÖABT';
  if (code.startsWith('YOKDIL')) return 'YÖKDİL';
  if (code.startsWith('AYT')) return 'AYT';
  return code;
}
/**
 * Sections from a lesson's topics, in their order, the units shared out as evenly as they go (the first ones get the remainder).
 * With fewer units than topics, only as many topics as units get a section.
 */
export function sectionsFromTopics(topics: { id: string; name: string }[], total: number): { title: string; fromUnit: number; toUnit: number; topicId: string }[] {
  const used = topics.slice(0, Math.max(0, Math.min(topics.length, total)));
  if (!used.length) return [];
  const size = Math.floor(total / used.length), extra = total % used.length;
  let from = 1;
  return used.map((topic, i) => {
    const to = from + size - 1 + (i < extra ? 1 : 0);
    const section = { title: topic.name.slice(0, 200), fromUnit: from, toUnit: to, topicId: topic.id };
    from = to + 1;
    return section;
  });
}
export function resourceKindLabel(kind: ResourceKind | string): string { return RESOURCE_KINDS.find(x => x.value === kind)?.label ?? 'Diğer'; }
/** "Limit Soru Bankası, test 12–15"; the name alone without a range. */
export function resourceRangeText(name: string, unit: string | null | undefined, from: number | null | undefined, to: number | null | undefined): string {
  if (from == null || to == null) return name;
  return `${name}, ${resourceUnitWord(unit)} ${from === to ? from : `${from}–${to}`}`;
}

@Injectable({ providedIn: 'root' })
export class CoachingResourcesService {
  private readonly http = inject(HttpClient);

  private url(target: ResourceTarget): string {
    const id = target.studentId ? encodeURIComponent(target.studentId) : null;
    if (id && target.parent) return `${environment.apiUrl}/coaching/parents/me/children/${id}/resources`;
    if (id) return `${environment.apiUrl}/coaching/staff/students/${id}/study/resources`;
    return `${environment.apiUrl}/coaching/study-planning/resources`;
  }

  list(target: ResourceTarget, includeArchived = false) {
    const params = includeArchived ? new HttpParams().set('includeArchived', 'true') : undefined;
    return this.http.get<{ data: StudentResource[] }>(this.url(target), { params }).pipe(map(x => x.data));
  }
  create(target: ResourceTarget, input: StudentResourceInput) {
    return this.http.post<{ data: StudentResource }>(this.url(target), input).pipe(map(x => x.data));
  }
  update(target: ResourceTarget, id: string, input: StudentResourceInput) {
    return this.http.put<{ data: StudentResource }>(`${this.url(target)}/${encodeURIComponent(id)}`, input).pipe(map(x => x.data));
  }
  archive(target: ResourceTarget, resource: Pick<StudentResource, 'id' | 'version'>, isArchived: boolean) {
    return this.http.put<{ data: StudentResource }>(`${this.url(target)}/${encodeURIComponent(resource.id)}/archive`,
      { expectedVersion: resource.version, isArchived }).pipe(map(x => x.data));
  }
  /** KocApp plan, phase 2: a teacher gives the same book to other students of hers. */
  copy(studentId: string, resource: Pick<StudentResource, 'id'>, studentIds: string[]) {
    return this.http.post<{ data: ResourceCopyResult }>(`${this.url({ studentId })}/${encodeURIComponent(resource.id)}/copy`, { studentIds }).pipe(map(x => x.data));
  }
  /** The teacher's own students (first 100), to copy a resource to. */
  teacherStudents() {
    const params = new HttpParams().set('pageNumber', '1').set('pageSize', '100');
    return this.http.get<{ items: TeacherStudentChoice[] }>(`${environment.apiUrl}/coaching/teachers/me/students`, { params }).pipe(map(x => x.items ?? []));
  }
  remove(target: ResourceTarget, resource: Pick<StudentResource, 'id' | 'version'>) {
    return this.http.delete<void>(`${this.url(target)}/${encodeURIComponent(resource.id)}`,
      { params: new HttpParams().set('expectedVersion', String(resource.version)) });
  }
}
