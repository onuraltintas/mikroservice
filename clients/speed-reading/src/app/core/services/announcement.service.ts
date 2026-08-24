import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, BehaviorSubject, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  AnnouncementDto,
  AnnouncementDetailDto,
  AnnouncementStatsDto,
  CreateAnnouncementRequest,
  UpdateAnnouncementRequest,
  AnnouncementAudience
} from '../models/announcement.model';

@Injectable({
  providedIn: 'root'
})
export class AnnouncementService {
  private http = inject(HttpClient);
  private apiUrl = `${environment.apiUrl}/v1/announcements`;

  private unreadCountSubject = new BehaviorSubject<number>(0);
  public unreadCount$ = this.unreadCountSubject.asObservable();

  private activeAnnouncementsSubject = new BehaviorSubject<AnnouncementDto[]>([]);
  public activeAnnouncements$ = this.activeAnnouncementsSubject.asObservable();

  /**
   * Get current user's announcements
   */
  getMyAnnouncements(includeDismissed = false, onlyPinned = false): Observable<AnnouncementDto[]> {
    let params = new HttpParams();
    if (includeDismissed) params = params.set('includeDismissed', 'true');
    if (onlyPinned) params = params.set('onlyPinned', 'true');

    return this.http.get<AnnouncementDto[]>(`${this.apiUrl}/my-announcements`, { params }).pipe(
      tap(announcements => {
        this.activeAnnouncementsSubject.next(announcements);
        const unread = announcements.filter(a => !a.hasViewed && !a.hasDismissed).length;
        this.unreadCountSubject.next(unread);
      })
    );
  }

  /**
   * Get all announcements (admin only)
   */
  getAllAnnouncements(options?: {
    isActive?: boolean;
    isPinned?: boolean;
    targetAudience?: AnnouncementAudience;
    targetInstitutionId?: string;
    includeExpired?: boolean;
    take?: number;
  }): Observable<AnnouncementDetailDto[]> {
    let params = new HttpParams();

    if (options?.isActive !== undefined) params = params.set('isActive', String(options.isActive));
    if (options?.isPinned !== undefined) params = params.set('isPinned', String(options.isPinned));
    if (options?.targetAudience) params = params.set('targetAudience', String(options.targetAudience));
    if (options?.targetInstitutionId) params = params.set('targetInstitutionId', options.targetInstitutionId);
    if (options?.includeExpired) params = params.set('includeExpired', 'true');
    if (options?.take) params = params.set('take', String(options.take));

    return this.http.get<AnnouncementDetailDto[]>(this.apiUrl, { params });
  }

  /**
   * Get announcement statistics
   */
  getAnnouncementStats(id: string): Observable<AnnouncementStatsDto> {
    return this.http.get<AnnouncementStatsDto>(`${this.apiUrl}/${id}/stats`);
  }

  /**
   * Create new announcement
   */
  createAnnouncement(request: CreateAnnouncementRequest): Observable<string> {
    return this.http.post<string>(this.apiUrl, request);
  }

  /**
   * Update announcement
   */
  updateAnnouncement(id: string, request: UpdateAnnouncementRequest): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/${id}`, request);
  }

  /**
   * Delete announcement
   */
  deleteAnnouncement(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }

  /**
   * Record that user viewed announcement
   */
  recordView(id: string): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/${id}/view`, {}).pipe(
      tap(() => this.updateLocalAnnouncementState(id, { hasViewed: true }))
    );
  }

  /**
   * Record that user clicked announcement action
   */
  recordClick(id: string): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/${id}/click`, {}).pipe(
      tap(() => this.updateLocalAnnouncementState(id, { hasClicked: true }))
    );
  }

  /**
   * Dismiss announcement
   */
  dismissAnnouncement(id: string): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/${id}/dismiss`, {}).pipe(
      tap(() => {
        this.updateLocalAnnouncementState(id, { hasDismissed: true });
        this.decrementUnreadCount();
      })
    );
  }

  /**
   * Refresh announcements
   */
  refreshAnnouncements(): void {
    this.getMyAnnouncements().subscribe();
  }

  /**
   * Get unread count
   */
  getUnreadCount(): number {
    return this.unreadCountSubject.value;
  }

  /**
   * Update local announcement state
   */
  private updateLocalAnnouncementState(id: string, updates: Partial<AnnouncementDto>): void {
    const announcements = this.activeAnnouncementsSubject.value;
    const index = announcements.findIndex(a => a.id === id);

    if (index !== -1) {
      announcements[index] = { ...announcements[index], ...updates };
      this.activeAnnouncementsSubject.next([...announcements]);
    }
  }

  /**
   * Decrement unread count
   */
  private decrementUnreadCount(): void {
    const current = this.unreadCountSubject.value;
    if (current > 0) {
      this.unreadCountSubject.next(current - 1);
    }
  }
}
