import { NavigationService } from './navigation.service';
import { AuthService } from './auth.service';
import { Router } from '@angular/router';

describe('student program navigation', () => {
  it('exposes assignments, reviews, and the personalized path', () => {
    const service = new NavigationService({} as Router,
      { currentUserValue: { roles: ['Student'] } } as AuthService);
    const routes = service.getStudentMenuItems().map(item => item.route);
    expect(routes).toContain('/student/assignments');
    expect(routes).toContain('/student/reviews');
    expect(routes).toContain('/student/learning-path');
  });
});
