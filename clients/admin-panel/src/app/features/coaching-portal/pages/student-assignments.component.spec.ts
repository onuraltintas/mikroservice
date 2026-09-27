import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { of } from 'rxjs';
import { AuthService, UserProfile } from '../../../core/auth/auth.service';
import { CoachingPortalService } from '../../../core/services/coaching-portal.service';
import { StudentAssignmentsComponent } from './student-assignments.component';

describe('StudentAssignmentsComponent cancelled assignments', () => {
  it('separates cancelled work from pending and overdue filters', () => {
    TestBed.configureTestingModule({
      imports: [StudentAssignmentsComponent],
      providers: [
        { provide: AuthService, useValue: { userProfile: signal<UserProfile | null>({
          id: 'student-1', email: 'student@example.test', firstName: 'Ada', lastName: 'Öğrenci',
          username: 'student@example.test', roles: ['Student'], role: 'Student', permissions: []
        }) } },
        { provide: CoachingPortalService, useValue: { getStudentAssignments: () => of({ items: [], pageNumber: 1, pageSize: 25, totalCount: 0, totalPages: 0 }) } },
        { provide: ActivatedRoute, useValue: {} }
      ]
    });
    const component = TestBed.createComponent(StudentAssignmentsComponent).componentInstance;
    component.assignments.set([
      { id: 'pending', title: 'Bekleyen', dueDate: '', status: 'Assigned', isOverdue: false },
      { id: 'overdue', title: 'Geciken', dueDate: '', status: 'Assigned', isOverdue: true },
      { id: 'cancelled', title: 'İptal', dueDate: '', status: 'Assigned', assignmentStatus: 'Cancelled', isOverdue: true }
    ]);

    component.setFilter('pending');
    expect(component.visibleAssignments().map(assignment => assignment.id)).toEqual(['pending']);
    component.setFilter('overdue');
    expect(component.visibleAssignments().map(assignment => assignment.id)).toEqual(['overdue']);
    component.setFilter('cancelled');
    expect(component.visibleAssignments().map(assignment => assignment.id)).toEqual(['cancelled']);
    expect(component.assignmentStatusLabel(component.assignments()[2])).toBe('İptal edildi');
  });
});
