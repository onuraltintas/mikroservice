import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { CoachingPlanPreferencesService } from './coaching-plan-preferences.service';

// H4 contract item 6: the exam focus and tempo of the plan wizard.
describe('plan preferences API', () => {
  function setup() {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    return { service: TestBed.inject(CoachingPlanPreferencesService), http: TestBed.inject(HttpTestingController) };
  }

  it('reads the student\'s own and, for a teacher, the student\'s; a failed read means "none"', () => {
    const { service, http } = setup();
    let value: unknown;
    service.get().subscribe(x => value = x);
    http.expectOne(r => r.method === 'GET' && r.url === '/api/coaching/study-planning/plan-preferences')
      .flush({ success: true, data: { examFocus: 'Tyt', tempo: 'Balanced', version: 2, updatedAt: null } });
    expect(value).toEqual({ examFocus: 'Tyt', tempo: 'Balanced', version: 2, updatedAt: null });
    service.get('student-1').subscribe(x => value = x);
    http.expectOne('/api/coaching/staff/students/student-1/study/plan-preferences').flush({}, { status: 500, statusText: 'Error' });
    expect(value).toBeNull();
    http.verify();
  });

  it('sends both values with PUT, since PUT replaces both', () => {
    const { service, http } = setup();
    service.save({ examFocus: null, tempo: 'Intense' }).subscribe();
    const put = http.expectOne(r => r.method === 'PUT' && r.url === '/api/coaching/study-planning/plan-preferences');
    expect(put.request.body).toEqual({ examFocus: null, tempo: 'Intense' });
    put.flush({ success: true, data: { examFocus: null, tempo: 'Intense', version: 1, updatedAt: null } });
    http.verify();
  });
});
