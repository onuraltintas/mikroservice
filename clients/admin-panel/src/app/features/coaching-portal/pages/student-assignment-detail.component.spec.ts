import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { vi } from 'vitest';
import { AuthService, UserProfile } from '../../../core/auth/auth.service';
import { AssignmentDetail, CoachingPortalService } from '../../../core/services/coaching-portal.service';
import { StudentAssignmentDetailComponent } from './student-assignment-detail.component';

describe('StudentAssignmentDetailComponent navigation', () => {
  const profile = signal<UserProfile | null>(null);

  beforeEach(async () => {
    profile.set(null);
    await TestBed.configureTestingModule({
      imports: [StudentAssignmentDetailComponent],
      providers: [
        { provide: AuthService, useValue: { userProfile: profile } },
        { provide: CoachingPortalService, useValue: {} },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => null } } } },
        { provide: Router, useValue: { navigate: vi.fn() } }
      ]
    }).compileComponents();
  });

  it('returns the student assignment list for a student', () => {
    profile.set(user('Student'));
    const component = TestBed.createComponent(StudentAssignmentDetailComponent).componentInstance;

    expect(component.backRoute()).toBe('/coaching-portal/assignments');
  });

  it('returns the teacher assignment list for a teacher', () => {
    profile.set(user('Teacher'));
    const component = TestBed.createComponent(StudentAssignmentDetailComponent).componentInstance;

    expect(component.backRoute()).toBe('/coaching-portal/teacher/assignments');
  });

  it('returns the child list for a parent', () => {
    profile.set(user('Parent'));
    const component = TestBed.createComponent(StudentAssignmentDetailComponent).componentInstance;

    expect(component.backRoute()).toBe('/coaching-portal/children');
  });

  it('shows book instructions for mixed assignments as well as book-only assignments', () => {
    const component = TestBed.createComponent(StudentAssignmentDetailComponent).componentInstance;
    const mixed = { source: 'Mixed' } as AssignmentDetail;

    expect(component.hasBookReference(mixed)).toBe(true);
    expect(component.hasBookReference({ source: 'Digital' } as AssignmentDetail)).toBe(false);
  });

  it('uses the authorized teacher role when the profile has multiple roles', () => {
    profile.set({ ...user('InstitutionAdmin'), roles: ['InstitutionAdmin', 'Teacher'] });
    const component = TestBed.createComponent(StudentAssignmentDetailComponent).componentInstance;

    expect(component.isTeacher()).toBe(true);
    expect(component.isStudent()).toBe(false);
    expect(component.backRoute()).toBe('/coaching-portal/teacher/assignments');
  });
});

function user(role: string): UserProfile {
  return {
    id: 'user-1',
    email: 'user@example.test',
    firstName: 'Test',
    lastName: 'User',
    username: 'user@example.test',
    roles: [role],
    role,
    permissions: []
  };
}
