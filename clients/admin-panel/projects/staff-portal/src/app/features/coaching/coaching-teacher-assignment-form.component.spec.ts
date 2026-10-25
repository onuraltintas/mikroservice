import { ToasterService } from '../../../../../../src/app/core/services/toaster.service';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { StaffAuthService } from '../../auth/staff-auth.service';
import { CoachingTeacherStudentsService } from './coaching-teacher-students.service';
import {
  CoachingAssignmentCreateRequest,
  CoachingAssignmentUpdateRequest,
  CoachingTeacherAssignmentsService
} from './coaching-teacher-assignments.service';
import { CoachingTeacherAssignmentFormComponent } from './coaching-teacher-assignment-form.component';

describe('CoachingTeacherAssignmentFormComponent', () => {
  it('creates a normalized assignment using the authenticated teacher and selected active students', () => {
    const assignments = assignmentService();
    const fixture = createFixture(assignments);
    fixture.detectChanges();
    const component = fixture.componentInstance;
    component.form.title = '  Haftalık tekrar  ';
    component.form.dueDate = '2030-01-02T10:00';
    component.toggleStudent('student-1');
    const saved = vi.spyOn(component.saved, 'emit');

    component.submit();

    expect(assignments.createAssignment).toHaveBeenCalledOnce();
    expect(assignments.createAssignment.mock.calls[0][0]).toMatchObject({
      teacherId: 'teacher-1',
      title: 'Haftalık tekrar',
      studentIds: ['student-1']
    });
    expect(assignments.createAssignment.mock.calls[0][1]).toEqual(expect.any(String));
    expect(saved).toHaveBeenCalledWith('created');
  });

  it('does not save when title or active student selection is missing', () => {
    const assignments = assignmentService();
    const fixture = createFixture(assignments);
    fixture.detectChanges();
    fixture.componentInstance.form.dueDate = '2030-01-02T10:00';

    fixture.componentInstance.submit();

    expect(assignments.createAssignment).not.toHaveBeenCalled();
    expect(fixture.componentInstance.errorMessage()).toContain('zorunlu');
  });

  it('validates future due date and required book page range before saving', () => {
    const assignments = assignmentService();
    const fixture = createFixture(assignments);
    fixture.detectChanges();
    const component = fixture.componentInstance;
    component.form.title = 'Kitap ödevi';
    component.form.dueDate = '2030-01-02T10:00';
    component.form.assignmentSource = 'Book';
    component.toggleStudent('student-1');

    component.submit();

    expect(assignments.createAssignment).not.toHaveBeenCalled();
    expect(component.errorMessage()).toContain('sayfa aralığı');
  });

  it('rejects zero or reversed book question ranges before sending the request', () => {
    const assignments = assignmentService();
    const fixture = createFixture(assignments);
    fixture.detectChanges();
    const component = fixture.componentInstance;
    component.form.title = 'Kitap ödevi';
    component.form.dueDate = '2030-01-02T10:00';
    component.form.assignmentSource = 'Book';
    component.form.bookTitle = 'Matematik';
    component.form.bookStartPage = 10;
    component.form.bookEndPage = 12;
    component.form.bookStartQuestion = 0;
    component.form.bookEndQuestion = 0;
    component.toggleStudent('student-1');

    component.submit();

    expect(assignments.createAssignment).not.toHaveBeenCalled();
    expect(component.errorMessage()).toContain('soru aralığı');
  });

  it('does not update an assignment while one of its assigned students is no longer active', () => {
    const assignments = assignmentService({
      assignedStudents: [{ studentId: 'former-student', status: 'Assigned' }]
    });
    const fixture = createFixture(assignments, 'assignment-1');
    fixture.detectChanges();
    const component = fixture.componentInstance;
    component.form.dueDate = '2030-01-02T10:00';

    component.submit();

    expect(assignments.updateAssignment).not.toHaveBeenCalled();
    expect(component.errorMessage()).toContain('aktif olmayan');
  });

  it('loads an assignment, keeps its active student selected, and sends the update', () => {
    const assignments = assignmentService({
      assignedStudents: [{ studentId: 'student-1', status: 'Assigned' }]
    });
    const fixture = createFixture(assignments, 'assignment-1', ['student-1']);
    fixture.detectChanges();
    const component = fixture.componentInstance;
    component.form.dueDate = '2030-01-02T10:00';
    const saved = vi.spyOn(component.saved, 'emit');

    component.submit();

    expect(component.form.title).toBe('Haftalık tekrar');
    expect(component.isSelected('student-1')).toBe(true);
    expect(assignments.updateAssignment).toHaveBeenCalledWith('assignment-1', expect.objectContaining({
      assignmentId: 'assignment-1',
      title: 'Haftalık tekrar',
      studentIds: ['student-1']
    }));
    expect(saved).toHaveBeenCalledWith('updated');
  });

  it('searches and pages through the teacher-scoped student roster', () => {
    const students = studentService();
    const fixture = createFixture(assignmentService(), null, undefined, students);
    fixture.detectChanges();
    fixture.componentInstance.setStudentSearch('  Zeynep  ');
    fixture.componentInstance.studentTotalPages.set(2);
    fixture.componentInstance.nextStudentsPage();

    expect(students.getMyStudents).toHaveBeenLastCalledWith(2, 100, 'Zeynep');
  });

  it.each([
    ['PDF', 'material.pdf', 'application/pdf'],
    ['Word', 'material.doc', 'application/msword'],
    ['Word', 'material.docx', 'application/vnd.openxmlformats-officedocument.wordprocessingml.document'],
    ['Markdown', 'material.md', 'text/markdown']
  ])('accepts and uploads %s assignment attachments', (_label, name, type) => {
    const assignments = assignmentService();
    const fixture = createFixture(assignments);
    fixture.detectChanges();
    const component = fixture.componentInstance;
    component.form.title = 'Haftalık tekrar';
    component.form.dueDate = '2030-01-02T10:00';
    component.toggleStudent('student-1');
    const file = new File(['content'], name, { type });
    const input = { files: [file], value: 'selected' } as unknown as HTMLInputElement;

    component.onAssignmentFilesSelected({ target: input } as unknown as Event);
    component.submit();

    expect(component.selectedAttachments()).toEqual([]);
    expect(assignments.uploadInstructionAttachment).toHaveBeenCalledWith('assignment-new', file);
  });
});

