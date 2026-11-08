import {TestBed} from '@angular/core/testing';
import {provideHttpClient} from '@angular/common/http';
import {provideHttpClientTesting,HttpTestingController} from '@angular/common/http/testing';
import {CoachingWeeklyDraftService} from './coaching-weekly-draft.service';

describe('weekly draft API',()=>{
  it('reads the owned draft and saves only IDs, timing, version and timezone',()=>{
    TestBed.configureTestingModule({providers:[provideHttpClient(),provideHttpClientTesting()]});
    const service=TestBed.inject(CoachingWeeklyDraftService),http=TestBed.inject(HttpTestingController);let loaded:unknown;
    service.get().subscribe(value=>loaded=value);const get=http.expectOne(r=>r.url.endsWith('/study-planning/weekly-draft'));get.flush({data:{draft:null,lessons:[]}});expect(loaded).toEqual({draft:null,lessons:[]});
    service.save(3,'Europe/Istanbul',[{id:'one',lessonId:'math',lessonName:'Client label',examCode:'LGS',day:'Monday',startMinute:1080,durationMinutes:30}]).subscribe();
    const put=http.expectOne(r=>r.method==='PUT');expect(put.request.body).toEqual({expectedVersion:3,timeZoneId:'Europe/Istanbul',slots:[{id:'one',lessonId:'math',day:'Monday',startMinute:1080,durationMinutes:30}]});put.flush({data:{version:4,timeZoneId:'Europe/Istanbul',slots:[]}});http.verify();
  });

  it('uses the selected student scope for a teacher instead of the teachers own draft',()=>{
    TestBed.configureTestingModule({providers:[provideHttpClient(),provideHttpClientTesting()]});
    const service=TestBed.inject(CoachingWeeklyDraftService),http=TestBed.inject(HttpTestingController);
    service.get('student-1').subscribe();
    http.expectOne('/api/coaching/staff/students/student-1/study/weekly-draft').flush({data:{draft:null,lessons:[]}});
    service.save(2,'Europe/Istanbul',[],'student-1').subscribe();
    const save=http.expectOne(r=>r.method==='PUT');expect(save.request.url).toBe('/api/coaching/staff/students/student-1/study/weekly-draft');
    save.flush({data:{version:3,timeZoneId:'Europe/Istanbul',slots:[]}});http.verify();
  });

  // Paket H2: the proposal is the server's; the teacher asks for the student's.
  it('asks the server for a weekly proposal for the student or, for a teacher, for the chosen student',()=>{
    TestBed.configureTestingModule({providers:[provideHttpClient(),provideHttpClientTesting()]});
    const service=TestBed.inject(CoachingWeeklyDraftService),http=TestBed.inject(HttpTestingController);let loaded:unknown;
    service.proposal('Fill','tyt').subscribe(value=>loaded=value);
    const own=http.expectOne(r=>r.method==='POST'&&r.url.endsWith('/coaching/study-planning/weekly-draft/proposal'));
    expect(own.request.body).toEqual({mode:'Fill',examFocus:'tyt'});
    own.flush({data:{mode:'Fill',slots:[]}});expect(loaded).toEqual({mode:'Fill',slots:[]});
    service.proposal('Replace',null,'student-1').subscribe();
    const staff=http.expectOne('/api/coaching/staff/students/student-1/study/weekly-draft/proposal');
    expect(staff.request.method).toBe('POST');expect(staff.request.body).toEqual({mode:'Replace',examFocus:null});
    staff.flush({data:{mode:'Replace',slots:[]}});http.verify();
  });
});
