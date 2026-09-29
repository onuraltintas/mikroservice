import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { firstValueFrom } from 'rxjs';
import { CoachingTeacherAssignmentsService } from './coaching-teacher-assignments.service';

describe('CoachingTeacherAssignmentsService', () => {
  let http: HttpTestingController;
  let service: CoachingTeacherAssignmentsService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [CoachingTeacherAssignmentsService, provideHttpClient(), provideHttpClientTesting()]
    });
    http = TestBed.inject(HttpTestingController);
    service = TestBed.inject(CoachingTeacherAssignmentsService);
  });

  afterEach(() => http.verify());

  it('loads a paged, status-filtered list for the authenticated teacher id', async () => {
    const response = firstValueFrom(service.getTeacherAssignments('teacher-1', 2, 10, 'Completed'));
    const request = http.expectOne('/api/assignments/teacher/teacher-1?pageNumber=2&pageSize=10&status=Completed');
    expect(request.request.method).toBe('GET');
    request.flush({ items: [], pageNumber: 2, pageSize: 10, totalCount: 0, totalPages: 1 });

    await expect(response).resolves.toMatchObject({ pageNumber: 2, pageSize: 10 });
  });

  it('cancels an assignment through the server-authorized endpoint', async () => {
    const response = firstValueFrom(service.cancelAssignment('assignment-1'));
    const request = http.expectOne('/api/assignments/assignment-1/cancel');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({});
    request.flush({ message: 'Assignment cancelled successfully' });

    await expect(response).resolves.toEqual({ message: 'Assignment cancelled successfully' });
  });

  it('loads an assignment by its encoded id for edit authorization on the server', async () => {
    const response = firstValueFrom(service.getAssignment('assignment/1'));
    const request = http.expectOne('/api/assignments/assignment%2F1');
    expect(request.request.method).toBe('GET');
    request.flush({ id: 'assignment/1', assignedStudents: [] });

    await expect(response).resolves.toMatchObject({ id: 'assignment/1' });
  });

  it('creates a normalized assignment with an idempotency key', async () => {
    const response = firstValueFrom(service.createAssignment({
      teacherId: 'teacher-1',
      title: '  Haftalık tekrar  ',
      assignmentType: 'Individual',
      assignmentSource: 'Digital',
      dueDate: '2030-01-02T10:00:00.000Z',
      studentIds: ['student-1', 'student-1']
    }, 'idempotency-key'));
    const request = http.expectOne('/api/assignments');
    expect(request.request.method).toBe('POST');
    expect(request.request.headers.get('Idempotency-Key')).toBe('idempotency-key');
    expect(request.request.body).toMatchObject({
      teacherId: 'teacher-1',
      title: 'Haftalık tekrar',
      studentIds: ['student-1']
    });
    request.flush({ assignmentId: 'assignment-1', dueDate: '2030-01-02T10:00:00Z', assignedStudentCount: 1 });

    await expect(response).resolves.toMatchObject({ assignmentId: 'assignment-1' });
  });

  it('updates the requested assignment through its matching resource route', async () => {
    const response = firstValueFrom(service.updateAssignment('assignment-1', {
      assignmentId: 'assignment-1',
      title: 'Ödev güncellendi',
      assignmentSource: 'Digital',
      dueDate: '2030-01-02T10:00:00.000Z',
      studentIds: ['student-1']
    }));
    const request = http.expectOne('/api/assignments/assignment-1');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toMatchObject({ assignmentId: 'assignment-1', title: 'Ödev güncellendi' });
    request.flush({ assignmentId: 'assignment-1', dueDate: '2030-01-02T10:00:00Z', assignedStudentCount: 1 });

    await expect(response).resolves.toMatchObject({ assignmentId: 'assignment-1' });
  });

  it('grades a student submission with normalized teacher feedback', async () => {
    const response = firstValueFrom(service.gradeAssignment('assignment-1', 'student-1', 82, '  Güzel çalışma  '));
    const request = http.expectOne('/api/assignments/assignment-1/grade');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      assignmentId: 'assignment-1', studentId: 'student-1', score: 82, teacherFeedback: 'Güzel çalışma'
    });
    request.flush({ assignmentId: 'assignment-1', studentId: 'student-1', score: 82, status: 'Graded', gradedAt: '2030-01-03T10:00:00Z' });

    await expect(response).resolves.toMatchObject({ score: 82, status: 'Graded' });
  });

  it('downloads a submission through the authenticated attachment endpoint as a blob', async () => {
    const response = firstValueFrom(service.downloadAttachment('assignment-1', 'student-1', 'attachment-1'));
    const request = http.expectOne('/api/assignments/assignment-1/students/student-1/attachments/attachment-1/content');
    expect(request.request.method).toBe('GET');
    expect(request.request.responseType).toBe('blob');
    request.flush(new Blob(['submission'], { type: 'image/png' }));

    await expect(response).resolves.toBeInstanceOf(Blob);
  });
});
