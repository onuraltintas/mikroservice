import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTableModule } from '@angular/material/table';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { environment } from '../../../../environments/environment';
import { ToasterService } from '../../../core/services/toaster.service';

interface User {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  roles: string[];
}

interface Role {
  id: string;
  name: string;
}

interface BulkOperationResult {
  succeeded: number;
  failed: number;
  errors: string[];
}

@Component({
  selector: 'app-bulk-operations',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatTabsModule,
    MatTableModule,
    MatCheckboxModule,
    MatSelectModule,
    MatFormFieldModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './bulk-operations.component.html',
  styleUrls: ['./bulk-operations.component.scss']
})
export class BulkOperationsComponent implements OnInit {
  private http = inject(HttpClient);
  private toaster = inject(ToasterService);
  private apiUrl = environment.apiUrl;

  // CSV Import
  selectedFile: File | null = null;
  importing = false;
  importResult: BulkOperationResult | null = null;

  // CSV Export
  exportRole: string | null = null;
  exporting = false;

  // Bulk Role Assignment
  users: Array<User & { selected?: boolean }> = [];
  roles: Role[] = [];
  selectedRoleForAssignment: string = '';
  removeExistingRoles = false;
  loadingUsers = false;
  assigning = false;
  assignmentResult: BulkOperationResult | null = null;
  allSelected = false;
  displayedColumns = ['select', 'email', 'name', 'roles'];

  ngOnInit(): void {
    this.loadRoles();
  }

  loadRoles(): void {
    this.http.get<Role[]>(`${this.apiUrl}/v1/roles`).subscribe({
      next: (roles) => {
        this.roles = roles;
      },
      error: (err) => {
        console.error('Error loading roles:', err);
      }
    });
  }

  downloadTemplate(): void {
    window.open(`${this.apiUrl}/v1/bulkoperations/download-template`, '_blank');
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files && input.files.length > 0) {
      this.selectedFile = input.files[0];
      this.importResult = null;
    }
  }

  importUsers(): void {
    if (!this.selectedFile) return;

    this.importing = true;
    const formData = new FormData();
    formData.append('file', this.selectedFile);

    this.http.post<BulkOperationResult>(`${this.apiUrl}/v1/bulkoperations/import-users`, formData).subscribe({
      next: (result) => {
        this.importing = false;
        this.importResult = result;
        this.selectedFile = null;
        this.toaster.alert(`İçe aktarma tamamlandı. Başarılı: ${result.succeeded}, Başarısız: ${result.failed}`);
      },
      error: (err) => {
        console.error('Error importing users:', err);
        this.importing = false;
        this.toaster.alert('Kullanıcılar içe aktarılırken hata oluştu');
      }
    });
  }

  exportUsers(): void {
    this.exporting = true;
    const params: any = {};
    if (this.exportRole) {
      params.role = this.exportRole;
    }

    this.http.get(`${this.apiUrl}/v1/bulkoperations/export-users`, {
      params,
      responseType: 'blob'
    }).subscribe({
      next: (blob) => {
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `users_export_${new Date().getTime()}.csv`;
        a.click();
        window.URL.revokeObjectURL(url);
        this.exporting = false;
      },
      error: (err) => {
        console.error('Error exporting users:', err);
        this.exporting = false;
        this.toaster.alert('Kullanıcılar dışa aktarılırken hata oluştu');
      }
    });
  }

  loadUsers(): void {
    this.loadingUsers = true;
    this.http.get<any>(`${this.apiUrl}/v1/users`).subscribe({
      next: (response) => {
        this.users = response.items.map((u: User) => ({ ...u, selected: false }));
        this.loadingUsers = false;
        this.assignmentResult = null;
      },
      error: (err) => {
        console.error('Error loading users:', err);
        this.loadingUsers = false;
        this.toaster.alert('Kullanıcılar yüklenirken hata oluştu');
      }
    });
  }

  toggleSelectAll(): void {
    this.allSelected = !this.allSelected;
    this.users.forEach(user => user.selected = this.allSelected);
  }

  onUserSelectionChange(): void {
    this.allSelected = this.users.length > 0 && this.users.every(u => u.selected);
  }

  getSelectedCount(): number {
    return this.users.filter(u => u.selected).length;
  }

  bulkAssignRoles(): void {
    const selectedUserIds = this.users.filter(u => u.selected).map(u => u.id);

    if (selectedUserIds.length === 0 || !this.selectedRoleForAssignment) {
      return;
    }

    this.assigning = true;

    this.http.post<BulkOperationResult>(`${this.apiUrl}/v1/bulkoperations/assign-roles`, {
      userIds: selectedUserIds,
      role: this.selectedRoleForAssignment,
      removeExistingRoles: this.removeExistingRoles
    }).subscribe({
      next: (result) => {
        this.assigning = false;
        this.assignmentResult = result;
        this.toaster.alert(`Rol atama tamamlandı. Başarılı: ${result.succeeded}, Başarısız: ${result.failed}`);
        this.loadUsers(); // Reload to show updated roles
      },
      error: (err) => {
        console.error('Error assigning roles:', err);
        this.assigning = false;
        this.toaster.alert('Roller atanırken hata oluştu');
      }
    });
  }
}
