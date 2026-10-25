import {TestBed} from '@angular/core/testing';
import {provideHttpClient} from '@angular/common/http';
import {provideHttpClientTesting,HttpTestingController} from '@angular/common/http/testing';
import {CoachingWeeklyOutcomePlanService} from './coaching-weekly-outcome-plan.service';

describe('weekly outcome plan API',()=>{
  it('reads options, previews dates and creates only with the reviewed fingerprint',()=>{
    TestBed.configureTestingModule({providers:[provideHttpClient(),provideHttpClientTesting()]});const service=TestBed.inject(CoachingWeeklyOutcomePlanService),http=TestBed.inject(HttpTestingController);
    let received:unknown;service.options().subscribe(x=>received=x);http.expectOne(r=>r.url.endsWith('/weekly-outcome-plans/options')).flush({data:{weeklyDraftVersion:3}});expect(received).toEqual({weeklyDraftVersion:3});
    const request={startDate:'2027-05-24',endDate:'2027-06-01',expectedWeeklyDraftVersion:3,reuseFreedSlots:true};
    service.preview(request).subscribe(x=>received=x);const preview=http.expectOne(r=>r.url.endsWith('/weekly-outcome-plans/preview'));expect(preview.request.body).toEqual(request);preview.flush({data:{fingerprint:'reviewed'}});expect(received).toEqual({fingerprint:'reviewed'});
    const save={title:'Plan',preview:request,fingerprint:'reviewed'};service.create(save).subscribe(x=>received=x);const create=http.expectOne(r=>r.url.endsWith('/weekly-outcome-plans/drafts'));expect(create.request.method).toBe('POST');expect(create.request.body).toEqual(save);create.flush({data:{id:'new',version:0}});expect(received).toEqual({id:'new',version:0});http.verify();
  });

  it('scopes options, previews and draft creation to a teacher-selected student',()=>{
    TestBed.configureTestingModule({providers:[provideHttpClient(),provideHttpClientTesting()]});const service=TestBed.inject(CoachingWeeklyOutcomePlanService),http=TestBed.inject(HttpTestingController);
    service.options('student-1').subscribe();http.expectOne('/api/coaching/staff/students/student-1/study/weekly-outcome-plans/options').flush({data:{weeklyDraftVersion:4}});
    const request={startDate:'2027-05-24',endDate:'2027-06-01',expectedWeeklyDraftVersion:4,reuseFreedSlots:false};
    service.preview(request,'student-1').subscribe();const preview=http.expectOne('/api/coaching/staff/students/student-1/study/weekly-outcome-plans/preview');expect(preview.request.body).toEqual(request);preview.flush({data:{fingerprint:'student-fingerprint'}});
    const save={title:'Öğrenci planı',preview:request,fingerprint:'student-fingerprint'};service.create(save,'student-1').subscribe();const create=http.expectOne('/api/coaching/staff/students/student-1/study/weekly-outcome-plans/drafts');expect(create.request.body).toEqual(save);create.flush({data:{id:'draft',version:0}});http.verify();
  });
});
