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
    staff.flush({data:{mode:'Replace',slots:[]}});
    // Review of PR 130 (13): a teacher's answers for a student without study times go with the request.
    service.proposal('Fill',null,'student-1',[{day:'Monday',startMinute:1020,endMinute:1140}]).subscribe();
    const answered=http.expectOne('/api/coaching/staff/students/student-1/study/weekly-draft/proposal');
    expect(answered.request.body).toEqual({mode:'Fill',examFocus:null,windows:[{day:'Monday',startMinute:1020,endMinute:1140}]});
    answered.flush({data:{mode:'Fill',slots:[]}});http.verify();
  });
  // Paket H4: the tempo's finish date goes with the proposal; the tempo options are asked the same way.
  it('sends finishBy only when given and asks for the tempo options of the student or the chosen student',()=>{
    TestBed.configureTestingModule({providers:[provideHttpClient(),provideHttpClientTesting()]});
    const service=TestBed.inject(CoachingWeeklyDraftService),http=TestBed.inject(HttpTestingController);
    service.proposal('Fill','ayt',undefined,undefined,'2027-05-12').subscribe();
    const own=http.expectOne('/api/coaching/study-planning/weekly-draft/proposal');
    expect(own.request.body).toEqual({mode:'Fill',examFocus:'ayt',finishBy:'2027-05-12'});own.flush({data:{}});
    service.proposal('Replace',null,undefined,undefined,null).subscribe();
    const none=http.expectOne('/api/coaching/study-planning/weekly-draft/proposal');
    expect(none.request.body).toEqual({mode:'Replace',examFocus:null});none.flush({data:{}});
    let options:unknown;service.tempoOptions('tyt',undefined,undefined,'Replace').subscribe(x=>options=x);
    const tempo=http.expectOne(r=>r.method==='POST'&&r.url==='/api/coaching/study-planning/weekly-draft/tempo-options');
    expect(tempo.request.body).toEqual({examFocus:'tyt',mode:'Replace'});tempo.flush({data:{planEnd:'2027-06-13',options:[]}});
    expect(options).toEqual({planEnd:'2027-06-13',options:[]});
    service.tempoOptions(null,'student-1',[{day:'Monday',startMinute:1020,endMinute:1140}]).subscribe();
    const staff=http.expectOne('/api/coaching/staff/students/student-1/study/weekly-draft/tempo-options');
    expect(staff.request.body).toEqual({examFocus:null,mode:'Fill',windows:[{day:'Monday',startMinute:1020,endMinute:1140}]});staff.flush({data:{}});http.verify();
  });
});
