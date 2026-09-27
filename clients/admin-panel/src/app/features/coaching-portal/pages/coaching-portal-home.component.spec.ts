import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { AuthService, UserProfile } from '../../../core/auth/auth.service';
import { CoachingPortalService } from '../../../core/services/coaching-portal.service';
import { CoachingPortalHomeComponent } from './coaching-portal-home.component';

describe('CoachingPortalHomeComponent teacher metrics', () => {
  it('shows unique active roster count and full assignment total, not counts from a preview page', () => {
    const profile = signal<UserProfile | null>({
      id: 'teacher-1', email: 'teacher@example.test', firstName: 'Ada', lastName: 'Koç',
      username: 'teacher@example.test', roles: ['Teacher'], role: 'Teacher', permissions: []
    });
    const service = {
      getTeacherAssignments: vi.fn(() => of({
        items: [{ id: 'a1', title: 'Ödev 1', type: 'Digital', dueDate: '2030-01-01', status: 'Active',
          totalStudents: 4, submittedCount: 2, createdAt: '2029-12-01' }],
        pageNumber: 1, pageSize: 5, totalCount: 23, totalPages: 5
      })),
      getTeacherStudents: vi.fn(() => of({
        items: [{ userId: 'student-1' }], pageNumber: 1, pageSize: 1, totalCount: 41, totalPages: 41
      }))
    };
    TestBed.configureTestingModule({
      imports: [CoachingPortalHomeComponent],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: { userProfile: profile } },
        { provide: CoachingPortalService, useValue: service }
      ]
    });
    const fixture = TestBed.createComponent(CoachingPortalHomeComponent);
    fixture.detectChanges();

    expect(service.getTeacherStudents).toHaveBeenCalledWith(1, 1);
    expect(fixture.nativeElement.textContent).toContain('Toplam ödev');
    expect(fixture.nativeElement.textContent).toContain('23');
    expect(fixture.nativeElement.textContent).toContain('41');
    expect(fixture.nativeElement.textContent).not.toContain('Aktif ödevler');
  });
});
