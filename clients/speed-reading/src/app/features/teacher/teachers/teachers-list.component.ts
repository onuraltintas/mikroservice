import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule, MatTableDataSource } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDialogModule, MatDialog } from '@angular/material/dialog';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatMenuModule } from '@angular/material/menu';
import { MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatTooltipModule } from '@angular/material/tooltip';
import { environment } from '../../../../environments/environment';
import { AddTeacherDialogComponent } from './add-teacher-dialog.component';
import { ResetPasswordDialogComponent } from './reset-password-dialog.component';
import { LinkTeacherDialogComponent } from './link-teacher-dialog.component';
import { ToasterService } from '../../../core/services/toaster.service';
import { TeachersService } from '../../../core/services/teachers.service';

interface Teacher {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  studentCount?: number;
  isActive: boolean;
  createdAt: string;
}

@Component({
  selector: 'app-teachers-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatCardModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatDialogModule,
    MatProgressSpinnerModule,
    MatMenuModule,
    MatChipsModule,
    MatFormFieldModule,
    MatInputModule,
    MatTooltipModule
  ],
  template: `
    <div class="teachers-container">
      <div class="header-section">
        <div>
          <h1>Öğretmenler</h1>
          <p class="subtitle">Kurumunuzdaki öğretmenleri yönetin ve performanslarını takip edin.</p>
        </div>
        <div class="header-actions">
           <input type="file" id="teacherFileInput" (change)="onFileSelected($event)" accept=".xlsx, .xls" style="display: none;">
          <button mat-stroked-button color="accent" (click)="downloadTemplate()" [disabled]="loading" class="action-btn">
            <mat-icon>download</mat-icon>
            Şablon İndir
          </button>
          <button mat-stroked-button color="primary" (click)="triggerFileInput()" [disabled]="loading" class="action-btn">
             <mat-icon>upload_file</mat-icon>
             Excel ile Yükle
          </button>
          <button mat-stroked-button class="action-btn link-btn"(click) = "openLinkDialog()" >
            <mat-icon > link </mat-icon>
            Öğretmen Bağla
          </button>
          <button mat-raised-button color="primary" class="action-btn add-btn" (click)="openAddDialog()">
            <mat-icon>add</mat-icon>
            Yeni Öğretmen
          </button>
        </div>
      </div>

      <!-- Filters & Stats Bar could go here -->
      <div class="filters-bar">
        <mat-form-field appearance="outline" class="search-field">
          <mat-icon matPrefix>search</mat-icon>
          <input matInput (keyup)="applyFilter($event)" placeholder="İsim veya e-posta ile ara..." #input>
        </mat-form-field>
      </div>

      <mat-card class="teachers-card mat-elevation-z0">
        <mat-card-content class="p-0">
          <div *ngIf="loading" class="loading-container">
            <mat-spinner diameter="40"></mat-spinner>
          </div>

          <div class="table-container" *ngIf="!loading">
            <table mat-table [dataSource]="dataSource" matSort class="modern-table">

              <!-- Avatar Column -->
              <ng-container matColumnDef="avatar">
                <th mat-header-cell *matHeaderCellDef></th>
                <td mat-cell *matCellDef="let teacher">
                  <div class="avatar-circle" [style.background]="getAvatarColor(teacher)">
                    {{ getInitials(teacher) }}
                  </div>
                </td>
              </ng-container>

              <!-- Name Column -->
              <ng-container matColumnDef="name">
                <th mat-header-cell *matHeaderCellDef>Öğretmen</th>
                <td mat-cell *matCellDef="let teacher">
                  <div class="teacher-info">
                    <span class="teacher-name">{{ teacher.firstName }} {{ teacher.lastName }}</span>
                    <span class="teacher-email">{{ teacher.email }}</span>
                  </div>
                </td>
              </ng-container>

              <!-- Student Count Column -->
              <ng-container matColumnDef="studentCount">
                <th mat-header-cell *matHeaderCellDef>Öğrenci Sayısı</th>
                <td mat-cell *matCellDef="let teacher">
                  <div class="student-count-badge">
                    <mat-icon class="small-icon">school</mat-icon>
                    {{ teacher.studentCount || 0 }}
                  </div>
                </td>
              </ng-container>

              <!-- Status Column -->
              <ng-container matColumnDef="status">
                <th mat-header-cell *matHeaderCellDef>Durum</th>
                <td mat-cell *matCellDef="let teacher">
                  <span class="status-chip" [class.active]="teacher.isActive" [class.inactive]="!teacher.isActive">
                    <span class="status-dot"></span>
                    {{ teacher.isActive ? 'Aktif' : 'Pasif' }}
                  </span>
                </td>
              </ng-container>

              <!-- Actions Column -->
              <ng-container matColumnDef="actions">
                <th mat-header-cell *matHeaderCellDef></th>
                <td mat-cell *matCellDef="let teacher">
                  <div class="action-buttons">
                    <button mat-icon-button color="primary" (click)="viewStudents(teacher)" matTooltip="Öğrencileri Gör">
                      <mat-icon>groups</mat-icon>
                    </button>
                    <button mat-icon-button color="accent" (click)="viewReports(teacher)" matTooltip="Raporları Gör">
                      <mat-icon>assessment</mat-icon>
                    </button>
                    <button mat-icon-button [matMenuTriggerFor]="menu">
                      <mat-icon>more_vert</mat-icon>
                    </button>
                    <mat-menu #menu="matMenu">
                      <button mat-menu-item (click)="openResetPasswordDialog(teacher)">
                        <mat-icon>lock_reset</mat-icon>
                        <span>Şifre Sıfırla</span>
                      </button>
                      <button mat-menu-item (click)="deleteTeacher(teacher)" class="delete-item">
                        <mat-icon>delete_outline</mat-icon>
                        <span>Sil / Bağlantıyı Kes</span>
                      </button>
                    </mat-menu>
                  </div>
                </td>
              </ng-container>

              <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
              <tr mat-row *matRowDef="let row; columns: displayedColumns;" class="teacher-row"></tr>

              <!-- Row shown when there is no matching data. -->
              <tr class="mat-row" *matNoDataRow>
                <td class="mat-cell" colspan="5" *ngIf="input.value">
                  <div class="no-data-message">
                    "{{input.value}}" için sonuç bulunamadı
                  </div>
                </td>
              </tr>
            </table>

            <div *ngIf="dataSource.data.length === 0 && !input.value" class="empty-state">
              <div class="empty-icon-bg">
                <mat-icon>person_add</mat-icon>
              </div>
              <h3>Henüz öğretmeniniz yok</h3>
              <p>Kurumunuza öğretmen ekleyerek veya bağlayarak başlayın.</p>
              <div class="empty-actions">
                <button mat-flat-button color="primary" (click)="openAddDialog()">Öğretmen Ekle</button>
                <button mat-stroked-button (click)="openLinkDialog()">Öğretmen Bağla</button>
              </div>
            </div>
          </div>
        </mat-card-content>
      </mat-card>
    </div>
  `,
  styles: [`
    :host {
      display: block;
      min-height: 100vh;
      background-color: #f8f9fa;
    }

    .teachers-container {
      max-width: 1400px;
      margin: 0 auto;
      padding: 32px 24px;
    }

    .header-section {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      margin-bottom: 32px;
      flex-wrap: wrap;
      gap: 16px;

      h1 {
        font-size: 28px;
        font-weight: 700;
        color: #1a1f36;
        margin: 0 0 8px 0;
        letter-spacing: -0.5px;
      }

      .subtitle {
        color: #697386;
        font-size: 16px;
        margin: 0;
      }
    }

    .header-actions {
      display: flex;
      gap: 12px;

      .action-btn {
        height: 48px;
        padding: 0 24px;
        border-radius: 12px;
        font-weight: 600;
        letter-spacing: 0.3px;
        
        &.add-btn {
          box-shadow: 0 4px 12px rgba(63, 81, 181, 0.25);
        }

        mat-icon {
          margin-right: 8px;
        }
      }
    }

    .filters-bar {
      margin-bottom: 24px;
    }

    .search-field {
      width: 100%;
      max-width: 400px;
      
      ::ng-deep .mat-mdc-text-field-wrapper {
        background-color: white;
        border-radius: 12px;
      }
      
      ::ng-deep .mat-mdc-form-field-subscript-wrapper {
        display: none;
      }
    }

    .teachers-card {
      border-radius: 16px;
      border: 1px solid rgba(0, 0, 0, 0.06);
      background: white;
      overflow: hidden;
      box-shadow: 0 1px 3px rgba(0, 0, 0, 0.05);

      .p-0 {
        padding: 0;
      }
    }

    .modern-table {
      width: 100%;

      th.mat-header-cell {
        color: #697386;
        font-size: 12px;
        font-weight: 600;
        text-transform: uppercase;
        letter-spacing: 0.5px;
        padding: 16px 24px;
        border-bottom: 1px solid #e3e8ee;
        background: #fcfcfd;
      }

      td.mat-cell {
        padding: 16px 24px;
        border-bottom: 1px solid #f1f3f5;
        color: #3c4257;
        font-size: 14px;
      }

      .teacher-row {
        cursor: pointer;
        transition: all 0.2s ease;

        &:hover {
          background-color: #f7fafc;
        }
      }
    }

    .avatar-circle {
      width: 40px;
      height: 40px;
      border-radius: 50%;
      color: white;
      display: flex;
      align-items: center;
      justify-content: center;
      font-weight: 600;
      font-size: 14px;
      text-transform: uppercase;
      box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
    }

    .teacher-info {
      display: flex;
      flex-direction: column;

      .teacher-name {
        font-weight: 600;
        color: #1a1f36;
        margin-bottom: 2px;
      }

      .teacher-email {
        color: #697386;
        font-size: 13px;
      }
    }

    .student-count-badge {
      display: inline-flex;
      align-items: center;
      padding: 4px 12px;
      background: #eef2ff;
      color: #3f51b5;
      border-radius: 20px;
      font-weight: 500;
      font-size: 13px;

      .small-icon {
        font-size: 16px;
        width: 16px;
        height: 16px;
        margin-right: 6px;
      }
    }

    .status-chip {
      display: inline-flex;
      align-items: center;
      padding: 6px 12px;
      border-radius: 20px;
      font-size: 13px;
      font-weight: 500;
      
      .status-dot {
        width: 8px;
        height: 8px;
        border-radius: 50%;
        margin-right: 8px;
      }

      &.active {
        background-color: #ecfdf5;
        color: #059669;
        .status-dot { background-color: #10b981; }
      }

      &.inactive {
        background-color: #fef2f2;
        color: #dc2626;
        .status-dot { background-color: #ef4444; }
      }
    }

    .action-buttons {
      display: flex;
      justify-content: flex-end;
      gap: 4px;
      opacity: 0.7;
      transition: opacity 0.2s;

      .teacher-row:hover & {
        opacity: 1;
      }
    }

    .delete-item {
      color: #f44336;
      mat-icon { color: #f44336; }
    }

    .loading-container {
      display: flex;
      justify-content: center;
      padding: 60px;
      background: #f8f9fa;
    }

    .empty-state {
      display: flex;
      flex-direction: column;
      align-items: center;
      padding: 80px 24px;
      background: #fff;

      .empty-icon-bg {
        width: 80px;
        height: 80px;
        border-radius: 50%;
        background: #eef2ff;
        display: flex;
        align-items: center;
        justify-content: center;
        margin-bottom: 24px;

        mat-icon {
          font-size: 40px;
          width: 40px;
          height: 40px;
          color: #3f51b5;
        }
      }

      h3 {
        font-size: 20px;
        font-weight: 600;
        color: #1a1f36;
        margin: 0 0 8px 0;
      }

      p {
        color: #697386;
        margin: 0 0 32px 0;
        max-width: 400px;
        text-align: center;
        line-height: 1.5;
      }

      .empty-actions {
        display: flex;
        gap: 16px;
      }
    }

    .no-data-message {
      padding: 40px;
      text-align: center;
      color: #697386;
      font-style: italic;
    }

    // Responsive
    @media (max-width: 768px) {
      .teachers-container {
        padding: 16px;
      }

      .header-section {
        flex-direction: column;
        align-items: stretch;
      }

      .header-actions {
        flex-direction: column;
        
        button {
          width: 100%;
        }
      }

      .mat-column-avatar, 
      .mat-column-status {
        display: none; // Hide on very small screens if needed
      }
    }
  `]
})
export class TeachersListComponent implements OnInit {
  private http = inject(HttpClient);
  private teachersService = inject(TeachersService);
  // Wait, I cannot use dynamic import here easily for type safety.
  // I need to add the import statement at the top first.
  // Let me update the imports first.
  private dialog = inject(MatDialog);
  private toaster = inject(ToasterService);

