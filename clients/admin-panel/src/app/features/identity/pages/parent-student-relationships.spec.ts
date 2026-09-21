import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { ADMIN_PERMISSIONS } from '../../../core/auth/permissions';
import { AuthService } from '../../../core/auth/auth.service';
import { IdentityService, ParentStudentRelationshipDto, UserDto } from '../../../core/services/identity.service';
import { ParentStudentRelationshipsComponent } from './parent-student-relationships';

const relationship: ParentStudentRelationshipDto = {
  id: 'relationship-1', parentUserId: 'parent-1', parentName: 'Ada Veli', parentEmail: 'ada@example.test',
  studentUserId: 'student-1', studentName: 'Ali Öğrenci', studentEmail: 'ali@example.test',
  relationship: 'Mother', status: 'Pending', requestedAt: '2026-09-21T12:00:00Z'
};

const user = (id: string, role: string): UserDto => ({
  userId: id, email: `${id}@example.test`, fullName: id, role, isActive: true,
  emailConfirmed: true, roles: [role], permissions: []
});

describe('ParentStudentRelationshipsComponent', () => {
  const identity = {
    getParentStudentRelationships: vi.fn(() => of({ items: [relationship], totalCount: 1, pageNumber: 1, pageSize: 25 })),
    getAllUsers: vi.fn((_page: number, _size: number, _search: string, role: string) =>
      of({ items: [user(role === 'Parent' ? 'parent-1' : 'student-1', role)], totalCount: 1, pageNumber: 1, pageSize: 100 })),
    requestParentStudentRelationship: vi.fn(() => of({ relationshipId: 'relationship-2' })),
    verifyParentStudentRelationship: vi.fn(() => of(void 0)),
    revokeParentStudentRelationship: vi.fn(() => of(void 0))
  };
  const profile = signal({
    id: 'admin-1', email: 'admin@example.test', role: 'SystemAdmin', roles: ['SystemAdmin'],
    permissions: [ADMIN_PERMISSIONS.usersView, ADMIN_PERMISSIONS.usersEdit]
  });

  beforeEach(async () => {
    vi.clearAllMocks();
    await TestBed.configureTestingModule({
      imports: [ParentStudentRelationshipsComponent],
      providers: [
        { provide: IdentityService, useValue: identity },
        { provide: AuthService, useValue: { userProfile: profile.asReadonly() } }
      ]
    }).compileComponents();
  });

  it('loads relationship records and role-scoped user directories', () => {
    const fixture = TestBed.createComponent(ParentStudentRelationshipsComponent);
    fixture.detectChanges();

    expect(identity.getParentStudentRelationships).toHaveBeenCalledWith(1, 25, '', undefined);
    expect(identity.getAllUsers).toHaveBeenCalledWith(1, 100, '', 'Parent', true);
    expect(identity.getAllUsers).toHaveBeenCalledWith(1, 100, '', 'Student', true);
    expect(fixture.componentInstance.items()).toEqual([relationship]);
  });

  it('creates, verifies and revokes through explicit workflows', () => {
    const component = TestBed.createComponent(ParentStudentRelationshipsComponent).componentInstance;
    component.ngOnInit();
    component.parentUserId = 'parent-1';
    component.studentUserId = 'student-1';
    component.relationship = 'Guardian';
    component.create();
    expect(identity.requestParentStudentRelationship).toHaveBeenCalledWith('parent-1', 'student-1', 'Guardian');

    component.verify(relationship);
    expect(identity.verifyParentStudentRelationship).toHaveBeenCalledWith('relationship-1');

    component.openRevoke(relationship);
    component.revocationReason = '  Velayet sona erdi.  ';
    component.revoke();
    expect(identity.revokeParentStudentRelationship).toHaveBeenCalledWith('relationship-1', 'Velayet sona erdi.');
  });
});
