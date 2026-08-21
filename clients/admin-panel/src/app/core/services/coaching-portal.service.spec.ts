import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CoachingPortalService } from './coaching-portal.service';

describe('CoachingPortalService', () => {
  function setup() {
    TestBed.configureTestingModule({
      providers: [CoachingPortalService, provideHttpClient(), provideHttpClientTesting()]
    });

    return {
      service: TestBed.inject(CoachingPortalService),
      http: TestBed.inject(HttpTestingController)
    };
  }

  it('requests bounded student assignment pages with encoded student ids', () => {
    const { service, http } = setup();

    service.getStudentAssignments('student/1', 5000, 999).subscribe();

    const request = http.expectOne(candidate => candidate.url.endsWith('/assignments/student/student%2F1'));
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('pageNumber')).toBe('1000');
    expect(request.request.params.get('pageSize')).toBe('100');
    request.flush({ items: [], pageNumber: 1000, pageSize: 100, totalCount: 0, totalPages: 0 });
    http.verify();
  });

  it('submits the assignment with a trimmed note and encoded id', () => {
    const { service, http } = setup();

    service.submitAssignment('assignment/1', 'student/1', '  tamamladım  ').subscribe();

    const request = http.expectOne(candidate => candidate.url.endsWith('/assignments/assignment%2F1/submit'));
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      assignmentId: 'assignment/1',
      studentId: 'student/1',
      studentNote: 'tamamladım'
    });
    request.flush({});
    http.verify();
  });

  it('loads teacher assignment summaries and grades a student through the protected resource path', () => {
    const { service, http } = setup();

    service.getTeacherAssignments('teacher/1', 2, 25).subscribe();
    const list = http.expectOne(candidate => candidate.url.endsWith('/assignments/teacher/teacher%2F1'));
    expect(list.request.method).toBe('GET');
    expect(list.request.params.get('pageNumber')).toBe('2');
    list.flush({ items: [], pageNumber: 2, pageSize: 25, totalCount: 0, totalPages: 0 });

    service.gradeAssignment('assignment/1', 'student/1', 87.5, 'Güzel çalışma').subscribe();
    const grade = http.expectOne(candidate => candidate.url.endsWith('/assignments/assignment%2F1/grade'));
    expect(grade.request.method).toBe('POST');
    expect(grade.request.body).toEqual({
      assignmentId: 'assignment/1',
      studentId: 'student/1',
      score: 87.5,
      teacherFeedback: 'Güzel çalışma'
    });
    grade.flush({});
    http.verify();
  });

  it('requests the parent child list from the identity-owned endpoint', () => {
    const { service, http } = setup();

    service.getMyChildren().subscribe();

    const request = http.expectOne(candidate => candidate.url.endsWith('/users/me/children'));
    expect(request.request.method).toBe('GET');
    request.flush([]);
    http.verify();
  });

  it('creates attachment metadata before uploading bytes with its hash', () => {
    const { service, http } = setup();
    const file = new File(['photo'], 'ödev.png', { type: 'image/png' });
    const hash = 'a'.repeat(64);

    service.createAttachment('assignment/1', 'student/1', file, hash).subscribe();
    const metadata = http.expectOne(candidate => candidate.url.endsWith('/assignments/assignment%2F1/students/student%2F1/attachments'));
    expect(metadata.request.method).toBe('POST');
    expect(metadata.request.body).toEqual({
      assignmentId: 'assignment/1',
      studentId: 'student/1',
      fileName: 'ödev.png',
      contentType: 'image/png',
      sizeBytes: file.size,
      sha256: hash
    });
    metadata.flush({
      assignmentId: 'assignment/1',
      studentId: 'student/1',
      attachmentId: 'attachment/1',
      uploadUrl: '/api/assignments/assignment%2F1/students/student%2F1/attachments/attachment%2F1/content',
      uploadUrlExpiresAt: '2030-01-01T00:00:00Z',
      status: 'Pending'
    });

    service.uploadAttachment('assignment/1', 'student/1', 'attachment/1', file, hash).subscribe();
    const upload = http.expectOne(candidate => candidate.url.endsWith('/assignments/assignment%2F1/students/student%2F1/attachments/attachment%2F1/content'));
    expect(upload.request.method).toBe('PUT');
    expect(upload.request.headers.get('Content-Type')).toBe('image/png');
    expect(upload.request.headers.get('X-Content-SHA256')).toBe(hash);
    expect(upload.request.body).toBe(file);
    upload.flush({});
    http.verify();
  });
});
