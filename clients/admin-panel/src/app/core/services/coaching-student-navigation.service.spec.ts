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
});
