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
});
