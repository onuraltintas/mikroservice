import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CoachingPortalService } from './coaching-portal.service';

describe('Student goal category API contract', () => {
  it('sends the same named category as the teacher portal', () => {
    TestBed.configureTestingModule({ providers: [CoachingPortalService, provideHttpClient(), provideHttpClientTesting()] });
    const http = TestBed.inject(HttpTestingController);
    const service = TestBed.inject(CoachingPortalService);
    for (const [category, name] of [[1, 'ExamPreparation'], [2, 'SubjectMastery'], [3, 'GradeImprovement'], [4, 'StudyHabits'], [5, 'TimeManagement'], [99, 'Other']] as const) {
      service.createStudentGoal('student', { title: 'LGS school target', category }, 'goal-category-test-key').subscribe();
      const request = http.expectOne(r => r.url.endsWith('/goals'));
      expect(request.request.body.category).toBe(name);
      request.flush({ goalId: 'goal' });
    }
    http.verify();
  });
});
