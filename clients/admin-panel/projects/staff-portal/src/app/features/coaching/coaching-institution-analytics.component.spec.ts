import { ToasterService } from '../../../../../../src/app/core/services/toaster.service';
import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { of, Subject, throwError } from 'rxjs';
import { vi } from 'vitest';
import { CoachingAdminService } from '../../../../../../src/app/core/services/coaching-admin.service';
import { CoachingInstitutionAnalyticsComponent } from './coaching-institution-analytics.component';

describe('institution coaching analytics', () => {
  it('clears old results and cancels pending requests when filters change', async () => {
    const { component, service, comparison } = setup();
    const pending = new Subject<typeof comparison>(); service.getInstitutionComparison.mockReturnValueOnce(pending);
    (await component.load(2)); component.fromDate = '2026-10-02'; component.filtersChanged();
    expect(pending.observed).toBe(false); expect(component.comparison()).toBeNull(); expect(component.warnings()).toBeNull();
    (await component.load());
    expect(service.getInstitutionEarlyWarnings).toHaveBeenLastCalledWith('institution', expect.objectContaining({ pageNumber: 1 }));
  });
  function setup() {
    const warning = { studentId: 'student', studentName: 'Ada Test', studentEmail: 'ada@example.invalid', gradeLevel: 8,
      teacherName: 'Test Koç', riskLevel: 'High' as const, riskScore: 70, reasonCodes: ['low_assignment_submission'],
      assignmentCount: 5, submittedAssignmentCount: 1, recordedAttendanceCount: 2, attendedSessionCount: 1, goalCount: 1, averageGoalProgress: 10 };
    const comparison = { institutionId: 'institution', studentCount: 1, assignmentCount: 5, submittedAssignmentCount: 1,
      examResultCount: 0, averageExamPercentage: null, attendancePercentage: 50, completedGoalCount: 0, goalCount: 1 };
    const service = { getInstitutionComparison: vi.fn(() => of(comparison)),
      getInstitutionEarlyWarnings: vi.fn(() => of({ institutionId: 'institution', items: [warning], pageNumber: 1, pageSize: 25, totalCount: 30, totalPages: 2 })) };
    TestBed.configureTestingModule({ imports: [CoachingInstitutionAnalyticsComponent], providers: [{ provide: CoachingAdminService, useValue: service }] });
    const fixture = TestBed.createComponent(CoachingInstitutionAnalyticsComponent);
    fixture.componentRef.setInput('institutionId', 'institution');
    fixture.componentInstance.fromDate = '2026-10-01'; fixture.componentInstance.toDate = '2026-10-13';
    fixture.detectChanges();
    return { fixture, component: fixture.componentInstance, service, comparison, warning };
  }

  it('uses the verified institution and local date bounds for comparison and paged warnings', async () => {
    const { component, fixture, service } = setup();
    // K2: plain dates; the server reads them as whole Europe/Istanbul days whatever the browser's time zone is.
    const dates = { fromDate: '2026-10-01', toDate: '2026-10-13' };
    expect(service.getInstitutionComparison).toHaveBeenCalledWith('institution', dates);
    expect(service.getInstitutionEarlyWarnings).toHaveBeenCalledWith('institution', { ...dates, pageNumber: 1, pageSize: 25 });
    expect(fixture.nativeElement.textContent).toContain('Ada Test');
    (await component.load(2));
    expect(service.getInstitutionEarlyWarnings).toHaveBeenLastCalledWith('institution', { ...dates, pageNumber: 2, pageSize: 25 });
  });

  it('rejects reversed or oversized ranges without requesting data', async () => {
    const { component, service } = setup();
    component.fromDate = '2026-10-14'; component.toDate = '2026-10-13'; (await component.load());
    expect(component.error()).toBeTruthy();
    component.fromDate = '2024-01-01'; component.toDate = '2026-10-13'; (await component.load());
    expect(service.getInstitutionComparison).toHaveBeenCalledTimes(1);
  });

  it('clears results on authorization loss and allows a retry', async () => {
    const { component, service } = setup();
    service.getInstitutionComparison.mockReturnValueOnce(throwError(() => new HttpErrorResponse({ status: 403 })));
    (await component.load());
    expect(component.comparison()).toBeNull(); expect(component.warnings()).toBeNull(); expect(component.error()).toContain('yetki');
    (await component.load()); expect(component.comparison()).not.toBeNull();
  });

  it('cancels old requests when institution scope changes and opens warning student reports', async () => {
    const { component, fixture, service, warning, comparison } = setup();
    const pending = new Subject<typeof comparison>(); service.getInstitutionComparison.mockReturnValueOnce(pending);
    (await component.load()); fixture.componentRef.setInput('institutionId', 'new-institution'); fixture.detectChanges();
    expect(pending.observed).toBe(false);
    expect(service.getInstitutionComparison).toHaveBeenLastCalledWith('new-institution', expect.any(Object));
    const selected = vi.fn(); component.studentSelected.subscribe(selected); component.openStudent(warning);
    expect(selected).toHaveBeenCalledWith(expect.objectContaining({ userId: 'student', firstName: 'Ada Test', gradeLevel: 8 }));
  });
});

beforeEach(() => { TestBed.overrideProvider(ToasterService, {useValue:{confirm:vi.fn().mockResolvedValue(true),success:vi.fn(),error:vi.fn(),info:vi.fn(),warning:vi.fn()}}); });
