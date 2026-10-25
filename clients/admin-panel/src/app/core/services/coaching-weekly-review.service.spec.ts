import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { CoachingWeeklyReviewService, WeeklyDecision } from './coaching-weekly-review.service';

describe('CoachingWeeklyReviewService', () => {
  let service: CoachingWeeklyReviewService; let http: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(CoachingWeeklyReviewService); http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());
  it('sends source lineage and displayed evidence with a new decision and a repeated review',()=>{
    const body={expectedVersion:null,title:'Yeni karar',note:'',dueOn:'2026-10-28',status:'Planned',sourceReflectionVersion:4,previousDecisionId:'old',expectedResultsFingerprint:'current-results'};
    service.decide('student','2026-10-12','new',body).subscribe();const decision=http.expectOne(r=>r.url.endsWith('/decisions/new'));expect(decision.request.body).toEqual(body);decision.flush({data:{}});
    service.reviewReflection('student','2026-10-12',4,'Sonuçları yeniden inceledim','current-results').subscribe();const review=http.expectOne(r=>r.url.endsWith('/2026-10-12/review'));
    expect(review.request.body).toEqual({expectedReflectionVersion:4,note:'Sonuçları yeniden inceledim',expectedResultsFingerprint:'current-results'});review.flush({data:{}});
  });
  it('sends postponement separately with the exact decision version and reason',()=>{
    const body={expectedVersion:4,dueOn:'2026-10-28',reason:'Hastalık nedeniyle ek süre'};
    service.postpone('student','2026-10-05','decision',body).subscribe();
    const request=http.expectOne(r=>r.url.endsWith('/staff/students/student/study/weekly-review/2026-10-05/decisions/decision/postpone'));
    expect(request.request.method).toBe('POST');expect(request.request.body).toEqual(body);request.flush({data:{}});
  });
  it('sends future deadline changes with their reason and exact version through the dedicated route',()=>{
    const body={expectedVersion:5,dueOn:'2026-10-24',reason:'Takvim erkene alındı'};
    service.changeDeadline('student','2026-10-05','decision',body).subscribe();
    const request=http.expectOne(r=>r.url.endsWith('/2026-10-05/decisions/decision/deadline-changes'));
    expect(request.request.method).toBe('POST');expect(request.request.body).toEqual(body);request.flush({data:{}});
  });
  it('keeps linking separate from calendar preview and sends exact versions and explicit approval',()=>{
    const row={id:'decision',version:4,reviewWeekStart:'2026-10-12'} as WeeklyDecision;
    service.linkWork('student',row,'task','assignment').subscribe();const link=http.expectOne(r=>r.url.endsWith('/decisions/decision/work-links'));
    expect(link.request.method).toBe('PUT');expect(link.request.body).toEqual({expectedVersion:4,studyTaskId:'task',assignmentId:'assignment'});link.flush({data:{}});
    const proposal={expectedDecisionVersion:4,expectedPlanVersion:7,planId:'plan',taskId:null,date:'2026-10-22',startMinute:1080,minutes:30};
    service.previewWork('student',row,proposal).subscribe();const preview=http.expectOne(r=>r.url.endsWith('/study-preview'));expect(preview.request.body).toEqual(proposal);preview.flush({data:{proposal,fingerprint:'hash'}});
    service.applyWork('student',row,{proposal,fingerprint:'hash'} as any,'stable').subscribe();const apply=http.expectOne(r=>r.url.endsWith('/study-apply'));
    expect(apply.request.body).toEqual({proposal,fingerprint:'hash',confirmed:true,requestId:'stable'});apply.flush({data:{}});
    service.createWorkAssignment('student',row,'2026-10-23T12:00:00Z',30,'request').subscribe();const assignment=http.expectOne(r=>r.url.endsWith('/decisions/decision/assignment'));
    expect(assignment.request.body).toEqual({expectedVersion:4,dueAt:'2026-10-23T12:00:00Z',estimatedMinutes:30,requestId:'request'});assignment.flush({data:{}});
  });
  it('loads a small home summary and asks the server for the selected decision page',()=>{
    service.home().subscribe();http.expectOne(r=>r.url.endsWith('/weekly-review/home')).flush({data:{}});
    service.get('student','2026-10-12',1,'decision').subscribe();const request=http.expectOne(r=>r.params.get('decisionId')==='decision');
    expect(request.request.params.get('weekStart')).toBe('2026-10-12');request.flush({data:{}});
  });
  it('reads own and selected student weeks and pages through the API envelope', () => {
    let result: unknown;
    service.get(null, '2026-10-12').subscribe(value => result = value);
    const own = http.expectOne(r => r.url.endsWith('/coaching/study-planning/weekly-review'));
    expect(own.request.params.get('weekStart')).toBe('2026-10-12'); expect(own.request.params.get('decisionPage')).toBe('1');
    own.flush({ data: { weekStart: '2026-10-12' } }); expect(result).toEqual({ weekStart: '2026-10-12' });
    service.get('student', '2026-10-19', 2).subscribe();
    const staff = http.expectOne(r => r.url.endsWith('/coaching/staff/students/student/study/weekly-review'));
    expect(staff.request.params.get('decisionPage')).toBe('2'); staff.flush({ data: {} });
  });
  it('keeps student reflection and teacher decision writes separate with optimistic versions', () => {
    const reflection = { expectedVersion: 3, successes: 'İyi', obstacles: '', nextFocus: 'Tekrar', rating: 4 };
    service.reflect('2026-10-12', reflection).subscribe();
    const own = http.expectOne(r => r.url.endsWith('/study-planning/weekly-review/2026-10-12'));
    expect(own.request.method).toBe('PUT'); expect(own.request.body).toEqual(reflection); own.flush({ data: {} });
    const decision = { expectedVersion: null, title: 'Tekrar', note: '', dueOn: '2026-10-19', status: 'Planned' };
    service.decide('student', '2026-10-12', 'stable-id', decision).subscribe();
    const staff = http.expectOne(r => r.url.endsWith('/staff/students/student/study/weekly-review/2026-10-12/decisions/stable-id'));
    expect(staff.request.body).toEqual(decision); staff.flush({ data: {} });
  });
});
