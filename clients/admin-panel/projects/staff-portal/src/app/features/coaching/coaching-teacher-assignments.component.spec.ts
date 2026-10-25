import { ToasterService } from '../../../../../../src/app/core/services/toaster.service';
import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { StaffAuthService } from '../../auth/staff-auth.service';
import {
  CoachingAssignmentCreateRequest,
  CoachingAssignmentDetail,
  CoachingAssignmentUpdateRequest,
  CoachingTeacherAssignmentsService,
  CoachingTeacherAssignment
} from './coaching-teacher-assignments.service';
import { CoachingTeacherStudentsService } from './coaching-teacher-students.service';
import { CoachingTeacherAssignmentsComponent } from './coaching-teacher-assignments.component';

describe('CoachingTeacherAssignmentsComponent', () => {
  it('opens a notification assignment outside the list page and reloads a second exact assignment',async () =>{
    const service=assignmentService();const fixture=createFixture(service);
    fixture.componentRef.setInput('notificationAssignmentId','not-on-page');fixture.detectChanges();
    expect(service.getAssignment).toHaveBeenLastCalledWith('not-on-page');
    await fixture.componentInstance.reviewAssignment('second');fixture.detectChanges();
    expect(service.getAssignment).toHaveBeenLastCalledWith('second');
    await fixture.componentInstance.closeAssignmentDetail();fixture.detectChanges();
    await fixture.componentInstance.reviewAssignment('second');fixture.detectChanges();
    expect(service.getAssignment.mock.calls.map(call=>call[0])).toEqual(['not-on-page','second','second']);
  });
  it('keeps an open assignment form instead of replacing it with a notification target',async () =>{
    const service=assignmentService();const fixture=createFixture(service);fixture.detectChanges();
    fixture.componentInstance.createAssignment();fixture.detectChanges();
    await fixture.componentInstance.reviewAssignment('other');fixture.detectChanges();
    expect(fixture.componentInstance.formMode()?.kind).toBe('new');
    expect(fixture.componentInstance.detailAssignmentId()).toBeNull();
    expect(fixture.nativeElement.querySelector('[role="alert"]').textContent).toContain('ödev formunu');
    expect(service.getAssignment).not.toHaveBeenCalled();
  });
  it('loads the current teacher assignment list with delivery and status information', async () => {
    const service = assignmentService();
    service.getTeacherAssignments.mockReturnValue(of(page([assignment('assignment-1')])));
    const fixture = createFixture(service);
    fixture.detectChanges();

    expect(service.getTeacherAssignments).toHaveBeenCalledWith('teacher-1', 1, 25, undefined);
    expect(fixture.nativeElement.textContent).toContain('LGS deneme ödevi');
    expect(fixture.nativeElement.textContent).toContain('1 / 2 teslim');
    expect(fixture.nativeElement.textContent).toContain('Aktif');
  });

  it('filters status from page one and lets the teacher move between pages', async () => {
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

  it('requires explicit confirmation and reloads after cancel succeeds', async () => {
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

  it('shows a retryable friendly error when the assignment list fails', async () => {
    const service = assignmentService();
    service.getTeacherAssignments.mockReturnValue(throwError(() => new Error('network')));
    const fixture = createFixture(service);
    fixture.detectChanges();

    expect(fixture.componentInstance.isLoading()).toBe(false);
    expect(fixture.componentInstance.errorMessage()).toBe('Ödev listesi yüklenemedi. Lütfen tekrar deneyin.');
    expect(fixture.nativeElement.textContent).toContain('Tekrar dene');
  });

  it('does not request a list if the authenticated user id is unavailable', async () => {
    const service = assignmentService();
    const fixture = createFixture(service, null);
    fixture.detectChanges();

    expect(service.getTeacherAssignments).not.toHaveBeenCalled();
    expect(fixture.componentInstance.errorMessage()).toBe('Öğretmen oturumu doğrulanamadı. Lütfen yeniden giriş yapın.');
  });

  it('opens the new assignment form from the teacher list and returns without navigating away', async () => {
    const service = assignmentService();
    const fixture = createFixture(service);
    fixture.detectChanges();
    (fixture.nativeElement.querySelector('[data-testid="create-assignment"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Yeni ödev oluştur');
    expect(fixture.nativeElement.querySelector('#assignment-title')).not.toBeNull();
    (fixture.nativeElement.querySelector('.editor-back') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Ödevlerim');
  });

  it('opens the edit form for the selected assignment', async () => {
    const service = assignmentService();
    service.getTeacherAssignments.mockReturnValue(of(page([assignment('assignment-1')])));
    const fixture = createFixture(service);
    fixture.detectChanges();
    (fixture.nativeElement.querySelector('[data-testid="edit-assignment-1"]') as HTMLButtonElement).click();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(service.getAssignment).toHaveBeenCalledWith('assignment-1');
    expect(fixture.nativeElement.textContent).toContain('Ödevi düzenle');
    expect((fixture.nativeElement.querySelector('#assignment-title') as HTMLInputElement).value).toBe('Haftalık tekrar');
  });

  it('opens the teacher submission review from the assignment list', async () => {
    const service = assignmentService();
    service.getTeacherAssignments.mockReturnValue(of(page([assignment('assignment-1')])));
    const fixture = createFixture(service);
    fixture.detectChanges();
    (fixture.nativeElement.querySelector('[data-testid="review-assignment-1"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(service.getAssignment).toHaveBeenCalledWith('assignment-1');
    expect(fixture.nativeElement.textContent).toContain('ÖDEV DEĞERLENDİRMESİ');
    expect(fixture.nativeElement.textContent).toContain('Öğrenci teslimleri');
  });
});

function createFixture(service: ReturnType<typeof assignmentService>, userId: string | null = 'teacher-1') {
  TestBed.configureTestingModule({
    imports: [CoachingTeacherAssignmentsComponent],
    providers: [
      { provide: StaffAuthService, useValue: { getCurrentUserId: () => userId } },
      { provide: CoachingTeacherAssignmentsService, useValue: service },
      { provide: CoachingTeacherStudentsService, useValue: studentService() }
    ]
  });
  return TestBed.createComponent(CoachingTeacherAssignmentsComponent);
}

function assignmentService() {
  return {
    getTeacherAssignments: vi.fn(() => of(page([]))),
    getAssignment: vi.fn((_id: string) => of({
      id: 'assignment-1', teacherId: 'teacher-1', title: 'Haftalık tekrar', type: 'Individual', source: 'Digital',
      totalStudents: 0, submittedCount: 0,
      dueDate: '2030-01-02T10:00:00Z', status: 'Active', assignedStudents: [], createdAt: '2029-12-01T10:00:00Z'
    } as CoachingAssignmentDetail)),
    createAssignment: vi.fn((_request: CoachingAssignmentCreateRequest, _key: string) =>
      of({ assignmentId: 'assignment-new', dueDate: '2030-01-02T10:00:00Z', assignedStudentCount: 1 })),
    updateAssignment: vi.fn((_id: string, _request: CoachingAssignmentUpdateRequest) =>
      of({ assignmentId: 'assignment-1', dueDate: '2030-01-02T10:00:00Z', assignedStudentCount: 1 })),
    gradeAssignment: vi.fn(() => of({ assignmentId: 'assignment-1', studentId: 'student-1', score: 80, status: 'Graded', gradedAt: '2030-01-03T10:00:00Z' })),
    downloadAttachment: vi.fn(() => of(new Blob(['file'], { type: 'image/png' }))),
    cancelAssignment: vi.fn(() => of({ message: 'Assignment cancelled successfully' }))
  };
}

function studentService() {
  return {
    getMyStudents: vi.fn(() => of({
      items: [{
        userId: 'student-1', firstName: 'Ada', lastName: 'Yılmaz', fullName: 'Ada Yılmaz',
        gradeLevel: 8, assignmentStartDate: '2030-01-01T00:00:00Z'
      }],
      pageNumber: 1, pageSize: 100, totalCount: 1, totalPages: 1
    })),
    getStudentProgress: vi.fn(),
    getStudentHistory: vi.fn()
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

beforeEach(() => TestBed.overrideProvider(ToasterService, {useValue:{confirm:vi.fn().mockResolvedValue(true),success:vi.fn(),error:vi.fn()}}));
