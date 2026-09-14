import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TeachersService } from './teachers.service';

describe('TeachersService', () => {
  let service: TeachersService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [TeachersService, provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(TeachersService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('maps the authenticated teacher student roster to the shared student model', () => {
    service.getMyStudents().subscribe(students => {
      expect(students).toHaveSize(1);
      expect(students[0]).toEqual(jasmine.objectContaining({
        id: 'student-1',
        firstName: 'Ada',
        lastName: 'Yılmaz',
        email: 'ada@example.test',
        dailyGoalMinutes: 30,
        isActive: true
      }));
    });

    const request = http.expectOne(candidate => candidate.url === '/api/v1/teachers/me/students');
    expect(request.request.params.get('pageSize')).toBe('100');
    request.flush({
      items: [{
        userId: 'student-1',
        firstName: 'Ada',
        lastName: 'Yılmaz',
        email: 'ada@example.test',
        institutionId: 'institution-1',
        institutionName: 'Örnek Kolej',
        dailyGoalMinutes: 30,
        isActive: true
      }]
    });
  });
});
