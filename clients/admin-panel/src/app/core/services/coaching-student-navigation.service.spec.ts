import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { CoachingStudentNavigationService } from './coaching-student-navigation.service';

describe('student navigation visibility', () => {
  beforeEach(() => TestBed.configureTestingModule({providers:[provideHttpClient(), provideHttpClientTesting()]}));
  afterEach(() => TestBed.inject(HttpTestingController).verify());
  it('hides unused areas only after a successful server decision and keeps historical areas visible', () => {
    const service = TestBed.inject(CoachingStudentNavigationService);
    service.refresh();
    expect(service.showAssignments()).toBe(true);
    TestBed.inject(HttpTestingController).expectOne('/api/coaching/profiles/me/navigation').flush({data:{showAssignments:false,showSessions:true}});
    expect(service.showAssignments()).toBe(false);
    expect(service.showSessions()).toBe(true);
  });
  it('retains access if the server cannot determine visibility', () => {
    const service = TestBed.inject(CoachingStudentNavigationService);
    service.refresh();
    TestBed.inject(HttpTestingController).expectOne('/api/coaching/profiles/me/navigation').flush({}, {status:503,statusText:'Unavailable'});
    expect(service.showAssignments()).toBe(true);
    expect(service.showSessions()).toBe(true);
  });
  // Paket F #36: the menu waits for the answer, and a second refresh does not abort the request in flight.
  it('is not loaded until the server answers (or fails)', () => {
    const service = TestBed.inject(CoachingStudentNavigationService);
    service.refresh(true);
    expect(service.loaded()).toBe(false);
    TestBed.inject(HttpTestingController).expectOne('/api/coaching/profiles/me/navigation').flush({}, {status:503,statusText:'Unavailable'});
    expect(service.loaded()).toBe(true);
  });
  it('a refresh while a request is in flight shares it instead of aborting it', () => {
    const service = TestBed.inject(CoachingStudentNavigationService);
    const http = TestBed.inject(HttpTestingController);
    service.refresh(true); service.refresh();
    const requests = http.match('/api/coaching/profiles/me/navigation');
    expect(requests.length).toBe(1);
    expect(requests[0].cancelled).toBe(false);
    requests[0].flush({data:{showAssignments:false,showSessions:false}});
    expect(service.showAssignments()).toBe(false);
    service.refresh();
    http.expectOne('/api/coaching/profiles/me/navigation').flush({data:{showAssignments:true,showSessions:false}});
    expect(service.showAssignments()).toBe(true);
  });
});
