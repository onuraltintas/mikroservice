import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { StaffAuthService } from '../../auth/staff-auth.service';
import { CoachingTeacherAssignmentsService, CoachingTeacherAssignment } from './coaching-teacher-assignments.service';
import { CoachingTeacherAssignmentsComponent } from './coaching-teacher-assignments.component';

describe('CoachingTeacherAssignmentsComponent', () => {
  it('loads the current teacher assignment list with delivery and status information', () => {
    const service = assignmentService();
    service.getTeacherAssignments.mockReturnValue(of(page([assignment('assignment-1')])));
    const fixture = createFixture(service);
    fixture.detectChanges();

    expect(service.getTeacherAssignments).toHaveBeenCalledWith('teacher-1', 1, 25, undefined);
    expect(fixture.nativeElement.textContent).toContain('LGS deneme ödevi');
    expect(fixture.nativeElement.textContent).toContain('1 / 2 teslim');
    expect(fixture.nativeElement.textContent).toContain('Aktif');
  });

  it('filters status from page one and lets the teacher move between pages', () => {
    const service = assignmentService();
    service.getTeacherAssignments
      .mockReturnValueOnce(of(page([], 1, 30, 2)))
      .mockReturnValueOnce(of(page([], 2, 30, 2)))
      .mockReturnValueOnce(of(page([], 1, 30, 2)));
    const fixture = createFixture(service);
    fixture.detectChanges();
    const component = fixture.componentInstance;

    component.nextPage();
    component.setStatusFilter('Completed');

    expect(service.getTeacherAssignments.mock.calls.map(call => call.slice(0, 4))).toEqual([
      ['teacher-1', 1, 25, undefined],
      ['teacher-1', 2, 25, undefined],
      ['teacher-1', 1, 25, 'Completed']
    ]);
    expect(component.pageNumber()).toBe(1);
  });

  it('requires explicit confirmation and reloads after cancel succeeds', () => {
    const service = assignmentService();
    service.getTeacherAssignments.mockReturnValue(of(page([assignment('assignment-1')])));
    const fixture = createFixture(service);
    fixture.detectChanges();
    const component = fixture.componentInstance;
    component.requestCancel('assignment-1');

    expect(service.cancelAssignment).not.toHaveBeenCalled();
    component.cancelPendingAssignment();

    expect(service.cancelAssignment).toHaveBeenCalledWith('assignment-1');
    expect(component.successMessage()).toBe('Ödev iptal edildi.');
    expect(component.cancelConfirmationId()).toBeNull();
    expect(service.getTeacherAssignments).toHaveBeenCalledTimes(2);
  });

  it('shows a retryable friendly error when the assignment list fails', () => {
    const service = assignmentService();
    service.getTeacherAssignments.mockReturnValue(throwError(() => new Error('network')));
    const fixture = createFixture(service);
    fixture.detectChanges();

    expect(fixture.componentInstance.isLoading()).toBe(false);
    expect(fixture.componentInstance.errorMessage()).toBe('Ödev listesi yüklenemedi. Lütfen tekrar deneyin.');
    expect(fixture.nativeElement.textContent).toContain('Tekrar dene');
  });

  it('does not request a list if the authenticated user id is unavailable', () => {
    const service = assignmentService();
    const fixture = createFixture(service, null);
    fixture.detectChanges();

    expect(service.getTeacherAssignments).not.toHaveBeenCalled();
    expect(fixture.componentInstance.errorMessage()).toBe('Öğretmen oturumu doğrulanamadı. Lütfen yeniden giriş yapın.');
  });
});

function createFixture(service: ReturnType<typeof assignmentService>, userId: string | null = 'teacher-1') {
  TestBed.configureTestingModule({
    imports: [CoachingTeacherAssignmentsComponent],
    providers: [
      { provide: StaffAuthService, useValue: { getCurrentUserId: () => userId } },
      { provide: CoachingTeacherAssignmentsService, useValue: service }
    ]
  });
  return TestBed.createComponent(CoachingTeacherAssignmentsComponent);
}

function assignmentService() {
  return {
    getTeacherAssignments: vi.fn(() => of(page([]))),
    cancelAssignment: vi.fn(() => of({ message: 'Assignment cancelled successfully' }))
  };
}

function assignment(id: string): CoachingTeacherAssignment {
  return {
    id,
    title: 'LGS deneme ödevi',
    type: 'Digital',
    dueDate: '2030-01-01T10:00:00Z',
    status: 'Active',
    totalStudents: 2,
    submittedCount: 1,
    createdAt: '2029-12-01T10:00:00Z'
  };
}

function page(items: CoachingTeacherAssignment[], pageNumber = 1, totalCount = items.length, totalPages = 1) {
  return { items, pageNumber, pageSize: 25, totalCount, totalPages };
}
