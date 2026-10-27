import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

/** Roadmap O3: teacher–student and teacher–parent messages. */
export type CoachingMessageRole = 'Teacher' | 'Student' | 'Parent';
export interface CoachingMessageContact { userId: string; name: string; role: CoachingMessageRole; studentId: string; studentName: string; conversationId: string | null }
export interface CoachingConversation {
  id: string; kind: 'TeacherStudent' | 'TeacherParent'; studentId: string; studentName: string; otherUserId: string; otherName: string;
  otherRole: CoachingMessageRole; lastMessageAt: string; lastMessagePreview: string | null; unreadCount: number; canSend: boolean;
}
export interface CoachingConversationPage { items: CoachingConversation[]; totalCount: number; pageNumber: number; pageSize: number }
export interface CoachingMessageFile { id: string; fileName: string; contentType: string; sizeBytes: number }
export interface CoachingMessage {
  id: string; senderUserId: string; senderName: string; mine: boolean; body: string; sentAt: string; readByOther: boolean; attachments: CoachingMessageFile[];
}
export interface CoachingConversationThread { conversation: CoachingConversation; messages: CoachingMessage[]; hasOlder: boolean; lastInstitutionReviewAt: string | null }
export interface CoachingInstitutionConversation {
  id: string; kind: 'TeacherStudent' | 'TeacherParent'; teacherId: string; teacherName: string; studentId: string; studentName: string;
  counterpartId: string; counterpartName: string; lastMessageAt: string; messageCount: number; lastReviewedAt: string | null;
}
export interface CoachingInstitutionConversationPage { items: CoachingInstitutionConversation[]; totalCount: number; pageNumber: number; pageSize: number }

export const COACHING_MESSAGE_MAX_LENGTH = 4000;
export const COACHING_MESSAGE_MAX_FILES = 5;
export const COACHING_MESSAGE_MAX_FILE_BYTES = 10 * 1024 * 1024;
export const COACHING_MESSAGE_FILE_TYPES = '.jpg,.jpeg,.png,.webp,.pdf,.doc,.docx,.md';

export const coachingMessageRoleLabel = (role: CoachingMessageRole) => role === 'Teacher' ? 'Öğretmen' : role === 'Student' ? 'Öğrenci' : 'Veli';

/** The messages API of a signed-in teacher, student or parent; `apiUrl` is the application's API root (for example `/api`). */
export class CoachingMessagesApi {
  constructor(private readonly http: HttpClient, private readonly apiUrl: string) {}
  private get url() { return `${this.apiUrl}/coaching/messages`; }
  contacts() { return this.http.get<CoachingMessageContact[]>(`${this.url}/contacts`); }
  conversations(pageNumber = 1, pageSize = 50) {
    return this.http.get<CoachingConversationPage>(`${this.url}/conversations`, { params: new HttpParams().set('pageNumber', pageNumber).set('pageSize', pageSize) });
  }
  unreadCount() { return this.http.get<{ count: number }>(`${this.url}/unread-count`); }
  start(otherUserId: string, studentId: string) { return this.http.post<CoachingConversation>(`${this.url}/conversations`, { otherUserId, studentId }); }
  thread(conversationId: string, before: string | null = null) {
    const params = before ? new HttpParams().set('before', before) : new HttpParams();
    return this.http.get<CoachingConversationThread>(`${this.url}/conversations/${encodeURIComponent(conversationId)}`, { params });
  }
  send(conversationId: string, body: string | null, attachmentIds: string[]) {
    return this.http.post<CoachingMessage>(`${this.url}/conversations/${encodeURIComponent(conversationId)}/messages`, { body, attachmentIds });
  }
  upload(conversationId: string, file: File): Observable<CoachingMessageFile> {
    return this.http.post<CoachingMessageFile>(`${this.url}/conversations/${encodeURIComponent(conversationId)}/attachments`, file, {
      params: new HttpParams().set('fileName', file.name),
      headers: new HttpHeaders({ 'Content-Type': file.type || 'application/octet-stream' })
    });
  }
  download(conversationId: string, fileId: string) {
    return this.http.get(`${this.url}/conversations/${encodeURIComponent(conversationId)}/attachments/${encodeURIComponent(fileId)}`, { responseType: 'blob' });
  }
}

/** The institution's review of its teachers' conversations. */
export class CoachingInstitutionMessagesApi {
  constructor(private readonly http: HttpClient, private readonly apiUrl: string) {}
  private get url() { return `${this.apiUrl}/coaching/institution/messages`; }
  list(pageNumber = 1) {
    return this.http.get<CoachingInstitutionConversationPage>(this.url, { params: new HttpParams().set('pageNumber', pageNumber).set('pageSize', 20) });
  }
  review(conversationId: string, reason: string) {
    return this.http.post<CoachingConversationThread>(`${this.url}/${encodeURIComponent(conversationId)}/review`, { reason });
  }
  download(conversationId: string, fileId: string) {
    return this.http.get(`${this.url}/${encodeURIComponent(conversationId)}/attachments/${encodeURIComponent(fileId)}`, { responseType: 'blob' });
  }
}

/** Saves a downloaded file with its own name. */
export function saveCoachingMessageFile(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url; link.download = fileName; link.rel = 'noopener';
  document.body.appendChild(link); link.click(); link.remove();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}

export function coachingFileSize(bytes: number): string {
  return bytes < 1024 * 1024 ? `${Math.max(1, Math.round(bytes / 1024))} KB` : `${(bytes / 1024 / 1024).toFixed(1).replace('.', ',')} MB`;
}
