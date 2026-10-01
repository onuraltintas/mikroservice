import { NavigationService } from './navigation.service';
import { AuthService } from './auth.service';
import { Router } from '@angular/router';

describe('student program navigation', () => {
  it('exposes assignments, reviews, and the personalized path', () => {
    const service = new NavigationService({} as Router,
      { currentUserValue: { roles: ['Student'] }, hasRole: (role: string) => role === 'Student' } as unknown as AuthService);
    const routes = service.getStudentMenuItems().map(item => item.route);
    expect(routes).toContain('/student/assignments');
    expect(routes).toContain('/student/reviews');
    expect(routes).toContain('/student/learning-path');
  });
  it('shows staff training separately without personal student reports', () => {
    const service = new NavigationService({} as Router,
      { currentUserValue: { roles: ['Teacher'] }, hasRole: (role: string) => role === 'Teacher' } as unknown as AuthService);
    const routes = service.getStudentMenuItems().map(item => item.route);
    expect(routes).toContain('/student/training-programs');
    expect(routes).not.toContain('/student/reports');
  });
});
