import { ToasterService } from '../../../core/services/toaster.service';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { CoachingProfileComponent } from '../../../../../shared/coaching-profile.component';
import { vi } from 'vitest';

describe('Coaching owned profile', () => {
  it('lets a student fill education fields and save them through the rendered form', async () => {
    const { fixture, component, http } = await setup();
    const student = { version: 1, educationStatus: 'Student', gradeLevel: 10, shareProgressWithTeachers: true, isActive: true };
    http.expectOne('/api/coaching/profiles/me').flush({ data: { student } });
    fixture.detectChanges(); await fixture.whenStable();
    const fields = { schoolName: 'Örnek Lise', schoolCity: 'Ankara', schoolDistrict: 'Çankaya', schoolType: 'Lise', classBranch: 'A', birthDate: '2008-01-02', studentBio: 'Sınava hazırlanıyorum.', gradeLevel: '11' };
    for (const [name, value] of Object.entries(fields)) {
      const input = fixture.nativeElement.querySelector(`[name="${name}"]`) as HTMLInputElement;
      input.value = value; input.dispatchEvent(new Event('input'));
    }
    (fixture.nativeElement.querySelector('[name="shareProgress"]') as HTMLInputElement).click();
    fixture.detectChanges(); await fixture.whenStable();
    expect(component.dirty).toBe(true);
    fixture.nativeElement.querySelector('form').dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    const request = http.expectOne('/api/coaching/profiles/me/student');
    expect(request.request.body.schoolName).toBe('Örnek Lise'); expect(request.request.body.schoolDistrict).toBe('Çankaya');
    expect(request.request.body.gradeLevel).toBe(11); expect(request.request.body.birthDate).toBe('2008-01-02');
    expect(request.request.body.shareProgressWithTeachers).toBe(false);
    request.flush({ data: { ...student, ...request.request.body, version: 2 } }); fixture.detectChanges(); await fixture.whenStable();
    expect(fixture.nativeElement.textContent).toContain('Profilin kaydedildi.'); http.verify();
  });
  it('speaks to students with "sen" and to teachers with "siz" (T7)', async () => {
    const student = await setup('Student');
    student.http.expectOne('/api/coaching/profiles/me').flush({ data: { student: null, teacher: null } });
    student.fixture.detectChanges(); await student.fixture.whenStable();
    expect(student.fixture.nativeElement.textContent).toContain('Koçluk profilin henüz oluşturulmamış.');
    expect(student.fixture.nativeElement.textContent).toContain('Bekleyen koçluk davetin yok.');
    expect(student.fixture.nativeElement.textContent).not.toMatch(/niz\b|nız\b|bilirsiniz/);
    TestBed.resetTestingModule();
    const teacher = await setup('Teacher');
    teacher.http.expectOne('/api/coaching/profiles/me').flush({ data: { student: null, teacher: null } });
    teacher.fixture.detectChanges(); await teacher.fixture.whenStable();
    expect(teacher.fixture.nativeElement.textContent).toContain('Koçluk profiliniz henüz oluşturulmamış.');
  });
  async function setup(role: 'Student' | 'Teacher' = 'Student', invitations: object[] = []) {
    TestBed.configureTestingModule({ imports: [CoachingProfileComponent], providers: [provideHttpClient(), provideHttpClientTesting()] });
    const fixture = TestBed.createComponent(CoachingProfileComponent);
    fixture.componentRef.setInput('apiUrl', '/api'); fixture.componentRef.setInput('role', role);
    fixture.detectChanges(); await fixture.whenStable();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne('/api/coaching/invitations/my-invitations').flush(invitations);
    return { fixture, component: fixture.componentInstance, http };
  }

  it('reads without creating a profile and provisions explicitly with a product role', async () => {
    const { fixture, component, http } = await setup();
    http.expectOne('/api/coaching/profiles/me').flush({ data: { student: null, teacher: null, memberships: [] } });
    fixture.detectChanges(); await fixture.whenStable();
    http.expectNone('/api/coaching/profiles/me/provision');
    component.provision();
    const request = http.expectOne('/api/coaching/profiles/me/provision');
    expect(request.request.body).toEqual({ role: 'Student' });
    request.flush({ data: { userId: 'same-account' } });
    http.expectOne('/api/coaching/profiles/me').flush({ data: { student: null, teacher: null, memberships: [] } });
    fixture.detectChanges(); await fixture.whenStable();
    http.verify();
  });

  it('saves graduate education in Coaching with version protection and no account fields', async () => {
    const { fixture, component, http } = await setup();
    http.expectOne('/api/coaching/profiles/me').flush({ data: { student: { version: 3, educationStatus: 'student', gradeLevel: 12, shareProgressWithTeachers: true, isActive: true } } });
    fixture.detectChanges(); await fixture.whenStable();
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

  it('keeps unsaved education after a version conflict', async () => {
    const { fixture, component, http } = await setup();
    http.expectOne('/api/coaching/profiles/me').flush({ data: { student: { version: 1, educationStatus: 'student', gradeLevel: 10, shareProgressWithTeachers: true, isActive: true } } });
    fixture.detectChanges(); await fixture.whenStable();
    component.student!.gradeLevel = 11; component.dirty = true; component.save();
    http.expectOne('/api/coaching/profiles/me/student').flush({ message: 'Profil değişti. Yeniden yükleyin.' }, { status: 409, statusText: 'Conflict' });
    expect(component.student!.gradeLevel).toBe(11); expect(component.dirty).toBe(true);
    expect(component.error()).toContain('Profil değişti'); http.verify();
  });

  it('saves teacher expertise in its own product profile without changing education or account roles', async () => {
    const { fixture, component, http } = await setup('Teacher');
    const teacher = { version: 2, subjects: ['Matematik'], title: 'Koç', isActive: true };
    http.expectOne('/api/coaching/profiles/me').flush({ data: { teacher, student: null } });
    fixture.detectChanges(); await fixture.whenStable();
    component.subjects = 'Matematik, Fen'; component.dirty = true; component.save();
    const request = http.expectOne('/api/coaching/profiles/me/teacher');
    expect(request.request.body.subjects).toEqual(['Matematik', 'Fen']);
    expect(request.request.body.expectedVersion).toBe(2);
    expect(request.request.body.canViewAllInstitutionStudents).toBeUndefined();
    request.flush({ data: { ...teacher, version: 3, subjects: ['Matematik', 'Fen'] } });
    expect(component.subjects).toBe('Matematik, Fen'); expect(component.dirty).toBe(false); http.verify();
  });

  it('accepts a product invitation then reloads membership without writing an Identity profile', async () => {
    const invite = { id: 'invite-1', inviterEmail: 'coach@example.test', type: 'StudentToTeacher', expiresAt: '2026-10-20' };
    const { fixture, component, http } = await setup('Student', [invite]);
    const student = { version: 1, educationStatus: 'Student', gradeLevel: 10, shareProgressWithTeachers: true, isActive: true };
    http.expectOne('/api/coaching/profiles/me').flush({ data: { student } });
    fixture.detectChanges(); await fixture.whenStable();
    component.respond(invite.id, 'accept');
    expect(component.working()).toBe(true);
    http.expectOne('/api/coaching/invitations/invite-1/accept').flush(null);
    http.expectOne('/api/coaching/invitations/my-invitations').flush([]);
    http.expectOne('/api/coaching/profiles/me').flush({ data: { student, memberships: [] } });
    fixture.detectChanges(); await fixture.whenStable();
    expect(component.invitations()).toEqual([]); expect(component.working()).toBe(false); http.verify();
  });

  it('does not discard a dirty form when accepting an invitation or leaving the profile', async () => {
    const { fixture, component, http } = await setup();
    http.expectOne('/api/coaching/profiles/me').flush({ data: { student: { version: 1, educationStatus: 'student', gradeLevel: 10, isActive: true } } });
    fixture.detectChanges(); await fixture.whenStable();
    component.dirty = true; component.respond('invite-1', 'accept');
    expect(component.invitationError()).toContain('profil değişikliklerini kaydet');
    http.expectNone('/api/coaching/invitations/invite-1/accept');
    const confirm = vi.spyOn(TestBed.inject(ToasterService), 'confirm').mockResolvedValue(false);
    expect(await component.canLeavePage()).toBe(false); await component.reload();
    http.expectNone('/api/coaching/profiles/me'); confirm.mockRestore();
    const unload = new Event('beforeunload', { cancelable: true }) as BeforeUnloadEvent;
    window.dispatchEvent(unload); expect(unload.defaultPrevented).toBe(false); http.verify();
  });

  it('keeps missing profile creation retryable after access failure and reports invitation loading errors', async () => {
    const { fixture, component, http } = await setup();
    http.expectOne('/api/coaching/profiles/me').flush({ data: { student: null, teacher: null } });
    fixture.detectChanges(); await fixture.whenStable();
    component.provision();
    http.expectOne('/api/coaching/profiles/me/provision').flush({ message: 'Rol doğrulanamadı.' }, { status: 403, statusText: 'Forbidden' });
    expect(component.ready()).toBe(true); expect(component.working()).toBe(false); expect(component.error()).toContain('Rol doğrulanamadı');
    component.loadInvitations();
    http.expectOne('/api/coaching/invitations/my-invitations').flush({}, { status: 503, statusText: 'Unavailable' });
    expect(component.invitationError()).toContain('Davetler yüklenemedi'); http.verify();
  });
});

beforeEach(() => { TestBed.overrideProvider(ToasterService, {useValue:{confirm:vi.fn().mockResolvedValue(true),success:vi.fn(),error:vi.fn(),info:vi.fn(),warning:vi.fn()}}); });
