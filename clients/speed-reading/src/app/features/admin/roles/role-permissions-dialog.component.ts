import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, Inject, OnInit, inject, signal } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { forkJoin } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { isSystemRoleName } from './roles-list.component';

export interface PermissionDto {
  id: string;
  key: string;
  description: string;
  group: string;
  isSystem: boolean;
  isDeleted: boolean;
}

interface RolePermissionsDto {
  roleId: string;
  roleName: string;
  assignedPermissions: string[];
}

interface PermissionGroup {
  name: string;
  permissions: PermissionDto[];
}

export interface RolePermissionsDialogData {
  id: string;
  name: string;
}

@Component({
  selector: 'app-role-permissions-dialog',
  standalone: true,
  imports: [
    CommonModule,
    MatDialogModule,
    MatButtonModule,
    MatCheckboxModule,
    MatIconModule,
    MatProgressSpinnerModule
  ],
  template: `
    <h2 mat-dialog-title>
      <mat-icon>rule</mat-icon>
      {{ data.name }} izinleri
    </h2>

    <mat-dialog-content>
      <p class="description">Rolün hangi yönetim yetkilerine sahip olduğunu görüntüleyin veya güncelleyin.</p>

      @if (loading()) {
        <div class="loading"><mat-spinner diameter="36"></mat-spinner></div>
      } @else if (errorMessage()) {
        <p class="error-message">{{ errorMessage() }}</p>
      } @else {
        @if (readOnly()) {
          <p class="read-only-message">
            <mat-icon>lock</mat-icon>
            Sistem rolü güvenlik nedeniyle yalnızca görüntülenebilir.
          </p>
        }

        @for (group of groupedPermissions(); track group.name) {
          <section class="permission-group">
            <h3>{{ group.name }}</h3>
            @for (permission of group.permissions; track permission.key) {
              <mat-checkbox
                [checked]="isAssigned(permission.key)"
                [disabled]="readOnly() || saving()"
                (change)="setPermission(permission.key, $event.checked)">
                <span class="permission-key">{{ permission.key }}</span>
                <span class="permission-description">{{ permission.description }}</span>
              </mat-checkbox>
            }
          </section>
        }
      }
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close [disabled]="saving()">Kapat</button>
      <button mat-flat-button color="primary" (click)="save()" [disabled]="readOnly() || loading() || saving() || !!errorMessage()">
        @if (saving()) { Kaydediliyor... } @else { Kaydet }
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    :host { display: block; }
    mat-dialog-content { min-width: min(720px, 86vw); max-height: 68vh; }
    mat-dialog-title { display: flex; align-items: center; gap: 8px; }
    .description, .permission-description { color: #6b7280; }
    .description { margin-top: 0; }
    .loading { min-height: 180px; display: grid; place-items: center; }
    .error-message { color: #b91c1c; padding: 24px 0; }
    .read-only-message { display: flex; align-items: center; gap: 8px; color: #92400e; background: #fffbeb; padding: 10px 12px; border-radius: 6px; }
    .permission-group { border-top: 1px solid #e5e7eb; padding: 14px 0 4px; }
    .permission-group h3 { margin: 0 0 8px; font-size: 15px; font-weight: 600; }
    mat-checkbox { display: block; margin: 8px 0; }
    .permission-key, .permission-description { display: block; }
    .permission-key { font-size: 13px; }
    .permission-description { font-size: 11px; }
    @media (max-width: 600px) { mat-dialog-content { min-width: 0; } }
  `]
})
export class RolePermissionsDialogComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}/v1`;

  readonly permissions = signal<PermissionDto[]>([]);
  readonly assignedPermissions = signal<string[]>([]);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly errorMessage = signal('');

  constructor(
    @Inject(MAT_DIALOG_DATA) public readonly data: RolePermissionsDialogData,
    private readonly dialogRef: MatDialogRef<RolePermissionsDialogComponent, boolean>
  ) { }

  ngOnInit(): void {
    forkJoin({
      permissions: this.http.get<PermissionDto[]>(`${this.apiUrl}/permissions`),
      role: this.http.get<RolePermissionsDto>(`${this.apiUrl}/roles/${this.data.id}/permissions`)
    }).subscribe({
      next: response => {
        this.permissions.set((response.permissions ?? []).filter(permission => !permission.isDeleted));
        this.assignedPermissions.set(response.role?.assignedPermissions ?? []);
        this.loading.set(false);
      },
      error: () => {
        this.errorMessage.set('İzinler yüklenemedi. Lütfen daha sonra tekrar deneyin.');
        this.loading.set(false);
      }
    });
  }

  readOnly(): boolean {
    return isSystemRoleName(this.data.name);
  }

  isAssigned(permission: string): boolean {
    return this.assignedPermissions().includes(permission);
  }

  setPermission(permission: string, checked: boolean): void {
    if (this.readOnly()) return;

    const current = new Set(this.assignedPermissions());
    if (checked) current.add(permission);
    else current.delete(permission);
    this.assignedPermissions.set([...current].sort());
  }

  groupedPermissions(): PermissionGroup[] {
    const groups = new Map<string, PermissionDto[]>();
    for (const permission of this.permissions()) {
      const group = permission.group || 'Diğer';
      const entries = groups.get(group) ?? [];
      entries.push(permission);
      groups.set(group, entries);
    }

    return [...groups.entries()]
      .sort(([left], [right]) => left.localeCompare(right))
      .map(([name, permissions]) => ({
        name,
        permissions: permissions.sort((left, right) => left.key.localeCompare(right.key))
      }));
  }

  save(): void {
    if (this.readOnly() || this.loading() || this.errorMessage()) return;

    this.saving.set(true);
    this.http.put<void>(`${this.apiUrl}/roles/${this.data.id}/permissions`, {
      permissions: this.assignedPermissions()
    }).subscribe({
      next: () => this.dialogRef.close(true),
      error: () => {
        this.errorMessage.set('İzinler kaydedilemedi. SystemAdmin hesabında MFA doğrulaması gerekebilir.');
        this.saving.set(false);
      }
    });
  }
}
