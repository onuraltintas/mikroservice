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
});
