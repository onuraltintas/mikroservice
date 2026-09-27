import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AssignmentService } from './assignment.service';

describe('institution assignments API', () => {
  it('queries only the institution-scoped route with teacher filter', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(AssignmentService);
    const http = TestBed.inject(HttpTestingController);

    service.getInstitutionAssignments('institution-1', 2, 25, 'reading', true, undefined, 'teacher-1').subscribe();

    const request = http.expectOne(req => req.url.endsWith('/institutions/institution-1/assignments'));
    expect(request.request.params.get('teacherId')).toBe('teacher-1');
    expect(request.request.params.get('pageNumber')).toBe('2');
    request.flush({ items: [], totalCount: 0, pageNumber: 2, pageSize: 25 });
    http.verify();
  });
});
