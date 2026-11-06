import { ToasterService } from '../../../core/services/toaster.service';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { CoachingProfileComponent } from '../../../../../shared/coaching-profile.component';
import { vi } from 'vitest';

describe('Coaching owned profile', () => {
  it('lets a student fill education fields and save them through the rendered form', async () => {
    const { fixture, component, http } = await setup();
    const student = { version: 1, educationStatus: 'Student', gradeLevel: 10, shareProgressWithTeachers: true, isActive: true };
    http.expectOne('/api/coaching/profiles/me').flush({ data: { student } });
    fixture.detectChanges(); await fixture.whenStable();
    const fields = { classBranch: 'A', birthDate: '2008-01-02', studentBio: 'Sınava hazırlanıyorum.', gradeLevel: '11' };
    for (const [name, value] of Object.entries(fields)) {
      const input = fixture.nativeElement.querySelector(`[name="${name}"]`) as HTMLInputElement;
      input.value = value; input.dispatchEvent(new Event('input'));
    }
    (fixture.nativeElement.querySelector('[name="shareProgress"]') as HTMLInputElement).click();
    fixture.detectChanges(); await fixture.whenStable();
    expect(component.dirty).toBe(true);
    fixture.nativeElement.querySelector('form').dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    const request = http.expectOne('/api/coaching/profiles/me/student');
    expect(request.request.body.classBranch).toBe('A');
    expect(request.request.body.gradeLevel).toBe(11); expect(request.request.body.birthDate).toBe('2008-01-02');
    expect(request.request.body.shareProgressWithTeachers).toBe(false);
    request.flush({ data: { ...student, ...request.request.body, version: 2 } }); fixture.detectChanges(); await fixture.whenStable();
    // A changed grade asks the study settings whether the exam still fits.
    http.expectOne('/api/coaching/study-planning/setup').flush({ data: { context: null, isReady: false, missingFields: [], warnings: [], purposeNotForEducation: false } });
    fixture.detectChanges(); await fixture.whenStable();
    expect(fixture.nativeElement.textContent).toContain('Profilin kaydedildi.');
    expect(fixture.nativeElement.querySelector('[data-testid="grade-purpose-notice"]')).toBeNull(); http.verify();
  });

  it('picks the school from the catalog like the wizard and keeps an older typed school until then (Paket D #7)', async () => {
    const { fixture, component, http } = await setup();
    const student = { version: 5, educationStatus: 'Student', gradeLevel: 10, shareProgressWithTeachers: true, isActive: true,
      homeProvinceId: '34', homeCity: 'İstanbul', homeDistrictId: '3401', homeDistrict: 'Kadıköy',
      schoolName: 'Eski Lise', schoolCity: 'İstanbul', schoolDistrict: 'Kadıköy', schoolType: 'Lise' };
    http.expectOne('/api/coaching/profiles/me').flush({ data: { student } });
    fixture.detectChanges(); await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;
    // No free-text school fields: the saved school is shown as it is.
    for (const name of ['schoolName', 'schoolCity', 'schoolDistrict', 'schoolType']) expect(el.querySelector(`[name="${name}"]`)).toBeNull();
    expect(el.querySelector('[data-testid="own-school"]')!.textContent).toContain('Eski Lise');
    expect(el.textContent).not.toContain('Okulumu listede bulamıyorum');
    (Array.from(el.querySelectorAll('[data-testid="own-school"] button')).find(x => x.textContent?.includes('Okulunu değiştir')) as HTMLElement).click();
    fixture.detectChanges(); await fixture.whenStable();
    // Review L8: the focus moves into the picker.
    await new Promise(resolve => setTimeout(resolve));
    expect(document.activeElement).toBe(el.querySelector('[data-testid="own-school"] input[role="combobox"]'));
    // The home city is the starting city; its district's schools come first, marked "ilçende", with the catalog's type.
    expect(component.schoolCity()).toBe('İstanbul');
    await new Promise(resolve => setTimeout(resolve, 250));
    const list = http.expectOne(r => r.url === '/api/coaching/students/me/onboarding/options/schools');
    expect(list.request.params.get('city')).toBe('İstanbul'); expect(list.request.params.get('district')).toBe('Kadıköy');
    expect(list.request.params.has('type')).toBe(false);
    list.flush({ data: { items: [{ id: 's-1', name: 'Kadıköy Anadolu Lisesi', district: 'KADIKÖY', type: 'Anadolu Lisesi', minimumScore: 480, scoreYear: 2025, inHomeDistrict: true }], totalCount: 1, pageNumber: 1, pageSize: 20 } });
    fixture.detectChanges(); await fixture.whenStable();
    const option = el.querySelector('[data-testid="own-school"] [role="option"]') as HTMLElement;
    expect(option.textContent).toContain('ilçende'); expect(option.textContent).toContain('Anadolu Lisesi');
    option.click(); fixture.detectChanges(); await fixture.whenStable();
    expect(el.querySelector('[data-testid="own-school"]')!.textContent).toContain('Kadıköy Anadolu Lisesi');
    expect(component.dirty).toBe(true);
    component.save();
    const request = http.expectOne('/api/coaching/profiles/me/student');
    expect(request.request.body).toMatchObject({ schoolId: 's-1', schoolName: 'Eski Lise', schoolType: 'Lise' });
    request.flush({ data: { ...student, version: 6, schoolName: 'Kadıköy Anadolu Lisesi', schoolCity: 'İSTANBUL', schoolDistrict: 'KADIKÖY', schoolType: 'Anadolu Lisesi' } });
    fixture.detectChanges(); await fixture.whenStable();
    expect(el.querySelector('[data-testid="own-school"]')!.textContent).toContain('Anadolu Lisesi');
    http.verify();
  });

  it('lets a younger student remove an older typed school and shows the diploma grade only when the exam asks it (review L2, L5)', async () => {
    const { fixture, component, http } = await setup();
    const student = { version: 5, educationStatus: 'Student', gradeLevel: 7, shareProgressWithTeachers: true, isActive: true, schoolName: 'Eski Ortaokul' };
    http.expectOne('/api/coaching/profiles/me').flush({ data: { student, examAsksDiploma: false } });
    fixture.detectChanges(); await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;
    const buttons = Array.from(el.querySelectorAll('[data-testid="own-school"] button')).map(x => x.textContent?.trim());
    expect(buttons).toContain('Okulu kaldır'); expect(buttons).not.toContain('Okulunu değiştir');
    component.removeSchool(); fixture.detectChanges(); await fixture.whenStable();
    expect(el.querySelector('[data-testid="own-school"]')!.textContent).toContain('seçilmedi');
    // A graduate preparing for YDS is not asked the diploma grade (the server would drop it).
    const status = el.querySelector('[name="educationStatus"]') as HTMLSelectElement;
    status.value = 'graduate'; status.dispatchEvent(new Event('change')); fixture.detectChanges(); await fixture.whenStable();
    expect(component.student!.educationStatus).toBe('graduate');
    expect(el.querySelector('[name="diplomaGrade"]')).toBeNull();
    TestBed.resetTestingModule();
    const yks = await setup();
    yks.http.expectOne('/api/coaching/profiles/me').flush({ data: { student: { ...student, educationStatus: 'Graduate', gradeLevel: null }, examAsksDiploma: true } });
    yks.fixture.detectChanges(); await yks.fixture.whenStable();
    expect(yks.fixture.nativeElement.querySelector('[name="diplomaGrade"]')).not.toBeNull();
  });

  it('lets the student pick another city and remove the school', async () => {
    const { fixture, component, http } = await setup();
    const student = { version: 5, educationStatus: 'Graduate', gradeLevel: null, shareProgressWithTeachers: true, isActive: true,
      homeProvinceId: '34', homeCity: 'İstanbul', homeDistrictId: '3401', homeDistrict: 'Kadıköy', schoolName: 'Eski Lise', schoolType: 'Lise' };
    http.expectOne('/api/coaching/profiles/me').flush({ data: { student } });
    fixture.detectChanges(); await fixture.whenStable();
    component.openSchoolPicker(); fixture.detectChanges(); await fixture.whenStable();
    component.changeSchoolCity(); fixture.detectChanges(); await fixture.whenStable();
    http.expectOne('/api/coaching/students/me/onboarding/options/schools/cities').flush({ data: [{ value: 'ANKARA', label: 'Ankara', count: 3 }] });
    component.pickSchoolCity({ value: 'ANKARA', label: 'Ankara' }); fixture.detectChanges(); await fixture.whenStable();
    await new Promise(resolve => setTimeout(resolve, 250));
    const list = http.expectOne(r => r.url === '/api/coaching/students/me/onboarding/options/schools');
    expect(list.request.params.get('city')).toBe('ANKARA'); expect(list.request.params.has('district')).toBe(false);
    list.flush({ data: { items: [], totalCount: 0, pageNumber: 1, pageSize: 20 } });
    component.removeSchool(); expect(component.dirty).toBe(true);
    component.save();
    expect(http.expectOne('/api/coaching/profiles/me/student').request.body).toMatchObject({ schoolName: null, schoolCity: null, schoolDistrict: null, schoolType: null });
  });

  it('tells the student when the new grade no longer fits the exam and links to the study settings (Paket D #8)', async () => {
    const { fixture, component, http } = await setup();
    const student = { version: 2, educationStatus: 'Student', gradeLevel: 8, shareProgressWithTeachers: true, isActive: true };
    http.expectOne('/api/coaching/profiles/me').flush({ data: { student } });
    fixture.detectChanges(); await fixture.whenStable();
    component.student!.gradeLevel = 9; component.dirty = true; component.save();
    http.expectOne('/api/coaching/profiles/me/student').flush({ data: { ...student, version: 3, gradeLevel: 9 } });
    http.expectOne('/api/coaching/study-planning/setup').flush({ data: { context: null, isReady: false, missingFields: ['Purpose'], warnings: [], purposeNotForEducation: true } });
    fixture.detectChanges(); await fixture.whenStable();
    const notice = fixture.nativeElement.querySelector('[data-testid="grade-purpose-notice"]') as HTMLElement;
    expect(notice.textContent).toContain('Seçili sınavın sınıfına uymuyor; çalışma tercihlerini güncelle.');
    expect(notice.querySelector('a[href="/coaching-portal/account/settings"]')!.textContent).toContain('Çalışma tercihlerim');
    // The diploma grade is asked only for 11th, 12th grade and graduates (as in the wizard).
    expect(fixture.nativeElement.querySelector('[name="diplomaGrade"]')).toBeNull();
    // Saving without a grade change does not ask again.
    component.student!.bio = 'x'; component.dirty = true; component.save();
    http.expectOne('/api/coaching/profiles/me/student').flush({ data: { ...student, version: 4, gradeLevel: 9 } });
    http.expectNone('/api/coaching/study-planning/setup');
    // Review L8: a later save clears the old notice.
    fixture.detectChanges(); await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('[data-testid="grade-purpose-notice"]')).toBeNull(); http.verify();
  });

  it('lets a student correct the home city, district and diploma grade from the wizard (KVKK m.11)', async () => {
    const { fixture, component, http } = await setup();
    const student = { version: 3, educationStatus: 'Student', gradeLevel: 12, shareProgressWithTeachers: true, isActive: true,
      homeProvinceId: '06', homeCity: 'Ankara', homeDistrictId: '0601', homeDistrict: 'Çankaya', diplomaGrade: 85, diplomaGradeEstimated: true };
    http.expectOne('/api/coaching/profiles/me').flush({ data: { student } });
    fixture.detectChanges(); await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[data-testid="home-place"]')!.textContent).toContain('Ankara / Çankaya');
    (Array.from(el.querySelectorAll('[data-testid="home-place"] button')).find(x => x.textContent?.includes('Değiştir')) as HTMLElement).click();
    http.expectOne('/api/locations/provinces').flush([{ id: '06', name: 'Ankara' }, { id: '34', name: 'İstanbul' }]);
    fixture.detectChanges(); await fixture.whenStable();
    component.pickProvince('34');
    http.expectOne('/api/locations/provinces/34/districts').flush([{ id: '3401', provinceId: '34', name: 'Kadıköy' }]);
    component.homeDistrictId = '3401';
    component.student!.diplomaGrade = null;
    component.save();
    const request = http.expectOne('/api/coaching/profiles/me/student');
    expect(request.request.body).toMatchObject({ homeProvinceId: '34', homeDistrictId: '3401', clearDiplomaGrade: true });
    request.flush({ data: { ...student, version: 4, homeCity: 'İstanbul', homeDistrict: 'Kadıköy', diplomaGrade: null } });
    TestBed.resetTestingModule();
    const second = await setup();
    second.http.expectOne('/api/coaching/profiles/me').flush({ data: { student } });
    second.fixture.detectChanges(); await second.fixture.whenStable();
    second.component.student!.diplomaGrade = 120; second.component.save();
    expect(second.component.error()).toContain('Diploma notu 50 ile 100');
    second.component.student!.diplomaGrade = 90.5; second.component.save();
    expect(second.http.expectOne('/api/coaching/profiles/me/student').request.body).toMatchObject({ diplomaGrade: 90.5, diplomaGradeEstimated: true });
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
    TestBed.configureTestingModule({ imports: [CoachingProfileComponent], providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()] });
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
    http.expectOne('/api/coaching/study-planning/setup').flush({ data: { context: null, isReady: true, missingFields: [], warnings: [] } });
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
