import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { of } from 'rxjs';
import { StudentStudyReportComponent } from './student-study-report.component';
import { CoachingPlanningPermissionService } from '../../../core/services/coaching-planning-permission.service';

// User decision 15.11.2026: with a plan made for a week (or up to a date) the report says its plan figures count only that plan.
describe('StudentStudyReportComponent plan span note', () => {
  function mount(partialPlanEnd: string | null, inputs: { staffStudentId?: string; adminStudentId?: string } = {}) {
    const permissions = { own: vi.fn(() => of({ hasTeacher: false, allowedByTeacher: false, studentMayPlan: true, partialPlanEnd })),
      forStudent: vi.fn(() => of({ hasTeacher: true, allowedByTeacher: false, studentMayPlan: false, partialPlanEnd })) };
    TestBed.configureTestingModule({ imports: [StudentStudyReportComponent], providers: [provideHttpClient(), provideHttpClientTesting(),
      { provide: CoachingPlanningPermissionService, useValue: permissions }] });
    const fixture = TestBed.createComponent(StudentStudyReportComponent);
    for (const [name, value] of Object.entries(inputs)) fixture.componentRef.setInput(name, value);
    fixture.componentInstance.fromDate = '2026-11-09'; fixture.componentInstance.toDate = '2026-11-15';
    fixture.componentInstance.load();
    TestBed.inject(HttpTestingController).match(() => true);
    return { fixture, permissions };
  }

  it('remembers the end of a short plan for the student and for a teacher\'s student', () => {
    const own = mount('2026-11-15');
    expect(own.permissions.own).toHaveBeenCalled();
    expect(own.fixture.componentInstance.partialPlanEnd()).toBe('2026-11-15');
    TestBed.resetTestingModule();
    const staff = mount('2026-11-22', { staffStudentId: 'student-1' });
    expect(staff.permissions.forStudent).toHaveBeenCalledWith('student-1');
    expect(staff.fixture.componentInstance.partialPlanEnd()).toBe('2026-11-22');
  });

  it('has no note for a plan to the exam, and does not ask for the admin view', () => {
    expect(mount(null).fixture.componentInstance.partialPlanEnd()).toBeNull();
    TestBed.resetTestingModule();
    const admin = mount('2026-11-15', { adminStudentId: 'student-1' });
    expect(admin.permissions.own).not.toHaveBeenCalled(); expect(admin.permissions.forStudent).not.toHaveBeenCalled();
    expect(admin.fixture.componentInstance.partialPlanEnd()).toBeNull();
  });
});
