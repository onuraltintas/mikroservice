import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface StudentSession { accessToken?: string; requiresMfa?: boolean; mfaEnrollmentRequired?: boolean; mfaChallengeToken?: string; }
export interface StudentWorkspace { id: string; name: string; role: string; }
export interface StudentInvitation { id: string; workspaceName: string; role: string; }
export interface StudentCourse { id: string; name: string; subject: string; groupName: string; }
export interface StudentLesson {
  id: string; workspaceName: string; courseName: string; subject: string;
  groupName: string | null; teacherName: string | null; startsAt: string; endsAt: string;
}
export interface StudentMakeupCredit {
  id: string; workspaceId: string; sourceLessonId: string; makeupLessonId: string | null;
  reason: string; status: string; grantedAt: string; expiresAt: string;
}
export interface StudentAssignment {
  id: string; workspaceId: string; workspaceName: string; courseId: string; courseName: string; title: string;
  instructions: string | null; dueAt: string; status: string; isLate: boolean; submissionText: string | null;
  submittedAt: string | null; score: number | null; feedback: string | null; reviewedAt: string | null;
  materials: StudentAssignmentAttachment[]; submissionAttachments: StudentAssignmentAttachment[];
}
export interface StudentAssignmentAttachment {
  id: string; fileName: string; contentType: string; sizeBytes: number; purpose: string; uploadedAt: string; expiresAt: string;
}
export interface StudentAttendance {
  id: string; lessonId: string; workspaceName: string; courseName: string; subject: string;
  startsAt: string; endsAt: string; status: string; markedAt: string;
}
export interface StudentFinanceSummary {
  workspaceId: string; workspaceName: string; balance: number;
  packageBalances: { agreementId: string; grantedSessions: number; remainingSessions: number; expiresAt: string | null }[];
}
export interface StudentNotification { id: string; title: string; message: string; type: string; createdAt: string; isRead: boolean; }
interface Recaptcha { ready(callback: () => void): void; execute(siteKey: string, options: { action: string }): Promise<string>; }

@Injectable({ providedIn: 'root' })
export class CourseStudentService {
  private readonly http = inject(HttpClient);
  private token = '';
  private expiresAt = 0;
  product = '';
  userId = '';
  isAuthenticated(): boolean { return !!this.token && Date.now() < this.expiresAt; }
  private options(): { withCredentials: boolean; headers: Record<string, string> } {
    return { withCredentials: true, headers: this.isAuthenticated() ? { Authorization: `Bearer ${this.token}` } : {} };
  }

