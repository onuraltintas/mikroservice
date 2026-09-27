import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { AuthService } from '../../../core/auth/auth.service';
import { CoachingPortalService, TeacherAssignment } from '../../../core/services/coaching-portal.service';
import { TeacherAssignmentsComponent } from './teacher-assignments.component';

describe('TeacherAssignmentsComponent', () => {
  it('filters assignment lifecycle status from the first page', () => {
    const service = assignmentService();
    TestBed.configureTestingModule({
      imports: [TeacherAssignmentsComponent],
      providers: [
        { provide: AuthService, useValue: { userProfile: signal({ id: 'teacher-1', role: 'Teacher' }) } },
        { provide: CoachingPortalService, useValue: service },
        { provide: ActivatedRoute, useValue: {} }
      ]
    });
    const component = TestBed.createComponent(TeacherAssignmentsComponent).componentInstance;
    component.ngOnInit();
    component.pageNumber.set(3);

    component.setStatusFilter('Cancelled');

    expect(service.getTeacherAssignments).toHaveBeenLastCalledWith('teacher-1', 1, 25, 'Cancelled');
    expect(component.pageNumber()).toBe(1);
  });

  it('requires explicit confirmation before cancelling an active assignment', () => {
    const service = assignmentService();
    TestBed.configureTestingModule({
      imports: [TeacherAssignmentsComponent],
      providers: [
        { provide: AuthService, useValue: { userProfile: signal({ id: 'teacher-1', role: 'Teacher' }) } },
        { provide: CoachingPortalService, useValue: service },
        { provide: ActivatedRoute, useValue: {} }
      ]
    });
    const fixture = TestBed.createComponent(TeacherAssignmentsComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;
    component.assignments.set([teacherAssignment('assignment-1')]);
    fixture.detectChanges();

    const buttons = Array.from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>);
    const cancelButton = buttons.find(button => button.textContent?.includes('İptal et'))!;
    cancelButton.click();
    fixture.detectChanges();
    expect(service.cancelTeacherAssignment).not.toHaveBeenCalled();

    const confirmButton = Array.from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>)
      .find(button => button.textContent?.includes('Evet, iptal et'))!;
    confirmButton.click();
    fixture.detectChanges();

    expect(service.cancelTeacherAssignment).toHaveBeenCalledWith('assignment-1');
    expect(component.successMessage()).toBe('Ödev iptal edildi.');
    expect(component.cancelConfirmationId()).toBeNull();
  });

  it('keeps a retryable confirmation and shows a friendly message on cancellation failure', () => {
    const service = assignmentService();
    service.cancelTeacherAssignment.mockReturnValue(throwError(() => new Error('network')));
    TestBed.configureTestingModule({
      imports: [TeacherAssignmentsComponent],
      providers: [
        { provide: AuthService, useValue: { userProfile: signal({ id: 'teacher-1', role: 'Teacher' }) } },
        { provide: CoachingPortalService, useValue: service },
        { provide: ActivatedRoute, useValue: {} }
      ]
    });
    const component = TestBed.createComponent(TeacherAssignmentsComponent).componentInstance;
    component.ngOnInit();
    component.requestCancel('assignment-1');

    component.cancelPendingAssignment();

    expect(component.cancelConfirmationId()).toBe('assignment-1');
    expect(component.errorMessage()).toBe('Ödev iptal edilemedi. Lütfen tekrar deneyin.');
    expect(component.isCancelling()).toBe(false);
  });

  it('clears the loading state when an assignment request fails', () => {
    const service = assignmentService();
    service.getTeacherAssignments.mockReturnValue(throwError(() => new Error('network')));
    TestBed.configureTestingModule({
      imports: [TeacherAssignmentsComponent],
      providers: [
        { provide: AuthService, useValue: { userProfile: signal({ id: 'teacher-1', role: 'Teacher' }) } },
        { provide: CoachingPortalService, useValue: service },
        { provide: ActivatedRoute, useValue: {} }
      ]
    });
    const component = TestBed.createComponent(TeacherAssignmentsComponent).componentInstance;

    component.ngOnInit();

    expect(component.isLoading()).toBe(false);
    expect(component.errorMessage()).toBe('Ödevler yüklenemedi. Lütfen tekrar deneyin.');
  });

  it('offers a retry action after the assignment list fails', () => {
    const service = assignmentService();
    service.getTeacherAssignments
      .mockReturnValueOnce(throwError(() => new Error('network')))
      .mockReturnValueOnce(of({ items: [teacherAssignment('assignment-1')], pageNumber: 1, pageSize: 25, totalCount: 1, totalPages: 1 }));
    TestBed.configureTestingModule({
      imports: [TeacherAssignmentsComponent],
      providers: [
        { provide: AuthService, useValue: { userProfile: signal({ id: 'teacher-1', role: 'Teacher' }) } },
        { provide: CoachingPortalService, useValue: service },
        { provide: ActivatedRoute, useValue: {} }
      ]
    });
    const fixture = TestBed.createComponent(TeacherAssignmentsComponent);
    fixture.detectChanges();
    const retryButton = Array.from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>)
      .find(button => button.textContent?.includes('Tekrar dene'))!;

    retryButton.click();
    fixture.detectChanges();

    expect(service.getTeacherAssignments).toHaveBeenCalledTimes(2);
    expect(fixture.componentInstance.assignments().map(assignment => assignment.id)).toEqual(['assignment-1']);
    expect(fixture.componentInstance.errorMessage()).toBeNull();
  });
});

function assignmentService() {
  return {
    getTeacherAssignments: vi.fn(() => of({ items: [] as TeacherAssignment[], pageNumber: 1, pageSize: 25, totalCount: 0, totalPages: 0 })),
    cancelTeacherAssignment: vi.fn(() => of({ message: 'Assignment cancelled successfully' }))
  };
}

function teacherAssignment(id: string): TeacherAssignment {
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
