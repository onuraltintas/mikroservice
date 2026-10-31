import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';

export type ParentLinkProductName = 'Coaching' | 'SpeedReading';

export interface ParentLinkProduct {
  product: ParentLinkProductName;
  studentHasAccess: boolean;
  visibleToParent: boolean;
}

/** Identity `ParentLinkView` (V1 adds `products`, always Coaching then SpeedReading). */
export interface ParentLink {
  id: string;
  parentUserId: string;
  parentName: string;
  studentUserId: string;
  studentName: string;
  relationship: string;
  status: 'Pending' | 'Verified' | string;
  requestedAt: string;
  verifiedAt: string | null;
  products?: ParentLinkProduct[];
}

export interface ParentLinkCode {
  code: string;
  expiresAt: string;
}

export interface ParentLinkVisibility {
  coaching: boolean;
  speedReading: boolean;
}

/**
 * V2 (parent panel, decision 01.11.2026): the student's parent links live in Identity (`/api/users/me/...`), shared by
 * Coaching and Speed Reading. The student decides per product what a linked parent may see.
 */
@Injectable({ providedIn: 'root' })
export class ParentLinkService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/users/me`;

  getLinks(): Observable<ParentLink[]> {
    return this.http.get<ParentLink[]>(`${this.base}/parent-links`);
  }

  createCode(): Observable<ParentLinkCode> {
    return this.http.post<ParentLinkCode>(`${this.base}/parent-link-codes`, {});
  }

  approve(id: string): Observable<void> {
    return this.http.post<void>(`${this.base}/parent-links/${encodeURIComponent(id)}/approve`, {});
  }

  revoke(id: string): Observable<void> {
    return this.http.post<void>(`${this.base}/parent-links/${encodeURIComponent(id)}/revoke`, {});
  }

  setVisibility(id: string, visibility: ParentLinkVisibility): Observable<void> {
    return this.http.put<void>(`${this.base}/parent-links/${encodeURIComponent(id)}/products`, visibility);
  }
}

/** A product is visible unless the link says otherwise (default on, decision 2). */
export function productVisible(link: ParentLink, product: ParentLinkProductName): boolean {
  return link.products?.find(item => item.product === product)?.visibleToParent ?? true;
}
