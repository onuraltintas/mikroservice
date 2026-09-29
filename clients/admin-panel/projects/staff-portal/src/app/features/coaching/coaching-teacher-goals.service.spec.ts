import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { CoachingTeacherGoalsService } from './coaching-teacher-goals.service';

describe('CoachingTeacherGoalsService', () => {
  let http: HttpTestingController;
  let service: CoachingTeacherGoalsService;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    service = TestBed.inject(CoachingTeacherGoalsService);
  });

  afterEach(() => http.verify());

  it('loads a bounded teacher-scoped page of goals', () => {
    service.getTeacherGoals('teacher-1', 2, 10).subscribe();

    const request = http.expectOne('/api/goals/teacher/teacher-1?pageNumber=2&pageSize=10');
    expect(request.request.method).toBe('GET');
    request.flush({ items: [], pageNumber: 2, pageSize: 10, totalCount: 0, totalPages: 0 });
  });

  it('creates a goal with a server idempotency key and normalized fields', () => {
    service.createGoal({
      teacherId: 'teacher-1', studentId: 'student-1', title: '  LGS hazırlığı  ',
      category: 1, description: '  Haftalık tekrar  ', targetDate: '2030-01-05T10:00:00Z', targetScore: 85
    }, 'goal-idempotency-key').subscribe();

    const request = http.expectOne('/api/goals');
    expect(request.request.method).toBe('POST');
    expect(request.request.headers.get('Idempotency-Key')).toBe('goal-idempotency-key');
    expect(request.request.body).toMatchObject({
      teacherId: 'teacher-1', studentId: 'student-1', title: 'LGS hazırlığı',
      category: 1, description: 'Haftalık tekrar', targetScore: 85
    });
    request.flush({ goalId: 'goal-1' });
  });

  it('updates supported goal fields without changing the linked student', () => {
    service.updateGoal('goal-1', {
      goalId: 'goal-1', title: '  Yeni hedef  ', category: 2,
      description: '  Açıklama  ', targetDate: null, targetScore: 90,
      targetExamType: 4, targetSubject: '  Matematik  '
    }).subscribe();

    const request = http.expectOne('/api/goals/goal-1');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toMatchObject({
      goalId: 'goal-1', title: 'Yeni hedef', category: 2,
      description: 'Açıklama', targetScore: 90, targetExamType: 4, targetSubject: 'Matematik'
    });
    expect(request.request.body['studentId']).toBeUndefined();
    request.flush({ goalId: 'goal-1', title: 'Yeni hedef' });
  });
});
