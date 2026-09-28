import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { NEVER, of } from 'rxjs';
import { vi } from 'vitest';
import { AuthService, UserProfile } from '../../../core/auth/auth.service';
import { AssignmentDetail, CoachingPortalService } from '../../../core/services/coaching-portal.service';
import { StudentAssignmentDetailComponent } from './student-assignment-detail.component';
import { CoachingPortalViewService } from '../coaching-portal-view.service';

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

  it('lets a dual-role user submit their own assignment in student view', () => {
    profile.set({ ...user('Teacher'), roles: ['Teacher', 'Student'] });
    const view = TestBed.inject(CoachingPortalViewService);
    view.select('Student');
    const component = TestBed.createComponent(StudentAssignmentDetailComponent).componentInstance;
    expect(component.isStudent()).toBe(true);
    expect(component.isTeacher()).toBe(false);
    expect(component.backRoute()).toBe('/coaching-portal/assignments');
  });
});

describe('StudentAssignmentDetailComponent teacher attachments', () => {
  it('downloads a clean attachment for its assigned student and ignores unscanned attachments', () => {
    const profile = signal<UserProfile | null>(user('Teacher'));
    const attachment = {
      id: 'attachment-1', originalFileName: 'homework.jpg', contentType: 'image/jpeg',
      sizeBytes: 2048, status: 'Clean'
    };
    const service = {
      getAssignment: vi.fn(() => of({
        id: 'assignment-1', assignedStudents: [{ studentId: 'student-1', status: 'Submitted', attachments: [attachment] }]
      } as AssignmentDetail)),
      getTeacherStudents: vi.fn(() => of({
        items: [{ userId: 'student-1', fullName: 'Ada Yılmaz' }],
        pageNumber: 1, pageSize: 1, totalCount: 1, totalPages: 1
      })),
      downloadAttachment: vi.fn(() => NEVER)
    };
    TestBed.configureTestingModule({
      imports: [StudentAssignmentDetailComponent],
      providers: [
        { provide: AuthService, useValue: { userProfile: profile } },
        { provide: CoachingPortalService, useValue: service },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'assignment-1' } } } }
      ]
    });
    const fixture = TestBed.createComponent(StudentAssignmentDetailComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;

    component.downloadTeacherAttachment('student-1', attachment);
    component.downloadTeacherAttachment('student-1', { ...attachment, status: 'Pending' });

    expect(service.downloadAttachment).toHaveBeenCalledTimes(1);
    expect(service.downloadAttachment).toHaveBeenCalledWith('assignment-1', 'student-1', 'attachment-1');
  });
});

describe('StudentAssignmentDetailComponent cancelled assignment', () => {
  it('does not allow a student to submit or upload work for a cancelled assignment', () => {
    const profile = signal<UserProfile | null>(user('Student'));
    const service = {
      submitAssignment: vi.fn(),
      createAttachment: vi.fn()
    };
    TestBed.configureTestingModule({
      imports: [StudentAssignmentDetailComponent],
      providers: [
        { provide: AuthService, useValue: { userProfile: profile } },
        { provide: CoachingPortalService, useValue: service },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'assignment-1' } } } }
      ]
    });
    const component = TestBed.createComponent(StudentAssignmentDetailComponent).componentInstance;
    component.assignment.set({
      id: 'assignment-1',
      status: 'Cancelled',
      assignedStudents: [{ studentId: 'user-1', status: 'Assigned' }]
    } as AssignmentDetail);

    component.submit();

    expect(component.canSubmit()).toBe(false);
    expect(service.submitAssignment).not.toHaveBeenCalled();
    expect(service.createAttachment).not.toHaveBeenCalled();
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
