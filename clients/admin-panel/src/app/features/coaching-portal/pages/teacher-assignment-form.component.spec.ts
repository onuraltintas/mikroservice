import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { AuthService } from '../../../core/auth/auth.service';
import { CoachingPortalService, TeacherStudent } from '../../../core/services/coaching-portal.service';
import { TeacherAssignmentFormComponent } from './teacher-assignment-form.component';

describe('TeacherAssignmentFormComponent', () => {
  let fixture: ComponentFixture<TeacherAssignmentFormComponent>;
  let component: TeacherAssignmentFormComponent;
  let service: {
    getTeacherStudents: ReturnType<typeof vi.fn>;
    createTeacherAssignment: ReturnType<typeof vi.fn>;
    getAssignment: ReturnType<typeof vi.fn>;
    updateTeacherAssignment: ReturnType<typeof vi.fn>;
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
      createTeacherAssignment: vi.fn(() => of({ assignmentId: 'assignment-1', title: 'Ödev', dueDate: '2030-01-02T00:00:00Z', assignedStudentCount: 1 })),
      getAssignment: vi.fn(),
      updateTeacherAssignment: vi.fn(() => of({ assignmentId: 'assignment-1', dueDate: '2030-01-02T00:00:00Z', assignedStudentCount: 1 }))
    };
    router = { navigate: vi.fn(() => Promise.resolve(true)) };

    TestBed.configureTestingModule({
      imports: [TeacherAssignmentFormComponent],
      providers: [
        { provide: CoachingPortalService, useValue: service },
        { provide: AuthService, useValue: { userProfile: signal({ id: 'teacher-1', role: 'Teacher' }).asReadonly() } },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => null } } } },
        { provide: Router, useValue: router }
      ]
    });
    fixture = TestBed.createComponent(TeacherAssignmentFormComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('loads only the teacher roster and creates a normalized assignment', () => {
    component.form.title = '  Haftalık tekrar  ';
    component.form.dueDate = '2030-01-02T10:00';
    component.toggleStudent(student.userId);
    component.submit();

    expect(service.getTeacherStudents).toHaveBeenCalledWith(1, 100);
    expect(service.createTeacherAssignment).toHaveBeenCalledOnce();
    const [request, idempotencyKey] = service.createTeacherAssignment.mock.calls[0];
    expect(request).toMatchObject({
      teacherId: 'teacher-1',
      title: 'Haftalık tekrar',
      dueDate: new Date('2030-01-02T10:00').toISOString(),
      studentIds: ['student-1']
    });
    expect(idempotencyKey).toEqual(expect.any(String));
    expect(router.navigate).toHaveBeenCalledWith(['/coaching-portal/teacher/assignments', 'assignment-1']);
  });

  it('does not submit without a title or selected student', () => {
    component.form.dueDate = '2030-01-02T10:00';
    component.submit();

    expect(service.createTeacherAssignment).not.toHaveBeenCalled();
    expect(component.errorMessage()).toContain('başlık');
  });

  it('supports searching and paging beyond the first 100 assigned students', () => {
    component.setStudentSearch('  Zeynep  ');
    component.studentTotalPages.set(2);
    component.nextStudentsPage();

    expect(service.getTeacherStudents).toHaveBeenLastCalledWith(2, 100, 'Zeynep');
    expect(component.studentPageNumber()).toBe(2);
  });
});
