import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import { MaxkampusSession } from './session.service';

/**
 * The institution panel was written for the staff portal and asks its auth service for three things: who is signed in,
 * which products the account has, and joining Kurs Yönetimi as a teacher. On maxkampus.com the maxkampüs session answers.
 */
@Injectable()
export class MaxkampusStaffAuthAdapter {
  private readonly session = inject(MaxkampusSession);
  private readonly http = inject(HttpClient);
  readonly products = signal([{ product: 'course-management' as const, roles: [] as string[] }]);

  getCurrentUserId(): string | null { return this.session.userId(); }
  isAuthenticated(): boolean { return this.session.signedIn(); }
  getAccessToken(): string { return this.session.accessToken(); }
  async joinCourseManagement(role: 'Teacher' | 'InstitutionOwner'): Promise<void> {
    await firstValueFrom(this.http.post<void>(`${environment.apiUrl}/auth/course-management/join`, { role }));
    await this.session.refresh();
  }
}
