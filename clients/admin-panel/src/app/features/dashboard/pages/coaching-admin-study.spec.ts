import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AuthService } from '../../../core/auth/auth.service';
import { CoachingAdminStudyComponent } from './coaching-admin-study';

describe('CoachingAdminStudyComponent', () => {
  function setup(global = true) {
    TestBed.configureTestingModule({ imports: [CoachingAdminStudyComponent], providers: [provideHttpClient(), provideHttpClientTesting(),
      { provide: AuthService, useValue: { userProfile: () => ({ roles: global ? ['SystemAdmin'] : ['InstitutionAdmin'] }), hasPermission: () => true } }] });
    const fixture = TestBed.createComponent(CoachingAdminStudyComponent); fixture.componentRef.setInput('studentId', 'student-one'); fixture.detectChanges();
    return { fixture, component: fixture.componentInstance, http: TestBed.inject(HttpTestingController) };
  }
  afterEach(() => TestBed.inject(HttpTestingController).verify());
  it('shows student availability and complete revision task details read-only', () => {
    const { fixture, component, http } = setup();
    http.expectOne(r => r.url.endsWith('/student-one/study/availability')).flush({ data: { version: 1, timeZoneId: 'Europe/Istanbul', windows: [{ day: 'Monday', startMinute: 600, endMinute: 660 }] } });
    http.expectOne(r => r.url.endsWith('/student-one/study/plans')).flush({ data: { items: [{ id: 'revision', planId: 'plan', revisionNumber: 2, version: 1, title: 'Study plan', status: 'Archived', automaticAvailabilityVersion: 2 }], totalCount: 1, pageNumber: 1, pageSize: 25 } });
    component.inspect('revision');
    http.expectOne(r => r.url.endsWith('/plans/revision')).flush({ data: { id: 'revision', title: 'Study plan', status: 'Archived', version: 1, tasks: [{ id: 'task', plannedDate: '2026-10-02', title: 'History task', plannedMinutes: 30, isCompleted: true, actualMinutes: 25 }] } });
    fixture.detectChanges(); const text = fixture.nativeElement.textContent;
    expect(text).toContain('Europe/Istanbul'); expect(text).toContain('History task'); expect(text).toContain('25');
    expect(fixture.nativeElement.querySelector('button[type="submit"]')).toBeNull();
  });
  it('does not request personal planning data for institution managers', () => {
    const { http } = setup(false); http.expectNone(r => r.url.includes('/study/'));
  });
  it('cancels old student requests and resets selected detail when input changes', () => {
    const { fixture, component, http } = setup();
    const availability = http.expectOne(r => r.url.endsWith('/student-one/study/availability'));
    const plans = http.expectOne(r => r.url.endsWith('/student-one/study/plans'));
    fixture.componentRef.setInput('studentId', 'student-two'); fixture.detectChanges();
    expect(availability.cancelled).toBe(true); expect(plans.cancelled).toBe(true); expect(component.selected()).toBeNull();
    http.expectOne(r => r.url.endsWith('/student-two/study/availability')).flush({ data: null });
    http.expectOne(r => r.url.endsWith('/student-two/study/plans')).flush({ data: { items: [], totalCount: 0, pageNumber: 1, pageSize: 25 } });
  });
});
