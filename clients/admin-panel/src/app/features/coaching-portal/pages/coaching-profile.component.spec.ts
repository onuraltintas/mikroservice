import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { CoachingProfileComponent } from '../../../../../shared/coaching-profile.component';
import { vi } from 'vitest';

describe('Coaching owned profile', () => {
  it('lets a student fill education fields and save them through the rendered form', async () => {
    const { fixture, component, http } = setup();
    const student = { version: 1, educationStatus: 'Student', gradeLevel: 10, shareProgressWithTeachers: true, isActive: true };
    http.expectOne('/api/coaching/profiles/me').flush({ data: { student } });
    fixture.detectChanges(); await fixture.whenStable();
    const fields = { schoolName: 'Örnek Lise', schoolCity: 'Ankara', schoolDistrict: 'Çankaya', schoolType: 'Lise', classBranch: 'A', birthDate: '2008-01-02', studentBio: 'Sınava hazırlanıyorum.', gradeLevel: '11' };
    for (const [name, value] of Object.entries(fields)) {
      const input = fixture.nativeElement.querySelector(`[name="${name}"]`) as HTMLInputElement;
      input.value = value; input.dispatchEvent(new Event('input'));
    }
    (fixture.nativeElement.querySelector('[name="shareProgress"]') as HTMLInputElement).click();
    fixture.detectChanges();
    expect(component.dirty).toBe(true);
    fixture.nativeElement.querySelector('form').dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    const request = http.expectOne('/api/coaching/profiles/me/student');
    expect(request.request.body.schoolName).toBe('Örnek Lise'); expect(request.request.body.schoolDistrict).toBe('Çankaya');
    expect(request.request.body.gradeLevel).toBe(11); expect(request.request.body.birthDate).toBe('2008-01-02');
    expect(request.request.body.shareProgressWithTeachers).toBe(false);
    request.flush({ data: { ...student, ...request.request.body, version: 2 } }); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Koçluk profiliniz kaydedildi.'); http.verify();
  });
  function setup(role: 'Student' | 'Teacher' = 'Student', invitations: object[] = []) {
    TestBed.configureTestingModule({ imports: [CoachingProfileComponent], providers: [provideHttpClient(), provideHttpClientTesting()] });
    const fixture = TestBed.createComponent(CoachingProfileComponent);
    fixture.componentRef.setInput('apiUrl', '/api'); fixture.componentRef.setInput('role', role);
    fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne('/api/coaching/invitations/my-invitations').flush(invitations);
    return { fixture, component: fixture.componentInstance, http };
  }

  it('reads without creating a profile and provisions explicitly with a product role', () => {
    const { fixture, component, http } = setup();
    http.expectOne('/api/coaching/profiles/me').flush({ data: { student: null, teacher: null, memberships: [] } });
    fixture.detectChanges();
    http.expectNone('/api/coaching/profiles/me/provision');
    component.provision();
    const request = http.expectOne('/api/coaching/profiles/me/provision');
    expect(request.request.body).toEqual({ role: 'Student' });
    request.flush({ data: { userId: 'same-account' } });
    http.expectOne('/api/coaching/profiles/me').flush({ data: { student: null, teacher: null, memberships: [] } });
    fixture.detectChanges();
    http.verify();
  });

  it('saves graduate education in Coaching with version protection and no account fields', () => {
    const { fixture, component, http } = setup();
    http.expectOne('/api/coaching/profiles/me').flush({ data: { student: { version: 3, educationStatus: 'student', gradeLevel: 12, shareProgressWithTeachers: true, isActive: true } } });
    fixture.detectChanges();
    component.student!.educationStatus = 'graduate'; component.dirty = true;
    component.save();
    const request = http.expectOne('/api/coaching/profiles/me/student');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body.expectedVersion).toBe(3);
    expect(request.request.body.gradeLevel).toBeNull();
    expect(request.request.body.userId).toBeUndefined();
    expect(request.request.body.firstName).toBeUndefined();
    request.flush({ data: { ...component.student, version: 4, gradeLevel: null } });
    expect(component.dirty).toBe(false); http.verify();
  });

  it('keeps unsaved education after a version conflict', () => {
    const { fixture, component, http } = setup();
    http.expectOne('/api/coaching/profiles/me').flush({ data: { student: { version: 1, educationStatus: 'student', gradeLevel: 10, shareProgressWithTeachers: true, isActive: true } } });
    fixture.detectChanges();
    component.student!.gradeLevel = 11; component.dirty = true; component.save();
    http.expectOne('/api/coaching/profiles/me/student').flush({ message: 'Profil değişti. Yeniden yükleyin.' }, { status: 409, statusText: 'Conflict' });
    expect(component.student!.gradeLevel).toBe(11); expect(component.dirty).toBe(true);
    expect(component.error()).toContain('Profil değişti'); http.verify();
  });

  it('saves teacher expertise in its own product profile without changing education or account roles', () => {
    const { fixture, component, http } = setup('Teacher');
    const teacher = { version: 2, subjects: ['Matematik'], title: 'Koç', isActive: true };
    http.expectOne('/api/coaching/profiles/me').flush({ data: { teacher, student: null } });
    fixture.detectChanges();
    component.subjects = 'Matematik, Fen'; component.dirty = true; component.save();
    const request = http.expectOne('/api/coaching/profiles/me/teacher');
    expect(request.request.body.subjects).toEqual(['Matematik', 'Fen']);
    expect(request.request.body.expectedVersion).toBe(2);
    expect(request.request.body.canViewAllInstitutionStudents).toBeUndefined();
    request.flush({ data: { ...teacher, version: 3, subjects: ['Matematik', 'Fen'] } });
    expect(component.subjects).toBe('Matematik, Fen'); expect(component.dirty).toBe(false); http.verify();
  });

  it('accepts a product invitation then reloads membership without writing an Identity profile', () => {
    const invite = { id: 'invite-1', inviterEmail: 'coach@example.test', type: 'StudentToTeacher', expiresAt: '2026-10-20' };
    const { fixture, component, http } = setup('Student', [invite]);
    const student = { version: 1, educationStatus: 'Student', gradeLevel: 10, shareProgressWithTeachers: true, isActive: true };
    http.expectOne('/api/coaching/profiles/me').flush({ data: { student } });
    fixture.detectChanges();
    component.respond(invite.id, 'accept');
    expect(component.working()).toBe(true);
    http.expectOne('/api/coaching/invitations/invite-1/accept').flush(null);
    http.expectOne('/api/coaching/invitations/my-invitations').flush([]);
    http.expectOne('/api/coaching/profiles/me').flush({ data: { student, memberships: [] } });
    fixture.detectChanges();
    expect(component.invitations()).toEqual([]); expect(component.working()).toBe(false); http.verify();
  });

  it('does not discard a dirty form when accepting an invitation or leaving the profile', () => {
    const { fixture, component, http } = setup();
    http.expectOne('/api/coaching/profiles/me').flush({ data: { student: { version: 1, educationStatus: 'student', gradeLevel: 10, isActive: true } } });
    fixture.detectChanges();
    component.dirty = true; component.respond('invite-1', 'accept');
    expect(component.invitationError()).toContain('profil değişikliklerini kaydedin');
    http.expectNone('/api/coaching/invitations/invite-1/accept');
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false);
    expect(component.canLeavePage()).toBe(false); component.reload();
    http.expectNone('/api/coaching/profiles/me'); confirm.mockRestore();
    const unload = new Event('beforeunload', { cancelable: true }) as BeforeUnloadEvent;
    component.beforeUnload(unload); expect(unload.defaultPrevented).toBe(true); http.verify();
  });

  it('keeps missing profile creation retryable after access failure and reports invitation loading errors', () => {
    const { fixture, component, http } = setup();
    http.expectOne('/api/coaching/profiles/me').flush({ data: { student: null, teacher: null } });
    fixture.detectChanges();
    component.provision();
    http.expectOne('/api/coaching/profiles/me/provision').flush({ message: 'Rol doğrulanamadı.' }, { status: 403, statusText: 'Forbidden' });
    expect(component.ready()).toBe(true); expect(component.working()).toBe(false); expect(component.error()).toContain('Rol doğrulanamadı');
    component.loadInvitations();
    http.expectOne('/api/coaching/invitations/my-invitations').flush({}, { status: 503, statusText: 'Unavailable' });
    expect(component.invitationError()).toContain('Davetler yüklenemedi'); http.verify();
  });
});
