import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { SpeedReadingInstitutionRosterService } from './speed-reading-institution-roster.service';

describe('SpeedReadingInstitutionRosterService', () => {
  it('scopes member and teacher-student operations to the selected institution', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(SpeedReadingInstitutionRosterService);
    const http = TestBed.inject(HttpTestingController);

    service.getMembers('institution-1', 2, 25, 1, 'Ada').subscribe();
    const members = http.expectOne(request => request.url === '/api/speed-reading/institutions/institution-1/members');
    expect(members.request.params.get('role')).toBe('1');
    expect(members.request.params.get('searchTerm')).toBe('Ada');
    expect(members.request.params.get('pageNumber')).toBe('2');
    members.flush({ items: [], totalCount: 0, pageNumber: 2, pageSize: 25 });

    service.assignStudent('institution-1', 'teacher-1', 'student-1').subscribe();
    const assignment = http.expectOne('/api/speed-reading/institutions/institution-1/teachers/teacher-1/students/student-1');
    expect(assignment.request.method).toBe('PUT');
    assignment.flush(null);

    service.removeStudent('institution-1', 'teacher-1', 'student-1').subscribe();
    const removal = http.expectOne('/api/speed-reading/institutions/institution-1/teachers/teacher-1/students/student-1');
    expect(removal.request.method).toBe('DELETE');
    removal.flush(null);
    http.verify();
  });
});
