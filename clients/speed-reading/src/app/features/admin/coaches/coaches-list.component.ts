import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatChipsModule } from '@angular/material/chips';
import { takeUntil, finalize } from 'rxjs/operators';
import { UsersService } from '../../../core/services/users.service';
import { UserDto } from '../../../core/models/user.model';
import { AuthService } from '../../../core/services/auth.service';
import { BaseComponent } from '../../../core/components/base.component';
import { strongPasswordValidator, PASSWORD_ERROR_MESSAGES } from '../../../shared/validators/password.validator';

// ─── Inline dialog ────────────────────────────────────────────────────────────
@Component({
  selector: 'app-coach-dialog',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatDialogModule, MatFormFieldModule,
    MatInputModule, MatButtonModule, MatIconModule, MatProgressSpinnerModule],
  template: `
    <h2 mat-dialog-title>Yeni Koç Ekle</h2>
    <mat-dialog-content>
      <form [formGroup]="form" style="display:flex;flex-direction:column;gap:12px;min-width:360px;padding-top:8px">
        <div style="display:grid;grid-template-columns:1fr 1fr;gap:12px">
          <mat-form-field>
            <mat-label>Ad</mat-label>
            <input matInput formControlName="firstName" />
            <mat-error *ngIf="form.get('firstName')?.hasError('required')">Zorunlu alan</mat-error>
          </mat-form-field>
          <mat-form-field>
            <mat-label>Soyad</mat-label>
            <input matInput formControlName="lastName" />
            <mat-error *ngIf="form.get('lastName')?.hasError('required')">Zorunlu alan</mat-error>
          </mat-form-field>
        </div>
        <mat-form-field>
          <mat-label>E-posta</mat-label>
          <input matInput formControlName="email" type="email" />
          <mat-error *ngIf="form.get('email')?.hasError('required')">Zorunlu alan</mat-error>
          <mat-error *ngIf="form.get('email')?.hasError('email')">Geçerli e-posta girin</mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Şifre</mat-label>
          <input matInput formControlName="password" [type]="hidePassword ? 'password' : 'text'" />
          <button mat-icon-button matSuffix type="button" (click)="hidePassword = !hidePassword">
            <mat-icon>{{ hidePassword ? 'visibility_off' : 'visibility' }}</mat-icon>
          </button>
          <mat-error *ngIf="form.get('password')?.errors">Geçerli bir şifre girin (min 8 karakter, büyük/küçük harf, rakam)</mat-error>
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>İptal</button>
      <button mat-flat-button color="primary" (click)="save()" [disabled]="saving">
        <mat-spinner *ngIf="saving" diameter="20" style="display:inline-block;margin-right:8px"></mat-spinner>
        Kaydet
      </button>
    </mat-dialog-actions>
  `
})
export class CoachDialogComponent extends BaseComponent {
  private fb = inject(FormBuilder);
  private authService = inject(AuthService);
  dialogRef = inject(MatDialogRef<CoachDialogComponent>);

  hidePassword = true;
  saving = false;

  form: FormGroup = this.fb.group({
    firstName: ['', Validators.required],
    lastName: ['', Validators.required],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, strongPasswordValidator()]]
  });

  save(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.saving = true;
    this.authService.registerCoach(this.form.value)
      .pipe(takeUntil(this.destroy$), finalize(() => this.saving = false))
      .subscribe({
        next: () => { this.toaster.success('Koç başarıyla eklendi'); this.dialogRef.close(true); },
        error: () => this.toaster.error('Koç eklenirken bir hata oluştu')
      });
  }
}