  async login(product: string, email: string, password: string): Promise<StudentSession> {
    if (!['coaching', 'speed-reading', 'course-management'].includes(product)) throw new Error('Geçersiz ürün.');
    const captcha = await this.captchaToken();
    const session = await firstValueFrom(this.http.post<StudentSession>(`/api/auth/course-management/login/${product}`, { email, password, rememberMe: false }, {
      withCredentials: true, headers: captcha ? { 'X-Auth-Recaptcha-Token': captcha } : {}
    }));
    if (!session.requiresMfa) this.applySession(session);
    return session;
  }
  async verifyMfa(challengeToken: string, code: string): Promise<void> {
    this.applySession(await firstValueFrom(this.http.post<StudentSession>('/api/auth/course-management/mfa/verify', { challengeToken, code }, { withCredentials: true })));
  }
  async restore(): Promise<boolean> {
    try {
      this.applySession(await firstValueFrom(this.http.post<StudentSession>('/api/auth/course-management/refresh-token', {}, { withCredentials: true })));
      return true;
    } catch { this.clear(); return false; }
  }
  async join(): Promise<void> {
    await firstValueFrom(this.http.post<void>('/api/auth/course-management/join', { role: 'Student' }, this.options()));
    this.applySession(await firstValueFrom(this.http.post<StudentSession>('/api/auth/course-management/session', {}, this.options())));
  }
  getInvitations() { return firstValueFrom(this.http.get<StudentInvitation[]>('/api/course-management/v1/participation/invitations', this.options())); }
  getWorkspaces() { return firstValueFrom(this.http.get<StudentWorkspace[]>('/api/course-management/v1/me/memberships', this.options())); }
  getCourses() { return firstValueFrom(this.http.get<StudentCourse[]>('/api/course-management/v1/me/courses', this.options())); }
  getLessons() { return firstValueFrom(this.http.get<StudentLesson[]>('/api/course-management/v1/me/lessons', this.options())); }
  getMakeupCredits() { return firstValueFrom(this.http.get<StudentMakeupCredit[]>('/api/course-management/v1/me/makeup-credits', this.options())); }
  getAssignments() { return firstValueFrom(this.http.get<StudentAssignment[]>('/api/course-management/v1/me/assignments', this.options())); }
  getAttendance() { return firstValueFrom(this.http.get<StudentAttendance[]>('/api/course-management/v1/me/attendance', this.options())); }
  submitAssignment(assignmentId: string, text: string) {
    return firstValueFrom(this.http.post<unknown>(`/api/course-management/v1/me/assignments/${assignmentId}/submission`, { text }, this.options()));
  }
  uploadAssignmentAttachment(assignmentId: string, file: File) {
    const contentType = this.attachmentContentType(file.name);
    return firstValueFrom(this.http.post<StudentAssignmentAttachment>(
      `/api/course-management/v1/me/assignments/${assignmentId}/attachments?fileName=${encodeURIComponent(file.name)}`,
      new Blob([file], { type: contentType }), { ...this.options(), headers: { ...this.options().headers, 'Content-Type': contentType } }));
  }
  downloadAssignmentAttachment(assignmentId: string, attachmentId: string) {
    return firstValueFrom(this.http.get(
      `/api/course-management/v1/me/assignments/${assignmentId}/attachments/${attachmentId}/content`,
      { ...this.options(), responseType: 'blob' }));
  }
  getFinanceSummary() { return firstValueFrom(this.http.get<StudentFinanceSummary[]>('/api/course-management/v1/me/finance', this.options())); }
  getNotifications() { return firstValueFrom(this.http.get<StudentNotification[]>('/api/course-management/notifications?pageNumber=1&pageSize=100', this.options())); }
  markNotificationAsRead(id: string) { return firstValueFrom(this.http.post<void>(`/api/course-management/notifications/${encodeURIComponent(id)}/mark-as-read`, {}, this.options())); }
  markAllNotificationsAsRead() { return firstValueFrom(this.http.post<void>('/api/course-management/notifications/mark-all-as-read', {}, this.options())); }
  cancelLessonParticipation(lessonId: string) {
    return firstValueFrom(this.http.post<{ lessonId: string; eligibleForCredit: boolean; creditId: string | null; creditStatus: string | null }>(
      `/api/course-management/v1/me/lessons/${lessonId}/cancel-participation`, {}, this.options()));
  }
  getProfile() { return firstValueFrom(this.http.get<{ displayName: string }>('/api/course-management/v1/me/profile', this.options())); }
  saveProfile(displayName: string) { return firstValueFrom(this.http.put<unknown>('/api/course-management/v1/me/profile', { displayName }, this.options())); }
  async accept(id: string, displayName: string): Promise<void> {
    await this.join();
    await firstValueFrom(this.http.post<unknown>(`/api/course-management/v1/participation/invitations/${id}/accept`, { displayName }, this.options()));
  }
  async logout(): Promise<void> {
    this.clear();
    await firstValueFrom(this.http.post<void>('/api/auth/course-management/revoke-token', {}, { withCredentials: true }));
  }
  clear(): void { this.token = ''; this.product = ''; this.userId = ''; this.expiresAt = 0; }
  private applySession(session: StudentSession): void {
    if (!session.accessToken) throw new Error('Oturum başlatılamadı.');
    const payload = JSON.parse(atob(session.accessToken.split('.')[1].replace(/-/g, '+').replace(/_/g, '/'))) as { sub: string; platform_product: string; exp: number };
    if (!payload.sub || !Number.isFinite(payload.exp) || !['coaching', 'speed-reading', 'course-management'].includes(payload.platform_product) || payload.exp * 1000 <= Date.now())
      throw new Error('Geçerli ürün oturumu alınamadı.');
    this.token = session.accessToken; this.product = payload.platform_product; this.userId = payload.sub; this.expiresAt = payload.exp * 1000;
  }
  private async captchaToken(): Promise<string | null> {
    const config = await firstValueFrom(this.http.get<{ enabled: boolean; siteKey: string | null }>('/api/auth/captcha-config'));
    if (!config.enabled) return null;
    if (!config.siteKey) throw new Error('Güvenlik doğrulaması yapılandırılamadı.');
    const win = window as Window & { grecaptcha?: Recaptcha };
    if (!win.grecaptcha) {
      await new Promise<void>((resolve, reject) => {
        const script = document.createElement('script');
        script.src = `https://www.google.com/recaptcha/api.js?render=${encodeURIComponent(config.siteKey!)}`;
        script.async = true; script.onload = () => resolve(); script.onerror = () => { script.remove(); reject(new Error('Güvenlik doğrulaması yüklenemedi.')); };
        document.head.appendChild(script);
      });
    }
    if (!win.grecaptcha) throw new Error('Güvenlik doğrulaması yüklenemedi.');
    const recaptcha = win.grecaptcha;
    await new Promise<void>(resolve => recaptcha.ready(resolve));
    const token = await recaptcha.execute(config.siteKey, { action: 'auth_login' });
    if (!token) throw new Error('Güvenlik doğrulaması tamamlanamadı.');
    return token;
  }
  private attachmentContentType(fileName: string): string {
    const extension = fileName.split('.').at(-1)?.toLowerCase();
    return extension === 'pdf' ? 'application/pdf' : extension === 'jpg' || extension === 'jpeg'
      ? 'image/jpeg' : extension === 'png' ? 'image/png' : 'application/octet-stream';
  }
}
