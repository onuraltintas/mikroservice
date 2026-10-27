import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { COACHING_INSTITUTION_SECTIONS, COACHING_TEACHER_SECTIONS } from '../../navigation/staff-location.service';
import { StaffNotificationCenterComponent } from '../../notifications/staff-notification-center.component';
import { CoachingInstitutionMessagesComponent } from './coaching-institution-messages.component';
import { CoachingInstitutionWorkspaceComponent } from './coaching-institution-workspace.component';
import { CoachingInstitutionService } from './coaching-institution.service';
import { CoachingTeacherWorkspaceComponent } from './coaching-teacher-workspace.component';

// Roadmap O3: teachers write to students and parents; the institution reviews a conversation with a recorded reason.
describe('O3 staff messages', () => {
  it('gives teachers a Messages section that a notification opens', async () => {
    expect(COACHING_TEACHER_SECTIONS).toContain('messages');
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const workspace = TestBed.runInInjectionContext(() => new CoachingTeacherWorkspaceComponent());
    expect(workspace.sectionGroups.flatMap(group => group.items).some(item => item.value === 'messages' && item.label === 'Mesajlar')).toBe(true);
    expect(await workspace.openMessageFromNotification('c1')).toBe(true);
    expect(workspace.activeSection()).toBe('messages'); expect(workspace.notificationAssignmentId()).toBe('c1');
    workspace.rememberConversation('c2');
    expect(workspace.notificationAssignmentId()).toBe('c2');
  });

  it('opens a message notification from the notification centre', () => {
    const opened = vi.fn();
    const center = { openCoachingWeekly: { emit: vi.fn() }, openCoachingAssignment: { emit: vi.fn() }, openCoachingMessage: { emit: opened } };
    StaffNotificationCenterComponent.prototype.openTarget.call(center as unknown as StaffNotificationCenterComponent, { kind: 'message', id: 'c1', label: 'Mesajı aç' });
    expect(opened).toHaveBeenCalledWith('c1');
  });

  it('is a section of the institution workspace', () => {
    expect(COACHING_INSTITUTION_SECTIONS).toContain('messages');
    TestBed.configureTestingModule({ providers: [{ provide: CoachingInstitutionService, useValue: {} }] });
    const workspace = TestBed.runInInjectionContext(() => new CoachingInstitutionWorkspaceComponent());
    expect(workspace.sectionGroups.flatMap(group => group.items).some(item => item.value === 'messages' && item.label === 'Yazışmalar')).toBe(true);
  });

  it('lists conversations without their text and reads one only with a reason', async () => {
    TestBed.configureTestingModule({ imports: [CoachingInstitutionMessagesComponent], providers: [provideHttpClient(), provideHttpClientTesting()] });
    const fixture = TestBed.createComponent(CoachingInstitutionMessagesComponent); const http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    const item = { id: 'c1', kind: 'TeacherParent', teacherId: 't1', teacherName: 'Can Öğretmen', studentId: 's1', studentName: 'Ada Yılmaz',
      counterpartId: 'p1', counterpartName: 'Ayşe Yılmaz', lastMessageAt: '2026-11-02T09:00:00Z', messageCount: 3, lastReviewedAt: null } as const;
    http.expectOne(r => r.url.endsWith('/coaching/institution/messages')).flush({ items: [item], totalCount: 1, pageNumber: 1, pageSize: 20 });
    await vi.waitFor(() => expect(fixture.componentInstance.page().items).toHaveLength(1));
    fixture.detectChanges();
    const text = () => (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text()).toContain('Can Öğretmen'); expect(text()).toContain('Ayşe Yılmaz');

    const component = fixture.componentInstance;
    component.choose({ ...item }); component.reason = 'kısa';
    await component.open();
    http.expectNone(r => r.url.endsWith('/review'));
    component.reason = 'Veli şikâyeti üzerine inceleme';
    const opening = component.open();
    const review = http.expectOne(r => r.url.endsWith('/coaching/institution/messages/c1/review'));
    expect(review.request.body).toEqual({ reason: 'Veli şikâyeti üzerine inceleme' });
    review.flush({ conversation: {}, hasOlder: false, lastInstitutionReviewAt: '2026-11-03T08:00:00Z',
      messages: [{ id: 'm1', senderUserId: 't1', senderName: 'Can Öğretmen', mine: false, body: 'Ada çok iyi çalıştı.', sentAt: '2026-11-02T09:00:00Z', readByOther: false, attachments: [] }] });
    await opening; fixture.detectChanges();
    expect(text()).toContain('Ada çok iyi çalıştı.');
  });
});
