import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';

export interface CoachingInvitation {
  id: string;
  inviterEmail: string;
  type: 'TeacherToInstitution' | 'StudentToInstitution' | 'StudentToTeacher';
  status: string;
  message?: string | null;
  expiresAt: string;
}

export interface SentCoachingInvitation {
  id: string;
  inviteeEmail: string;
  type: CoachingInvitation['type'];
  status: string;
  createdAt: string;
  expiresAt: string;
}

@Injectable({ providedIn: 'root' })
export class CoachingInvitationsService {
  private readonly http = inject(HttpClient);
  private readonly url = environment.apiUrl;

  inviteTeacher(email: string) {
    return this.http.post<{ invitationId: string }>(`${this.url}/institution/invite-teacher`, { teacherEmail: email });
  }

  inviteStudent(email: string, teacherUserId?: string) {
    return this.http.post<{ invitationId: string }>(`${this.url}/institution/invite-student`, {
      studentEmail: email, teacherUserId: teacherUserId || null
    });
  }

  inviteTeacherStudent(email: string) {
    return this.http.post<{ invitationId: string }>(`${this.url}/teachers/invite-student`, {
      studentEmail: email
    });
  }

  getMyInvitations() {
    return this.http.get<CoachingInvitation[]>(`${this.url}/invitations/my-invitations`);
  }

  getSentInvitations() {
    return this.http.get<SentCoachingInvitation[]>(`${this.url}/invitations/sent`);
  }

  accept(id: string) {
    return this.http.post<void>(`${this.url}/invitations/${encodeURIComponent(id)}/accept`, {});
  }

  reject(id: string) {
    return this.http.post<void>(`${this.url}/invitations/${encodeURIComponent(id)}/reject`, {});
  }
}
