import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { CoachingAssignmentDetail, CoachingTeacherAssignmentsService } from './coaching-teacher-assignments.service';
import { CoachingTeacherStudentsService } from './coaching-teacher-students.service';
import { CoachingTeacherAssignmentDetailComponent } from './coaching-teacher-assignment-detail.component';

describe('CoachingTeacherAssignmentDetailComponent', () => {
  it('loads the assigned student names, submission status and existing feedback', () => {
    const assignments = assignmentService();
    const fixture = createFixture(assignments);
    fixture.detectChanges();

    expect(assignments.getAssignment).toHaveBeenCalledWith('assignment-1');
    expect(fixture.nativeElement.textContent).toContain('Ada Yılmaz');
    expect(fixture.nativeElement.textContent).toContain('Teslim edildi');
    expect(fixture.nativeElement.textContent).toContain('İyi çalışma');
    expect(fixture.nativeElement.textContent).toContain('ödev-foto.png');
  });

  it('rejects a negative score or a score above the assignment maximum', () => {
    const assignments = assignmentService();
    const fixture = createFixture(assignments);
    fixture.detectChanges();
    const component = fixture.componentInstance;
    component.updateGradeDraft('student-1', 'score', 101);
    component.grade('student-1');

    expect(assignments.gradeAssignment).not.toHaveBeenCalled();
    expect(component.errorMessage()).toContain('0 ile 100');
  });

  it('saves a valid grade and reloads the server-authoritative assignment state', () => {
    const assignments = assignmentService();
    const fixture = createFixture(assignments);
    fixture.detectChanges();
    const component = fixture.componentInstance;
    component.updateGradeDraft('student-1', 'score', 82);
    component.updateGradeDraft('student-1', 'feedback', '  Güzel çalışma  ');

    component.grade('student-1');

    expect(assignments.gradeAssignment).toHaveBeenCalledWith('assignment-1', 'student-1', 82, 'Güzel çalışma');
    expect(assignments.getAssignment).toHaveBeenCalledTimes(2);
    expect(component.successMessage()).toBe('Değerlendirme kaydedildi.');
  });

  it('allows clean attachments to download and blocks attachments awaiting or failing security scans', () => {
    const assignments = assignmentService();
    const fixture = createFixture(assignments);
    const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined);
    vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:test');
    vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined);
    fixture.detectChanges();
    const component = fixture.componentInstance;
    const attachments = fixture.componentInstance.assignment()!.assignedStudents[0].attachments!;

    component.downloadAttachment('student-1', attachments[1]);
    component.downloadAttachment('student-1', attachments[0]);

    expect(assignments.downloadAttachment).toHaveBeenCalledOnce();
    expect(assignments.downloadAttachment).toHaveBeenCalledWith('assignment-1', 'student-1', 'attachment-clean');
    expect(click).toHaveBeenCalledOnce();
  });

  it('returns to the assignment list when the back action is used', () => {
    const fixture = createFixture(assignmentService());
    const close = vi.spyOn(fixture.componentInstance.close, 'emit');
    fixture.componentInstance.goBack();

    expect(close).toHaveBeenCalledOnce();
  });
});

function createFixture(assignments = assignmentService()) {
  TestBed.configureTestingModule({
    imports: [CoachingTeacherAssignmentDetailComponent],
    providers: [
      { provide: CoachingTeacherAssignmentsService, useValue: assignments },
      { provide: CoachingTeacherStudentsService, useValue: {
        getMyStudents: vi.fn(() => of({
          items: [{ userId: 'student-1', firstName: 'Ada', lastName: 'Yılmaz', fullName: 'Ada Yılmaz', gradeLevel: 8, assignmentStartDate: '2030-01-01T00:00:00Z' }],
          pageNumber: 1, pageSize: 100, totalCount: 1, totalPages: 1
        }))
      } }
    ]
  });
  const fixture = TestBed.createComponent(CoachingTeacherAssignmentDetailComponent);
  fixture.componentRef.setInput('assignmentId', 'assignment-1');
  return fixture;
}

function assignmentService() {
  const detail: CoachingAssignmentDetail = {
    id: 'assignment-1', teacherId: 'teacher-1', title: 'Haftalık tekrar', type: 'Individual', source: 'Digital',
    dueDate: '2030-01-02T10:00:00Z', status: 'Active', totalStudents: 1, submittedCount: 1,
    createdAt: '2029-12-01T10:00:00Z', maxScore: 100,
    assignedStudents: [{
      studentId: 'student-1', status: 'Submitted', score: 80, teacherFeedback: 'İyi çalışma',
      attachments: [
        { id: 'attachment-pending', originalFileName: 'bekliyor.png', contentType: 'image/png', sizeBytes: 1024, status: 'Pending' },
        { id: 'attachment-clean', originalFileName: 'ödev-foto.png', contentType: 'image/png', sizeBytes: 2048, status: 'Clean' }
      ]
    }]
  };
  return {
    getAssignment: vi.fn(() => of(detail)),
    gradeAssignment: vi.fn(() => of({ assignmentId: 'assignment-1', studentId: 'student-1', score: 82, status: 'Graded', gradedAt: '2030-01-03T10:00:00Z' })),
    downloadAttachment: vi.fn(() => of(new Blob(['file'], { type: 'image/png' })))
  };
}
