import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CoachingStudyPlanningService } from './coaching-study-planning.service';

describe('staff study planning requests', () => {
  beforeEach(() => TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] }));
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('reads the selected student through the staff API without changing self-service URLs', () => {
    const service = TestBed.inject(CoachingStudyPlanningService);
    const http = TestBed.inject(HttpTestingController);
    service.list(1, undefined, 'student-1').subscribe();
    http.expectOne('/api/coaching/staff/students/student-1/study/plans?pageNumber=1&pageSize=20').flush({ success: true, data: { items: [] } });
    service.list().subscribe();
    http.expectOne('/api/coaching/study-planning/plans?pageNumber=1&pageSize=20').flush({ success: true, data: { items: [] } });
  });

  it('creates and publishes a teacher plan for the selected student with the expected version', () => {
    const service = TestBed.inject(CoachingStudyPlanningService);
    const http = TestBed.inject(HttpTestingController);
    const input = { title: 'Haftalık plan', tasks: [{ plannedDate: '2026-10-19', title: 'Matematik', plannedMinutes: 30, topicId: null, isPinned: true }] };
    service.create(input, 'student-1').subscribe();
    const created = http.expectOne('/api/coaching/staff/students/student-1/study/plans');
    expect(created.request.body).toEqual(input);
    created.flush({ success: true, data: { id: 'plan-1', version: 0 } });
    service.publish('plan-1', 0, 'student-1').subscribe();
    const published = http.expectOne('/api/coaching/staff/students/student-1/study/plans/plan-1/publish');
    expect(published.request.body).toEqual({ expectedVersion: 0 });
    published.flush({ success: true, data: { id: 'plan-1', version: 1, status: 'Active' } });
  });
});
