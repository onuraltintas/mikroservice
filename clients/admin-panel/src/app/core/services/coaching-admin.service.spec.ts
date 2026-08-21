import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CoachingAdminService } from './coaching-admin.service';

describe('CoachingAdminService', () => {
  it('requests bounded assignment pages with optional filters', () => {
    TestBed.configureTestingModule({
      providers: [CoachingAdminService, provideHttpClient(), provideHttpClientTesting()]
    });
    const service = TestBed.inject(CoachingAdminService);
    const http = TestBed.inject(HttpTestingController);

    service.getAssignments({ pageNumber: 2, pageSize: 25, source: 'Book', status: 'Active', search: 'TYT' }).subscribe();

    const request = http.expectOne(candidate => candidate.url.endsWith('/coaching-admin/assignments'));
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('pageNumber')).toBe('2');
    expect(request.request.params.get('pageSize')).toBe('25');
    expect(request.request.params.get('source')).toBe('Book');
    expect(request.request.params.get('status')).toBe('Active');
    expect(request.request.params.get('search')).toBe('TYT');
    request.flush({ items: [], pageNumber: 2, pageSize: 25, totalCount: 0, totalPages: 0 });
    http.verify();
  });

  it('requests assignment detail and builds an attachment content URL', () => {
    TestBed.configureTestingModule({
      providers: [CoachingAdminService, provideHttpClient(), provideHttpClientTesting()]
    });
    const service = TestBed.inject(CoachingAdminService);
    const http = TestBed.inject(HttpTestingController);
    const assignmentId = 'assignment/1';

    service.getAssignment(assignmentId).subscribe();

    const request = http.expectOne(candidate => candidate.url.endsWith('/coaching-admin/assignments/assignment%2F1'));
    expect(request.request.method).toBe('GET');
    request.flush({});

    expect(service.attachmentUrl('a/1', 's/1', 'p/1'))
      .toContain('/assignments/a%2F1/students/s%2F1/attachments/p%2F1/content');
    http.verify();
  });

  it('uses dedicated bounded endpoints for operational coaching records', () => {
    TestBed.configureTestingModule({
      providers: [CoachingAdminService, provideHttpClient(), provideHttpClientTesting()]
    });
    const service = TestBed.inject(CoachingAdminService);
    const http = TestBed.inject(HttpTestingController);

    service.getSessions({ pageNumber: 1, pageSize: 25, status: 'Scheduled', search: 'TYT' }).subscribe();
    const sessions = http.expectOne(candidate => candidate.url.endsWith('/coaching-admin/sessions'));
    expect(sessions.request.params.get('status')).toBe('Scheduled');
    sessions.flush({ items: [], totalCount: 0, totalPages: 0 });

    service.getGoals({ completed: false }).subscribe();
    const goals = http.expectOne(candidate => candidate.url.endsWith('/coaching-admin/goals'));
    expect(goals.request.params.get('completed')).toBe('false');
    goals.flush({ items: [], totalCount: 0, totalPages: 0 });
    http.verify();
  });

  it('sends idempotent admin create requests and keeps resource ids in path', () => {
    TestBed.configureTestingModule({
      providers: [CoachingAdminService, provideHttpClient(), provideHttpClientTesting()]
    });
    const service = TestBed.inject(CoachingAdminService);
    const http = TestBed.inject(HttpTestingController);

    service.createSession({
      teacherId: 'teacher-1', studentId: 'student-1', startTime: '2030-01-01T10:00:00Z',
      durationMinutes: 60, type: 'OneOnOne'
    }, 'session-key-123456').subscribe();
    const session = http.expectOne(candidate => candidate.url.endsWith('/coaching-admin/sessions'));
    expect(session.request.method).toBe('POST');
    expect(session.request.headers.get('Idempotency-Key')).toBe('session-key-123456');
    session.flush({ sessionId: 'session-1' });

    service.updateGoalProgress('goal/1', 75).subscribe();
    const goal = http.expectOne(candidate => candidate.url.endsWith('/coaching-admin/goals/goal%2F1/progress'));
    expect(goal.request.method).toBe('PUT');
    expect(goal.request.body).toEqual({ goalId: 'goal/1', progress: 75 });
    goal.flush({});
    http.verify();
  });

  it('requests session and exam detail resources with encoded ids', () => {
    TestBed.configureTestingModule({
      providers: [CoachingAdminService, provideHttpClient(), provideHttpClientTesting()]
    });
    const service = TestBed.inject(CoachingAdminService);
    const http = TestBed.inject(HttpTestingController);

    service.getSession('session/1').subscribe();
    const session = http.expectOne(candidate => candidate.url.endsWith('/coaching-admin/sessions/session%2F1'));
    expect(session.request.method).toBe('GET');
    session.flush({});

    service.getExam('exam/1').subscribe();
    const exam = http.expectOne(candidate => candidate.url.endsWith('/coaching-admin/exams/exam%2F1'));
    expect(exam.request.method).toBe('GET');
    exam.flush({});
    http.verify();
  });

  it('requests a bounded institution comparison report with optional grade and dates', () => {
    TestBed.configureTestingModule({
      providers: [CoachingAdminService, provideHttpClient(), provideHttpClientTesting()]
    });
    const service = TestBed.inject(CoachingAdminService);
    const http = TestBed.inject(HttpTestingController);

    service.getInstitutionComparison('institution/1', {
      gradeLevel: 8,
      fromDate: '2030-01-01T00:00:00.000Z',
      toDate: '2030-02-01T00:00:00.000Z'
    }).subscribe();

    const request = http.expectOne(candidate => candidate.url.endsWith('/reports/institution/institution%2F1/comparison'));
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('gradeLevel')).toBe('8');
    expect(request.request.params.get('fromDate')).toBe('2030-01-01T00:00:00.000Z');
    expect(request.request.params.get('toDate')).toBe('2030-02-01T00:00:00.000Z');
    request.flush({});
    http.verify();
  });

  it('requests a paged institution early-warning report with bounded filters', () => {
    TestBed.configureTestingModule({
      providers: [CoachingAdminService, provideHttpClient(), provideHttpClientTesting()]
    });
    const service = TestBed.inject(CoachingAdminService);
    const http = TestBed.inject(HttpTestingController);

    service.getInstitutionEarlyWarnings('institution/1', {
      pageNumber: 2,
      pageSize: 10,
      gradeLevel: 8,
      fromDate: '2030-01-01T00:00:00.000Z',
      toDate: '2030-02-01T23:59:59.999Z'
    }).subscribe();

    const request = http.expectOne(candidate => candidate.url.endsWith('/reports/institution/institution%2F1/early-warnings'));
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('pageNumber')).toBe('2');
    expect(request.request.params.get('pageSize')).toBe('10');
    expect(request.request.params.get('gradeLevel')).toBe('8');
    expect(request.request.params.get('fromDate')).toBe('2030-01-01T00:00:00.000Z');
    expect(request.request.params.get('toDate')).toBe('2030-02-01T23:59:59.999Z');
    request.flush({});
    http.verify();
  });
});
