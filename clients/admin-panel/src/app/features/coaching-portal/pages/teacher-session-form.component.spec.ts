import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { AuthService } from '../../../core/auth/auth.service';
import { CoachingPortalService, TeacherStudent } from '../../../core/services/coaching-portal.service';
import { TeacherSessionFormComponent } from './teacher-session-form.component';

describe('TeacherSessionFormComponent', () => {
  let fixture: ComponentFixture<TeacherSessionFormComponent>;
  let component: TeacherSessionFormComponent;
  let service: {
    getTeacherStudents: ReturnType<typeof vi.fn>;
    createTeacherSession: ReturnType<typeof vi.fn>;
  };
  let router: { navigate: ReturnType<typeof vi.fn> };

  const student: TeacherStudent = {
    userId: 'student-1',
    firstName: 'Ada',
    lastName: 'Yılmaz',
    fullName: 'Ada Yılmaz',
    gradeLevel: 8,
    assignmentStartDate: '2030-01-01T00:00:00Z'
  };

  beforeEach(() => {
    service = {
      getTeacherStudents: vi.fn(() => of({ items: [student], pageNumber: 1, pageSize: 100, totalCount: 1, totalPages: 1 })),
      createTeacherSession: vi.fn(() => of({ sessionId: 'session-1' }))
    };
    router = { navigate: vi.fn(() => Promise.resolve(true)) };
    TestBed.configureTestingModule({
      imports: [TeacherSessionFormComponent],
      providers: [
        { provide: CoachingPortalService, useValue: service },
        { provide: AuthService, useValue: { userProfile: signal({ id: 'teacher-1', role: 'Teacher' }).asReadonly() } },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => null } } } },
        { provide: Router, useValue: router }
      ]
    });
    fixture = TestBed.createComponent(TeacherSessionFormComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('creates a future session for an assigned student', () => {
    component.form.subject = '  Matematik  ';
    component.form.startTime = '2030-01-04T10:00';
    component.toggleStudent(student.userId);
    component.submit();

    expect(service.getTeacherStudents).toHaveBeenCalledWith(1, 100);
    expect(service.createTeacherSession).toHaveBeenCalledOnce();
    const [request, idempotencyKey] = service.createTeacherSession.mock.calls[0];
    expect(request).toMatchObject({
      teacherId: 'teacher-1',
      studentId: 'student-1',
      subject: 'Matematik',
      startTime: new Date('2030-01-04T10:00').toISOString(),
      studentIds: ['student-1']
    });
    expect(idempotencyKey).toEqual(expect.any(String));
    expect(router.navigate).toHaveBeenCalledWith(['/coaching-portal/sessions']);
  });

  it('rejects a session without a participant or future start time', () => {
    component.form.startTime = '2000-01-01T10:00';
    component.submit();

    expect(service.createTeacherSession).not.toHaveBeenCalled();
    expect(component.errorMessage()).toContain('gelecekte');
  });

  it('supports searching and paging the teacher roster for large cohorts', () => {
    component.setStudentSearch('  Zeynep  ');
    component.studentTotalPages.set(2);
    component.nextStudentsPage();

    expect(service.getTeacherStudents).toHaveBeenLastCalledWith(2, 100, 'Zeynep');
    expect(component.studentPageNumber()).toBe(2);
  });
});
