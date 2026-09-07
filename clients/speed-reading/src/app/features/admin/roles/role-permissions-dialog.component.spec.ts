import { TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { RolePermissionsDialogComponent } from './role-permissions-dialog.component';

describe('RolePermissionsDialogComponent', () => {
  let http: HttpTestingController;
  let dialogRef: jasmine.SpyObj<MatDialogRef<RolePermissionsDialogComponent>>;

  beforeEach(() => {
    dialogRef = jasmine.createSpyObj<MatDialogRef<RolePermissionsDialogComponent>>('MatDialogRef', ['close']);

    TestBed.configureTestingModule({
      imports: [RolePermissionsDialogComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: MAT_DIALOG_DATA, useValue: { id: 'role-1', name: 'CustomCoach' } },
        { provide: MatDialogRef, useValue: dialogRef }
      ]
    });

    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads the complete permission catalog and the role assignments', () => {
    const fixture = TestBed.createComponent(RolePermissionsDialogComponent);
    const component = fixture.componentInstance;
    component.ngOnInit();

    http.expectOne('/api/v1/permissions').flush([
      { id: 'permission-1', key: 'Permissions.Users.View', description: 'Users', group: 'Users', isSystem: true, isDeleted: false },
      { id: 'permission-2', key: 'Permissions.Coaching.View', description: 'Coaching', group: 'Coaching', isSystem: true, isDeleted: false }
    ]);
    http.expectOne('/api/v1/roles/role-1/permissions').flush({
      roleId: 'role-1',
      roleName: 'CustomCoach',
      assignedPermissions: ['Permissions.Coaching.View']
    });

    expect(component.permissions().map(permission => permission.key)).toEqual([
      'Permissions.Users.View',
      'Permissions.Coaching.View'
    ]);
    expect(component.isAssigned('Permissions.Coaching.View')).toBeTrue();
    expect(component.isAssigned('Permissions.Users.View')).toBeFalse();
  });

  it('saves the selected permission keys for a custom role', () => {
    const fixture = TestBed.createComponent(RolePermissionsDialogComponent);
    const component = fixture.componentInstance;
    component.ngOnInit();

    http.expectOne('/api/v1/permissions').flush([
      { id: 'permission-1', key: 'Permissions.Users.View', description: 'Users', group: 'Users', isSystem: true, isDeleted: false }
    ]);
    http.expectOne('/api/v1/roles/role-1/permissions').flush({
      roleId: 'role-1',
      roleName: 'CustomCoach',
      assignedPermissions: []
    });

    component.setPermission('Permissions.Users.View', true);
    component.save();

    const request = http.expectOne('/api/v1/roles/role-1/permissions');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ permissions: ['Permissions.Users.View'] });
    request.flush(null);

    expect(dialogRef.close).toHaveBeenCalledWith(true);
  });
});
