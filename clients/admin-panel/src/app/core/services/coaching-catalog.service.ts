import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map } from 'rxjs';
import { environment } from '../../../environments/environment';

export type CoachingCatalogKind = 'lessons' | 'units' | 'topics' | 'schools' | 'universityPrograms';
export interface CoachingCatalogRow {
  id: string;
  name: string;
  source: string;
  sourceId: string;
  isActive: boolean;
  gradeNumber?: number | null;
  examCode?: string | null;
  lessonId?: string | null;
  unitId?: string | null;
  parentId?: string | null;
  displayOrder?: number | null;
  estimatedMinutes?: number | null;
  universityName?: string | null;
  programCode?: string | null;
  scoreType?: string | null;
  minimumScore?: number | null;
  scoreYear?: number | null;
  city?: string | null;
  district?: string | null;
  provinceId?: string | null;
  districtId?: string | null;
}
export interface CoachingCatalogPage {
  items: CoachingCatalogRow[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
}
export interface CoachingCatalogFilter {
  pageNumber: number;
  pageSize: number;
  search?: string;
  source?: string;
  isActive?: boolean;
  gradeNumber?: number;
  examCode?: string;
  scoreType?: string;
  scoreYear?: number;
}

export interface CoachingCatalogUsage {
  id: string;
  name: string;
  fingerprint: string;
  canDelete: boolean;
  catalogReferences: number;
  planReferences: number;
  goalReferences: number;
  examReferences: number;
}

@Injectable({ providedIn: 'root' })
export class CoachingCatalogService {
  private readonly http = inject(HttpClient);

  usage(kind: CoachingCatalogKind, id: string) {
    return this.http.get<{ data: CoachingCatalogUsage }>(`${environment.apiUrl}/coaching-admin/catalog/${kind}/${encodeURIComponent(id)}/usage`)
      .pipe(map(response => response.data));
  }

  delete(kind: CoachingCatalogKind, id: string, request: { fingerprint: string; reason: string; confirmId: string }) {
    return this.http.delete<{ success: boolean }>(`${environment.apiUrl}/coaching-admin/catalog/${kind}/${encodeURIComponent(id)}`, { body: request });
  }

  list(kind: CoachingCatalogKind, filter: CoachingCatalogFilter) {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(filter)) {
      if (value !== undefined && value !== '') params = params.set(key, String(value));
    }
    return this.http.get<{ success: boolean; data: CoachingCatalogPage }>(
      `${environment.apiUrl}/coaching-admin/catalog/${kind}`, { params }
    ).pipe(map(response => response.data));
  }
}
