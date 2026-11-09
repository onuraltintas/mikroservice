import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { CoachingStudyRoutineService, StudyRoutineAnswers } from './coaching-study-routine.service';

// Paket H4: the H3 routine API (student read and save, teacher read).
describe('study routine API', () => {
  const answers = { schoolShift: 'NoSchool', weekdayMinutes: 240 } as StudyRoutineAnswers;
  function setup() {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    return { service: TestBed.inject(CoachingStudyRoutineService), http: TestBed.inject(HttpTestingController) };
  }

  it('reads the student\'s routine and treats 404 as none', () => {
    const { service, http } = setup();
    let value: unknown = 'unset';
    service.get().subscribe(x => value = x);
    http.expectOne(r => r.method === 'GET' && r.url === '/api/coaching/study-planning/routine')
      .flush({ success: false, code: 'StudyPlanning.RoutineMissing' }, { status: 404, statusText: 'Not Found' });
    expect(value).toBeNull();
    let failed = false;
    service.get().subscribe({ error: () => failed = true });
    http.expectOne('/api/coaching/study-planning/routine').flush({}, { status: 500, statusText: 'Error' });
    expect(failed).toBe(true);
    http.verify();
  });

  it('reads a student\'s routine for a teacher and saves only the student\'s own', () => {
    const { service, http } = setup();
    let value: unknown;
    service.get('student-1').subscribe(x => value = x);
    http.expectOne('/api/coaching/staff/students/student-1/study/routine').flush({ success: true, data: null });
    expect(value).toBeNull();
    service.save({ expectedVersion: 2, expectedAvailabilityVersion: 5, answers }).subscribe(x => value = x);
    const put = http.expectOne(r => r.method === 'PUT' && r.url === '/api/coaching/study-planning/routine');
    expect(put.request.body).toEqual({ expectedVersion: 2, expectedAvailabilityVersion: 5, answers });
    put.flush({ success: true, data: { version: 3 } });
    expect(value).toEqual({ version: 3 });
    http.verify();
  });
});
