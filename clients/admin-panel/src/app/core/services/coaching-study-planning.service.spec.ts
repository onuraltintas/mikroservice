import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CoachingStudyPlanningService } from './coaching-study-planning.service';

describe('CoachingStudyPlanningService', () => {
  it('uses product-specific endpoints, unwraps data and sends versions without owner IDs', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(CoachingStudyPlanningService);
    const http = TestBed.inject(HttpTestingController);
    service.list(2, 'Active').subscribe(page => expect(page.totalCount).toBe(1));
    const list = http.expectOne('/api/coaching/study-planning/plans?pageNumber=2&pageSize=20&status=Active');
    list.flush({ success: true, data: { items: [], totalCount: 1, pageNumber: 2, pageSize: 20 } });
    service.complete('plan', 'task', 7, 45).subscribe();
    const complete = http.expectOne('/api/coaching/study-planning/plans/plan/tasks/task/completion');
    expect(complete.request.method).toBe('PUT');
    expect(complete.request.body).toEqual({ expectedVersion: 7, actualMinutes: 45 });
    complete.flush({ success: true, data: {} });
    service.reschedule('plan', 'task', 8, '2026-10-07').subscribe();
    const move = http.expectOne('/api/coaching/study-planning/plans/plan/tasks/task/schedule');
    expect(move.request.body).toEqual({ expectedVersion: 8, plannedDate: '2026-10-07' });
    move.flush({ success: true, data: {} });
    http.verify();
  });
});
