import { HttpClient, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { vi } from 'vitest';
import { CoachingMessagesApi, CoachingConversation, CoachingConversationThread } from '../../../../../shared/coaching-messages.api';
import { CoachingMessagesComponent } from '../../../../../shared/coaching-messages.component';
import { coachingNotificationTarget } from '../../../../../shared/coaching-notification-target';
import { COACHING_PORTAL_ROUTES } from '../coaching-portal.routes';
import { AuthService } from '../../../core/auth/auth.service';
import { CoachingMessagesPageComponent } from './coaching-messages-page.component';

// Roadmap O3: students and parents write to teachers; files, read receipts and closed conversations.
const conversation: CoachingConversation = {
  id: 'c1', kind: 'TeacherStudent', studentId: 's1', studentName: 'Ada Yılmaz', otherUserId: 't1', otherName: 'Can Öğretmen',
  otherRole: 'Teacher', lastMessageAt: '2026-11-02T09:00:00Z', lastMessagePreview: 'Merhaba', unreadCount: 2, canSend: true
};
const thread = (overrides: Partial<CoachingConversationThread> = {}): CoachingConversationThread => ({
  conversation, hasOlder: false, lastInstitutionReviewAt: null,
  messages: [
    { id: 'm1', senderUserId: 't1', senderName: 'Can Öğretmen', mine: false, body: 'Merhaba', sentAt: '2026-11-02T09:00:00Z', readByOther: false, attachments: [] },
    { id: 'm2', senderUserId: 's1', senderName: 'Ada Yılmaz', mine: true, body: 'Selam hocam', sentAt: '2026-11-02T09:01:00Z', readByOther: true,
      attachments: [{ id: 'f1', fileName: 'soru.pdf', contentType: 'application/pdf', sizeBytes: 2048 }] }],
  ...overrides
});

describe('O3 messages', () => {
  function setup(selectedId: string | null = null) {
    TestBed.configureTestingModule({ imports: [CoachingMessagesComponent], providers: [provideHttpClient(), provideHttpClientTesting()] });
    const fixture = TestBed.createComponent(CoachingMessagesComponent);
    fixture.componentRef.setInput('apiUrl', '/api'); fixture.componentRef.setInput('pollMs', 0);
    if (selectedId) fixture.componentRef.setInput('selectedId', selectedId);
    const http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    return { fixture, component: fixture.componentInstance, http, text: () => (fixture.nativeElement as HTMLElement).textContent ?? '' };
  }
  const flushList = (http: HttpTestingController, items = [conversation]) =>
    http.expectOne(r => r.url === '/api/coaching/messages/conversations').flush({ items, totalCount: items.length, pageNumber: 1, pageSize: 50 });

  it('lists conversations with their unread count and opens the one in the address', async () => {
    const { fixture, component, http, text } = setup('c1');
    flushList(http);
    await vi.waitFor(() => http.expectOne('/api/coaching/messages/conversations/c1').flush(thread()));
    await vi.waitFor(() => expect(component.thread()?.messages).toHaveLength(2));
    fixture.detectChanges();
    expect(text()).toContain('Can Öğretmen'); expect(text()).toContain('Selam hocam'); expect(text()).toContain('Okundu'); expect(text()).toContain('soru.pdf');
    expect(component.conversations()[0].unreadCount).toBe(0);
  });

  it('uploads the chosen files, then sends the message with them', async () => {
    const { fixture, component, http } = setup();
    flushList(http);
    const opened = vi.fn(); component.selectedIdChange.subscribe(opened);
    const opening = component.open('c1');
    http.expectOne('/api/coaching/messages/conversations/c1').flush(thread()); await opening;
    expect(opened).toHaveBeenCalledWith('c1');
    component.draft = '  Fotoğraf ekte  ';
    component.files.set([new File(['%PDF-1.4'], 'cevap.pdf', { type: 'application/pdf' })]);
    const sending = component.send();
    await vi.waitFor(() => {
      const upload = http.expectOne(r => r.url === '/api/coaching/messages/conversations/c1/attachments');
      expect(upload.request.params.get('fileName')).toBe('cevap.pdf'); expect(upload.request.headers.get('Content-Type')).toBe('application/pdf');
      upload.flush({ id: 'f9', fileName: 'cevap.pdf', contentType: 'application/pdf', sizeBytes: 8 });
    });
    await vi.waitFor(() => {
      const send = http.expectOne('/api/coaching/messages/conversations/c1/messages');
      expect(send.request.body).toEqual({ body: 'Fotoğraf ekte', attachmentIds: ['f9'] });
      send.flush({ id: 'm3', senderUserId: 's1', senderName: 'Ada Yılmaz', mine: true, body: 'Fotoğraf ekte', sentAt: '2026-11-02T10:00:00Z', readByOther: false,
        attachments: [{ id: 'f9', fileName: 'cevap.pdf', contentType: 'application/pdf', sizeBytes: 8 }] });
    });
    await sending; fixture.detectChanges();
    expect(component.thread()!.messages.at(-1)!.id).toBe('m3'); expect(component.files()).toEqual([]); expect(component.draft).toBe('');
    expect(component.conversations()[0].lastMessagePreview).toBe('Fotoğraf ekte');
  });

  it('refuses unsupported or too many files before uploading', () => {
    const { component, http } = setup(); flushList(http);
    const choose = (...files: File[]) => component.addFiles({ target: { files, value: '' } } as unknown as Event);
    choose(new File(['x'], 'kurulum.exe'));
    expect(component.error()).toContain('kurulum.exe'); expect(component.files()).toEqual([]);
    choose(...Array.from({ length: 6 }, (_, i) => new File(['x'], `foto${i}.jpg`)));
    expect(component.error()).toContain('en fazla 5'); expect(component.files()).toEqual([]);
  });

  it('shows a closed conversation as readable without a message box, and an institution review', async () => {
    const { fixture, component, http, text } = setup(); flushList(http);
    const opening = component.open('c1');
    http.expectOne('/api/coaching/messages/conversations/c1').flush(thread({ conversation: { ...conversation, canSend: false }, lastInstitutionReviewAt: '2026-11-03T08:00:00Z' }));
    await opening; fixture.detectChanges();
    expect(text()).toContain('yazışma kapandı'); expect(text()).toContain('Kurum yönetimi bu yazışmayı');
    expect((fixture.nativeElement as HTMLElement).querySelector('textarea')).toBeNull();
  });

  it('starts a conversation with a contact', async () => {
    const { component, http } = setup(); flushList(http, []);
    const picking = component.togglePicker();
    http.expectOne('/api/coaching/messages/contacts').flush([{ userId: 't1', name: 'Can Öğretmen', role: 'Teacher', studentId: 's1', studentName: 'Ada Yılmaz', conversationId: null }]);
    await picking;
    component.contactKey = 't1|s1';
    const starting = component.start();
    await vi.waitFor(() => {
      const start = http.expectOne(r => r.method === 'POST' && r.url === '/api/coaching/messages/conversations');
      expect(start.request.body).toEqual({ otherUserId: 't1', studentId: 's1' });
      start.flush({ ...conversation, unreadCount: 0 });
    });
    await vi.waitFor(() => http.expectOne('/api/coaching/messages/conversations/c1').flush(thread()));
    await starting;
    expect(component.conversations().map(x => x.id)).toEqual(['c1']); expect(component.thread()?.conversation.id).toBe('c1');
  });

  it('names a teacher for a parent by the child', () => {
    const { component, http } = setup(); flushList(http, []);
    component.viewerRole = 'Parent';
    expect(component.contactLabel({ userId: 't1', name: 'Can Öğretmen', role: 'Teacher', studentId: 's1', studentName: 'Ada Yılmaz', conversationId: null })).toBe('Can Öğretmen (Ada Yılmaz öğretmeni)');
    component.viewerRole = 'Teacher';
    expect(component.contactLabel({ userId: 'p1', name: 'Ayşe Yılmaz', role: 'Parent', studentId: 's1', studentName: 'Ada Yılmaz', conversationId: null })).toBe('Ayşe Yılmaz (Ada Yılmaz velisi)');
  });

  it('calls the messages API', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const http = TestBed.inject(HttpTestingController);
    const api = new CoachingMessagesApi(TestBed.inject(HttpClient), '/api');
    api.unreadCount().subscribe(); http.expectOne('/api/coaching/messages/unread-count');
    api.thread('c1', '2026-11-02T09:00:00Z').subscribe(); expect(http.expectOne(r => r.url === '/api/coaching/messages/conversations/c1').request.params.get('before')).toBe('2026-11-02T09:00:00Z');
    api.download('c1', 'f1').subscribe(); expect(http.expectOne('/api/coaching/messages/conversations/c1/attachments/f1').request.responseType).toBe('blob');
  });

  it('opens the conversation from a message notification for students and teachers', () => {
    const notification = { type: 'CoachingMessage', relatedEntityId: '3f2504e0-4f89-11d3-9a0c-0305e82c3301' };
    expect(coachingNotificationTarget(notification, 'student')).toEqual({ kind: 'message', id: notification.relatedEntityId, label: 'Mesajı aç' });
    expect(coachingNotificationTarget(notification, 'teacher')?.kind).toBe('message');
  });

  it('is a page for students and parents that keeps the conversation in the address', () => {
    for (const path of ['messages', 'messages/:conversationId'])
      expect(COACHING_PORTAL_ROUTES.find(route => route.path === path)?.data?.['coachingRoles']).toEqual(['Student', 'Parent']);
    TestBed.configureTestingModule({ providers: [provideRouter([]), { provide: AuthService, useValue: { userProfile: () => ({ roles: ['Parent'] }) } }] });
    const page = TestBed.runInInjectionContext(() => new CoachingMessagesPageComponent());
    expect(page.role).toBe('Parent');
  });
});

