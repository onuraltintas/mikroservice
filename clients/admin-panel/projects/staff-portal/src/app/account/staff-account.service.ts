import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface StaffAccountProfile {
  userId: string;
  firstName: string;
  lastName: string;
  email: string;
  phoneNumber?: string | null;
  role: string;
}

export interface UpdateStaffAccountProfileRequest {
  firstName: string;
  lastName: string;
  phoneNumber?: string | null;
}

@Injectable({ providedIn: 'root' })
export class StaffAccountService {
  private readonly http = inject(HttpClient);
  private readonly usersUrl = `${environment.apiUrl}/users`;

  getMyProfile(): Observable<StaffAccountProfile> {
    return this.http.get<StaffAccountProfile>(`${this.usersUrl}/me`);
  }

  updateMyProfile(request: UpdateStaffAccountProfileRequest): Observable<void> {
    return this.http.put<void>(`${this.usersUrl}/me`, request);
  }

  changeMyPassword(currentPassword: string, newPassword: string): Observable<void> {
    return this.http.post<void>(`${this.usersUrl}/me/change-password`, { currentPassword, newPassword });
  }
}