function createFixture(
  assignments = assignmentService(),
  assignmentId: string | null = null,
  activeStudentIds: string[] = [],
  students = studentService(activeStudentIds)
) {
  TestBed.configureTestingModule({
    imports: [CoachingTeacherAssignmentFormComponent],
    providers: [
      { provide: StaffAuthService, useValue: { getCurrentUserId: () => 'teacher-1' } },
      { provide: CoachingTeacherAssignmentsService, useValue: assignments },
      { provide: CoachingTeacherStudentsService, useValue: students }
    ]
  });
  const fixture = TestBed.createComponent(CoachingTeacherAssignmentFormComponent);
  fixture.componentRef.setInput('assignmentId', assignmentId);
  return fixture;
}

function assignmentService(detail: { assignedStudents?: Array<{ studentId: string; status: string }> } = {}) {
  return {
    getAssignment: vi.fn(() => of({
      id: 'assignment-1',
      teacherId: 'teacher-1',
      title: 'Haftalık tekrar',
      type: 'Individual',
      source: 'Digital',
      dueDate: '2030-01-02T10:00:00Z',
      status: 'Active',
      assignedStudents: detail.assignedStudents ?? [],
      createdAt: '2029-12-01T10:00:00Z'
    })),
    createAssignment: vi.fn((_request: CoachingAssignmentCreateRequest, _idempotencyKey: string) =>
      of({ assignmentId: 'assignment-new', title: 'Ödev', dueDate: '2030-01-02T10:00:00Z', assignedStudentCount: 1 })),
    updateAssignment: vi.fn((_assignmentId: string, _request: CoachingAssignmentUpdateRequest) =>
      of({ assignmentId: 'assignment-1', dueDate: '2030-01-02T10:00:00Z', assignedStudentCount: 1 })),
    uploadInstructionAttachment: vi.fn((_assignmentId: string, _file: File) => of({
      id: 'attachment-1', originalFileName: 'material.pdf', contentType: 'application/pdf',
      sizeBytes: 10, status: 'Clean', uploadedAt: '', scannedAt: ''
    }))
  };
}

function studentService(activeStudentIds: string[] = []) {
  const items = activeStudentIds.map(userId => ({
    userId,
    firstName: 'Ada',
    lastName: 'Yılmaz',
    fullName: 'Ada Yılmaz',
    gradeLevel: 8,
    assignmentStartDate: '2030-01-01T00:00:00Z'
  }));
  return {
    getMyStudents: vi.fn(() => of({ items, pageNumber: 1, pageSize: 100, totalCount: items.length, totalPages: 1 })),
    getStudentProgress: vi.fn(),
    getStudentHistory: vi.fn()
  };
}

beforeEach(() => { TestBed.overrideProvider(ToasterService, {useValue:{confirm:vi.fn().mockResolvedValue(true),success:vi.fn(),error:vi.fn(),info:vi.fn(),warning:vi.fn()}}); });
