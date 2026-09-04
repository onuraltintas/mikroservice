import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatDialogModule, MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { environment } from '../../../../environments/environment';
import { ToasterService } from '../../../core/services/toaster.service';

interface Role {
  id: string;
  name: string;
  normalizedName: string;
}

@Component({
  selector: 'app-roles-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatTableModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatMenuModule
  ],
  templateUrl: './roles-list.component.html',
  styleUrls: ['./roles-list.component.scss']
})
export class RolesListComponent implements OnInit {
  private http = inject(HttpClient);
  private dialog = inject(MatDialog);
  private toaster = inject(ToasterService);
  private apiUrl = `${environment.apiUrl}/v1/roles`;

  roles: Role[] = [];
  displayedColumns = ['name', 'normalizedName', 'actions'];

  ngOnInit(): void {
    this.loadRoles();
  }

  loadRoles(): void {
    this.http.get<Role[]>(this.apiUrl).subscribe({
      next: (roles) => {
        this.roles = roles;
      },
      error: (err) => {
        console.error('Error loading roles:', err);
        this.toaster.error('Roller yüklenirken bir hata oluştu');
      }
    });
  }

  isSystemRole(roleName: string): boolean {
    return ['Admin', 'Teacher', 'Student', 'Editor'].includes(roleName);
  }

  async openCreateDialog(): Promise<void> {
    const roleName = await this.toaster.prompt('Yeni rol adını girin:', '', { title: 'Yeni rol oluştur', placeholder: 'Rol adı' });
    if (roleName && roleName.trim()) {
      this.createRole(roleName.trim());
    }
  }

  createRole(name: string): void {
    this.http.post<Role>(this.apiUrl, { name }).subscribe({
      next: () => {
        this.loadRoles();
        this.toaster.success('Rol başarıyla oluşturuldu');
      },
      error: (err) => {
        console.error('Error creating role:', err);
        this.toaster.error('Rol oluşturulurken bir hata oluştu');
      }
    });
  }

  async openEditDialog(role: Role): Promise<void> {
    const newName = await this.toaster.prompt('Yeni rol adını girin:', role.name, { title: 'Rolü düzenle', placeholder: 'Rol adı' });
    if (newName && newName.trim() && newName !== role.name) {
      this.updateRole(role.id, newName.trim());
    }
  }

  updateRole(id: string, name: string): void {
    this.http.put<Role>(`${this.apiUrl}/${id}`, { name }).subscribe({
      next: () => {
        this.loadRoles();
        this.toaster.success('Rol başarıyla güncellendi');
      },
      error: (err) => {
        console.error('Error updating role:', err);
        this.toaster.error('Rol güncellenirken bir hata oluştu');
      }
    });
  }

  async deleteRole(role: Role): Promise<void> {
    if (this.isSystemRole(role.name)) {
      this.toaster.warning('Sistem rolleri silinemez');
      return;
    }

    const confirmed = await this.toaster.confirm(`"${role.name}" rolünü silmek istediğinizden emin misiniz?`);
    if (confirmed) {
      this.http.delete(`${this.apiUrl}/${role.id}`).subscribe({
        next: () => {
          this.loadRoles();
          this.toaster.success('Rol başarıyla silindi');
        },
        error: (err) => {
          console.error('Error deleting role:', err);
          this.toaster.error('Rol silinirken bir hata oluştu');
        }
      });
    }
  }
}