// ─── List component ───────────────────────────────────────────────────────────
@Component({
  selector: 'app-coaches-list',
  standalone: true,
  imports: [
    CommonModule, RouterLink, MatTableModule, MatButtonModule, MatIconModule, MatCardModule,
    MatDialogModule, MatTooltipModule, MatProgressSpinnerModule, MatChipsModule
  ],
  template: `
    <div class="page-container">
      <div class="page-header" style="display:flex;justify-content:space-between;align-items:center">
        <h2><mat-icon>sports</mat-icon> Koçlar</h2>
        <button mat-flat-button color="primary" (click)="openDialog()">
          <mat-icon>add</mat-icon> Yeni Koç
        </button>
      </div>

      <div *ngIf="loading()" class="loading-container"><mat-spinner diameter="48"></mat-spinner></div>

      <mat-card *ngIf="!loading()">
        <mat-card-content style="padding:0">
          <div *ngIf="coaches.length === 0" style="text-align:center;padding:48px;color:#999">
            <mat-icon style="font-size:48px;width:48px;height:48px;color:#ccc">sports</mat-icon>
            <p>Henüz koç bulunmuyor. "Yeni Koç" butonu ile koç ekleyebilirsiniz.</p>
          </div>
          <table mat-table [dataSource]="coaches" *ngIf="coaches.length > 0" class="full-width-table">
            <ng-container matColumnDef="name">
              <th mat-header-cell *matHeaderCellDef>Ad Soyad</th>
              <td mat-cell *matCellDef="let c">
                <div style="display:flex;align-items:center;gap:10px">
                  <div style="width:36px;height:36px;border-radius:50%;background:#7B1FA2;display:flex;align-items:center;justify-content:center;color:white;font-size:13px;font-weight:600">
                    {{ c.firstName?.charAt(0) }}{{ c.lastName?.charAt(0) }}
                  </div>
                  <span>{{ c.firstName }} {{ c.lastName }}</span>
                </div>
              </td>
            </ng-container>
            <ng-container matColumnDef="email">
              <th mat-header-cell *matHeaderCellDef>E-posta</th>
              <td mat-cell *matCellDef="let c">{{ c.email }}</td>
            </ng-container>
            <ng-container matColumnDef="status">
              <th mat-header-cell *matHeaderCellDef>Durum</th>
              <td mat-cell *matCellDef="let c">
                <mat-chip [style.background]="c.isActive ? '#E8F5E9' : '#FFEBEE'"
                          [style.color]="c.isActive ? '#388E3C' : '#D32F2F'">
                  {{ c.isActive ? 'Aktif' : 'Pasif' }}
                </mat-chip>
              </td>
            </ng-container>
            <ng-container matColumnDef="lastLogin">
              <th mat-header-cell *matHeaderCellDef>Son Giriş</th>
              <td mat-cell *matCellDef="let c">{{ c.lastLoginAt ? (c.lastLoginAt | date:'dd.MM.yyyy') : '—' }}</td>
            </ng-container>
            <ng-container matColumnDef="actions">
              <th mat-header-cell *matHeaderCellDef></th>
              <td mat-cell *matCellDef="let c">
                <button mat-icon-button matTooltip="Koçluk İlişkileri" color="primary"
                  [routerLink]="['/admin/coaching/relationships']">
                  <mat-icon>people</mat-icon>
                </button>
              </td>
            </ng-container>
            <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
            <tr mat-row *matRowDef="let row; columns: displayedColumns;"></tr>
          </table>
        </mat-card-content>
      </mat-card>
    </div>
  `
})
export class CoachesListComponent extends BaseComponent implements OnInit {
  private usersService = inject(UsersService);
  private dialog = inject(MatDialog);

  coaches: UserDto[] = [];
  displayedColumns = ['name', 'email', 'status', 'lastLogin', 'actions'];

  ngOnInit(): void { this.loadCoaches(); }

  loadCoaches(): void {
    this.loading.set(true);
    this.usersService.getUsers(undefined, 'Coach')
      .pipe(takeUntil(this.destroy$), finalize(() => this.loading.set(false)))
      .subscribe({
        next: coaches => this.coaches = coaches,
        error: () => this.toaster.error('Koçlar yüklenirken bir hata oluştu')
      });
  }

  openDialog(): void {
    this.dialog.open(CoachDialogComponent, { width: '500px', disableClose: true })
      .afterClosed().subscribe(result => { if (result) this.loadCoaches(); });
  }
}
