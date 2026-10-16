import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { CoachingProfileComponent } from '../../../../../shared/coaching-profile.component';

describe('Coaching owned profile', () => {
  function setup(role: 'Student' | 'Teacher' = 'Student') {
    TestBed.configureTestingModule({ imports: [CoachingProfileComponent], providers: [provideHttpClient(), provideHttpClientTesting()] });
    const fixture = TestBed.createComponent(CoachingProfileComponent);
    fixture.componentRef.setInput('apiUrl', '/api'); fixture.componentRef.setInput('role', role);
    fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne('/api/coaching/invitations/my-invitations').flush([]);
    return { fixture, component: fixture.componentInstance, http };
  }

  it('reads without creating a profile and provisions explicitly with a product role', () => {
    const { component, http } = setup();
    http.expectOne('/api/coaching/profiles/me').flush({ data: { student: null, teacher: null, memberships: [] } });
    http.expectNone('/api/coaching/profiles/me/provision');
    component.provision();
    const request = http.expectOne('/api/coaching/profiles/me/provision');
    expect(request.request.body).toEqual({ role: 'Student' });
    request.flush({ data: { userId: 'same-account' } });
    http.expectOne('/api/coaching/profiles/me').flush({ data: { student: null, teacher: null, memberships: [] } });
    http.verify();
  });

  it('saves graduate education in Coaching with version protection and no account fields', () => {
    const { component, http } = setup();
    http.expectOne('/api/coaching/profiles/me').flush({ data: { student: { version: 3, educationStatus: 'student', gradeLevel: 12, shareProgressWithTeachers: true, isActive: true } } });
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
    const { component, http } = setup();
    http.expectOne('/api/coaching/profiles/me').flush({ data: { student: { version: 1, educationStatus: 'student', gradeLevel: 10, shareProgressWithTeachers: true, isActive: true } } });
    component.student!.gradeLevel = 11; component.dirty = true; component.save();
    http.expectOne('/api/coaching/profiles/me/student').flush({ message: 'Profil değişti. Yeniden yükleyin.' }, { status: 409, statusText: 'Conflict' });
    expect(component.student!.gradeLevel).toBe(11); expect(component.dirty).toBe(true);
    expect(component.error()).toContain('Profil değişti'); http.verify();
  });
});
