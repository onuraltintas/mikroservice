import { TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { AuthService } from '../../../core/auth/auth.service';
import { CoachingInvitationsService } from '../../../core/services/coaching-invitations.service';
import { CoachingInvitationsComponent } from './coaching-invitations.component';

describe('CoachingInvitationsComponent', () => {
  it('lets an institution admin invite an existing teacher', async () => {
    const invitations = {
      getMyInvitations: vi.fn().mockReturnValue(of([])),
      inviteTeacher: vi.fn().mockReturnValue(of({ invitationId: 'invite-1' }))
    };
    const component = createComponent(invitations, ['InstitutionAdmin']);
    component.teacherEmail = 'teacher@example.com';

    await component.inviteTeacher();

    expect(invitations.inviteTeacher).toHaveBeenCalledWith('teacher@example.com');
    expect(component.success()).toContain('gönderildi');
  });

  it('does not claim acceptance when the API rejects a recipient invitation', async () => {
    const invitations = {
      getMyInvitations: vi.fn().mockReturnValue(of([{ id: 'invite-1', type: 'TeacherToInstitution' }])),
      accept: vi.fn().mockReturnValue(throwError(() => new Error('failed')))
    };
    const component = createComponent(invitations, ['Teacher']);
    await component.accept('invite-1');

    expect(component.error()).toContain('kabul edilemedi');
    expect(component.success()).toBeNull();
  });

  function createComponent(invitations: Record<string, unknown>, roles: string[]) {
    TestBed.configureTestingModule({ providers: [
      { provide: CoachingInvitationsService, useValue: invitations },
      { provide: AuthService, useValue: { userProfile: () => ({ roles }) } },
      { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: { get: () => null } } } }
    ] });
    return TestBed.runInInjectionContext(() => new CoachingInvitationsComponent());
  }
});
