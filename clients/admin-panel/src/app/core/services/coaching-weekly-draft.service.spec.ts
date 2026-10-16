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
});