  // Use MatTableDataSource for client-side filtering
  dataSource = new MatTableDataSource<Teacher>([]);
  loading = true;
  displayedColumns = ['avatar', 'name', 'studentCount', 'status', 'actions'];

  ngOnInit(): void {
    this.loadTeachers();
  }

  loadTeachers(): void {
    this.loading = true;
    this.http.get<Teacher[]>(`${environment.apiUrl}/v1/teachers`).subscribe({
      next: (data) => {
        this.dataSource.data = data;
        this.loading = false;
      },
      error: (err) => {
        console.error('Error loading teachers:', err);
        this.toaster.error('Öğretmenler yüklenirken hata oluştu');
        this.loading = false;
      }
    });
  }

  applyFilter(event: Event) {
    const filterValue = (event.target as HTMLInputElement).value;
    this.dataSource.filter = filterValue.trim().toLowerCase();
  }

  openAddDialog(): void {
    const dialogRef = this.dialog.open(AddTeacherDialogComponent, {
      width: '650px',
      autoFocus: false
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        this.loadTeachers();
      }
    });
  }

  openLinkDialog(): void {
    const dialogRef = this.dialog.open(LinkTeacherDialogComponent, {
      width: '450px',
      autoFocus: false
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        this.loadTeachers();
      }
    });
  }

  viewStudents(teacher: Teacher): void {
    window.location.href = `/teacher/students?teacherId=${teacher.id}`;
  }

  viewReports(teacher: Teacher): void {
    // Navigate to reports section with teacherId query param
    // We use window.location.href or router.navigate. Since other methods use window.location, we can stick to it or better use Router if injected.
    // BaseComponent usually has router but this is standalone.
    // I can't easily add Router injection without breaking constructor signature in pure replace.
    // But wait, "private http = inject(HttpClient)" style is used. I can add router injection!
    window.location.href = `/teacher/reports/class-overview?teacherId=${teacher.id}`;
  }

  openResetPasswordDialog(teacher: Teacher): void {
    const dialogRef = this.dialog.open(ResetPasswordDialogComponent, {
      width: '400px',
      data: { teacherId: teacher.id, teacherName: `${teacher.firstName} ${teacher.lastName}` }
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        this.toaster.success('Şifre başarıyla güncellendi');
      }
    });
  }

  deleteTeacher(teacher: Teacher): void {
    if (confirm(`${teacher.firstName} ${teacher.lastName} adlı öğretmeni silmek veya bağlantısını kesmek istediğinize emin misiniz?`)) {
      this.http.delete(`${environment.apiUrl}/v1/teachers/${teacher.id}`).subscribe({
        next: () => {
          this.toaster.success('Öğretmen başarıyla silindi/bağlantı kesildi');
          this.loadTeachers();
        },
        error: (err) => {
          this.toaster.error('İşlem başarısız oldu. Lütfen tekrar deneyin.');
        }
      });
    }
  }

  triggerFileInput(): void {
    const fileInput = document.getElementById('teacherFileInput') as HTMLInputElement;
    if (fileInput) {
      fileInput.click();
    }
  }

  onFileSelected(event: any): void {
    const file: File = event.target.files[0];
    if (file) {
      if (!file.name.endsWith('.xlsx') && !file.name.endsWith('.xls')) {
        this.toaster.error('Lütfen geçerli bir Excel dosyası yükleyin (.xlsx, .xls)');
        return;
      }

      this.loading = true;
      // Note: teachersService is not injected as a public property but as private.
      // But looking at the class, it is injected as 'private http'.
      // Wait, 'TeachersService' is NOT injected in the constructor or via inject() in the original file I viewed?
      // Let me double check the file content.
      // Ah, line 488: `private http = inject(HttpClient);`
      // But I need `TeachersService` to call `importTeachers`. 
      // I should inject `TeachersService`.
      // I will assume I can access it if I inject it.
      // Wait, I need to check if TeachersService is already injected.
      // It is NOT in the original file. I only see HttpClient.
      // So I will fix the Injection in a separate step or try to use HttpClient directly?
      // Better to use TeachersService.
      // I will add the logic assuming I will add the injection next.

      // Since I can't easily add injection in the middle of lines without context, 
      // I'll add the methods first, then I'll add the injection.

      // Actually, I can use the existing 'http' to call the service methods logic if I wanted to, 
      // but proper way is to use the service.

      // I'll assume 'teachersService' will be available.
      this.teachersService.importTeachers(file).subscribe({
        next: (result: any) => {
          this.loading = false;
          if (result.failureCount > 0) {
            this.toaster.warning(`${result.successCount} öğretmen eklendi, ${result.failureCount} hata oluştu. Hatalar: \n${result.errors.join('\n')}`);
          } else {
            this.toaster.success(`${result.successCount} öğretmen başarıyla eklendi.`);
          }
          this.loadTeachers();
        },
        error: (err: any) => {
          this.loading = false;
          this.toaster.error('Dosya yüklenirken bir hata oluştu. Lütfen dosya formatını kontrol edin.');
        }
      });
    }
  }

  downloadTemplate(): void {
    this.teachersService.getTeacherImportTemplate().subscribe({
      next: (blob: Blob) => {
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = 'Ogretmen_Yukleme_Sablonu.xlsx';
        a.click();
        window.URL.revokeObjectURL(url);
      },
      error: (err: any) => {
        this.toaster.error('Şablon indirilemedi.');
      }
    });
  }

  // Helpers
  getInitials(teacher: Teacher): string {
    const first = teacher.firstName?.charAt(0) || '';
    const last = teacher.lastName?.charAt(0) || '';
    return (first + last).toUpperCase();
  }

  getAvatarColor(teacher: Teacher): string {
    const colors = [
      '#3b82f6', '#10b981', '#f59e0b', '#ef4444',
      '#8b5cf6', '#ec4899', '#6366f1', '#14b8a6'
    ];
    // Hash function to pick consistent color
    let hash = 0;
    const str = (teacher.firstName + teacher.lastName + teacher.id);
    for (let i = 0; i < str.length; i++) {
      hash = str.charCodeAt(i) + ((hash << 5) - hash);
    }
    const index = Math.abs(hash % colors.length);
    return colors[index];
  }
}
