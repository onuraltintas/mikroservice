import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CoachingStudyPlanningService } from './coaching-study-planning.service';

describe('CoachingStudyPlanningService', () => {
  it('searches topics and previews without sending an owner or creating a plan', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(CoachingStudyPlanningService);
    const http = TestBed.inject(HttpTestingController);
    service.searchTopics(' math ', 8, 'LGS', 2).subscribe();
    http.expectOne('/api/coaching/study-planning/topics?search=math&pageNumber=2&pageSize=20&gradeNumber=8&examCode=LGS')
      .flush({ success: true, data: { items: [], totalCount: 0, pageNumber: 2, pageSize: 20 } });
    const input = { startDate: '2026-10-05', days: 7, expectedAvailabilityVersion: 3, topics: [{ topicId: 'topic', requiredMinutes: 30 }] };
    service.previewAutomatic(input).subscribe();
    const request = http.expectOne('/api/coaching/study-planning/automatic-preview');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(input);
    request.flush({ success: true, data: {} });
    http.verify();
  });
  it('searches targets with filters and saves only the goal target and version', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(CoachingStudyPlanningService);
    const http = TestBed.inject(HttpTestingController);
    service.searchSchools('science', 'Ankara', 'Center', 2).subscribe();
    const school = http.expectOne('/api/coaching/study-planning/targets/schools?search=science&city=Ankara&district=Center&pageNumber=2&pageSize=20');
    school.flush({ success: true, data: { items: [], totalCount: 0, pageNumber: 2, pageSize: 20 } });
    service.searchPrograms('Software', 'SAY', 1).subscribe();
    http.expectOne('/api/coaching/study-planning/targets/university-programs?search=Software&scoreType=SAY&pageNumber=1&pageSize=20')
      .flush({ success: true, data: { items: [], totalCount: 0, pageNumber: 1, pageSize: 20 } });
    service.getGoalTarget('goal').subscribe();
    http.expectOne('/api/coaching/study-planning/goals/goal/target').flush({ success: true, data: {} });
    service.saveGoalTarget('goal', 3, null, 'school').subscribe();
    const save = http.expectOne('/api/coaching/study-planning/goals/goal/target');
    expect(save.request.body).toEqual({ expectedVersion: 3, targetUniversityProgramId: null, targetSchoolId: 'school' });
    save.flush({ success: true, data: {} });
    http.verify();
  });
  it('uses availability endpoints and never supplies a student ID', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(CoachingStudyPlanningService);
    const http = TestBed.inject(HttpTestingController);
    service.getAvailability().subscribe();
    http.expectOne('/api/coaching/study-planning/availability').flush({ success: true, data: { version: 0, timeZoneId: 'UTC', windows: [] } });
    const update = { expectedVersion: null, timeZoneId: 'Europe/Istanbul', windows: [{ day: 'Monday' as const, startMinute: 480, endMinute: 540 }] };
    service.saveAvailability(update).subscribe();
    const request = http.expectOne('/api/coaching/study-planning/availability');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual(update);
    request.flush({ success: true, data: { version: 0, timeZoneId: 'Europe/Istanbul', windows: update.windows } });
    http.verify();
  });
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
