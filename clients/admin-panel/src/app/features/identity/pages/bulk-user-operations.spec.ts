import { TestBed } from '@angular/core/testing';
import { PLATFORM_ID } from '@angular/core';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { AuthService } from '../../../core/auth/auth.service';
import { IdentityService, UserDto } from '../../../core/services/identity.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { BulkUserOperationsComponent } from './bulk-user-operations';

const users: UserDto[] = [
  {
    userId: 'user-1', email: 'ada@example.com', fullName: 'Ada Lovelace', role: 'Editor',
    isActive: true, emailConfirmed: true, roles: ['Editor'], permissions: []
  },
  {
    userId: 'user-2', email: 'alan@example.com', fullName: 'Alan Turing', role: 'Teacher',
    isActive: true, emailConfirmed: true, roles: ['Teacher'], permissions: []
  }
];

describe('BulkUserOperationsComponent', () => {
  const identityService = {
    getRoles: vi.fn(() => of(['Editor', 'Teacher'])),
    getAllUsers: vi.fn(() => of({ items: users, totalCount: users.length, pageNumber: 1, pageSize: 100 })),
    getBulkUserImportTemplate: vi.fn(),
    importUsers: vi.fn(),
    exportUsers: vi.fn(),
    assignBulkRole: vi.fn(() => of({ succeeded: 2, failed: 0, errors: [] }))
  };
  const authService = { hasPermission: vi.fn(() => true) };
  const toaster = {
    confirm: vi.fn(async () => true),
    success: vi.fn(),
    error: vi.fn()
  };

  beforeEach(async () => {
    vi.clearAllMocks();
    await TestBed.configureTestingModule({
      imports: [BulkUserOperationsComponent],
      providers: [
        { provide: IdentityService, useValue: identityService },
        { provide: AuthService, useValue: authService },
        { provide: ToasterService, useValue: toaster },
        { provide: PLATFORM_ID, useValue: 'browser' }
      ]
    }).compileComponents();
  });

  it('loads at most the first 100 users and toggles the visible selection', () => {
    const fixture = TestBed.createComponent(BulkUserOperationsComponent);
    const component = fixture.componentInstance;

    expect(identityService.getAllUsers).toHaveBeenCalledWith(1, 100, '', undefined);
    expect(component.users()).toHaveLength(2);

    component.toggleAll();
    expect(component.selectedUserIds()).toEqual(['user-1', 'user-2']);
    component.toggleAll();
    expect(component.selectedUserIds()).toEqual([]);
  });

  it('rejects non-CSV files and imports only files within the 5 MB limit', () => {
    const component = TestBed.createComponent(BulkUserOperationsComponent).componentInstance;
    const invalidInput = document.createElement('input');
    invalidInput.type = 'file';

    Object.defineProperty(invalidInput, 'files', { value: [new File(['data'], 'users.txt', { type: 'text/plain' })] });
    component.selectFile({ target: invalidInput } as unknown as Event);
    expect(component.selectedFile()).toBeNull();
    expect(component.error()).toContain('CSV');

    const oversizedInput = document.createElement('input');
    oversizedInput.type = 'file';
    Object.defineProperty(oversizedInput, 'files', { value: [new File([new Uint8Array(5 * 1024 * 1024 + 1)], 'users.csv', { type: 'text/csv' })] });
    component.selectFile({ target: oversizedInput } as unknown as Event);
    expect(component.selectedFile()).toBeNull();
  });

  it('keeps the server result after refreshing the visible users following role assignment', async () => {
    const component = TestBed.createComponent(BulkUserOperationsComponent).componentInstance;
    component.selectedUserIds.set(['user-1', 'user-2']);
    component.assignmentRole = 'Editor';

    await component.assignRole();

    expect(identityService.assignBulkRole).toHaveBeenCalledWith(['user-1', 'user-2'], 'Editor', false);
    expect(component.assignmentResult()).toEqual({ succeeded: 2, failed: 0, errors: [] });
    expect(toaster.success).toHaveBeenCalled();
  });
});
