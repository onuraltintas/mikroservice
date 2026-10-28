import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CoachingStudyPlanningService } from './coaching-study-planning.service';

describe('CoachingStudyPlanningService', () => {
  it('routes calendar task writes to the owner or selected student with optimistic concurrency',()=>{
    TestBed.configureTestingModule({providers:[provideHttpClient(),provideHttpClientTesting()]});const service=TestBed.inject(CoachingStudyPlanningService),http=TestBed.inject(HttpTestingController);
    const input={expectedVersion:7,title:'Tekrar',plannedDate:'2027-01-04',startMinute:1080,plannedMinutes:30,lessonId:'math'};
    service.saveCalendarTask('p',null,input).subscribe();const add=http.expectOne('/api/coaching/study-planning/plans/p/tasks');expect(add.request.method).toBe('POST');expect(add.request.body).toEqual(input);add.flush({data:{}});
    service.saveCalendarTask('p','t',input,'s').subscribe();const edit=http.expectOne('/api/coaching/staff/students/s/study/plans/p/tasks/t');expect(edit.request.method).toBe('PUT');expect(edit.request.body).toEqual(input);edit.flush({data:{}});
    service.deleteCalendarTask('p','t',8,'s').subscribe();const remove=http.expectOne('/api/coaching/staff/students/s/study/plans/p/tasks/t');expect(remove.request.method).toBe('DELETE');expect(remove.request.body).toEqual({expectedVersion:8});remove.flush({data:{}});http.verify();
  });
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
    service.saveAutomaticDraft({ title: 'Plan', preview: input, expectedActiveRevisionId: null, expectedActiveRevisionVersion: null }).subscribe();
    const save = http.expectOne('/api/coaching/study-planning/automatic-drafts');
    expect(save.request.body.expectedActiveRevisionId).toBeNull();
    expect(save.request.body.preview).toEqual(input);
    save.flush({ success: true, data: {} });
    http.verify();
  });
  it('saves only the selected verified goal target and version', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(CoachingStudyPlanningService);
    const http = TestBed.inject(HttpTestingController);
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
    service.complete('plan', 'task', 7, 45, 0).subscribe();
    const complete = http.expectOne('/api/coaching/study-planning/plans/plan/tasks/task/completion');
    expect(complete.request.method).toBe('PUT');
    expect(complete.request.body).toEqual({ expectedVersion: 7, actualMinutes: 45, actualQuestions: 0 });
    complete.flush({ success: true, data: {} });
    service.reschedule('plan', 'task', 8, '2026-10-07').subscribe();
    const move = http.expectOne('/api/coaching/study-planning/plans/plan/tasks/task/schedule');
    expect(move.request.body).toEqual({ expectedVersion: 8, plannedDate: '2026-10-07' });
    move.flush({ success: true, data: {} });
    service.skip('plan', 'task', 9).subscribe();
    const skip = http.expectOne('/api/coaching/study-planning/plans/plan/tasks/task/skip');
    expect(skip.request.method).toBe('PUT');
    expect(skip.request.body).toEqual({ expectedVersion: 9 });
    skip.flush({ success: true, data: {} });
    http.verify();
  });
});
