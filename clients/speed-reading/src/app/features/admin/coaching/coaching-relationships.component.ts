import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { finalize, takeUntil } from 'rxjs';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatChipsModule } from '@angular/material/chips';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { BaseComponent } from '../../../core/components/base.component';
import { CoachingService, CoachingRelationship, CoachingStatus } from '../../../core/services/coaching.service';
import { UsersService } from '../../../core/services/users.service';
import { UserDto } from '../../../core/models/user.model';

// ─── Create Relationship Dialog ────────────────────────────────────────────────
@Component({
  selector: 'app-create-relationship-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule, MatDialogModule, MatFormFieldModule,
    MatSelectModule, MatButtonModule, MatIconModule, MatProgressSpinnerModule],
  template: `
    <h2 mat-dialog-title>Yeni Koçluk İlişkisi</h2>
    <mat-dialog-content class="ui-dialog-content ui-dialog-content--md">
      <div class="ui-stack">
        <mat-form-field appearance="outline">
          <mat-label>Öğrenci</mat-label>
          <mat-select [(ngModel)]="studentId">
            <mat-option *ngFor="let s of students" [value]="s.id">
              {{ s.firstName }} {{ s.lastName }} ({{ s.email }})
            </mat-option>
          </mat-select>
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Koç</mat-label>
          <mat-select [(ngModel)]="coachId">
            <mat-option *ngFor="let c of coaches" [value]="c.id">
              {{ c.firstName }} {{ c.lastName }} ({{ c.email }})
            </mat-option>
          </mat-select>
        </mat-form-field>
      </div>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>İptal</button>
      <button mat-raised-button color="primary" [disabled]="!studentId || !coachId" (click)="submit()">
        Oluştur
      </button>
    </mat-dialog-actions>
  `
})
export class CreateRelationshipDialogComponent {
  private dialogRef = inject(MatDialogRef<CreateRelationshipDialogComponent>);
  students: UserDto[] = [];
  coaches: UserDto[] = [];
  studentId = '';
  coachId = '';

  submit(): void {
    this.dialogRef.close({ studentId: this.studentId, coachId: this.coachId });
  }
}

// ─── Main Component ────────────────────────────────────────────────────────────
@Component({
  selector: 'app-coaching-relationships',
  standalone: true,
  imports: [
    CommonModule, FormsModule,
    MatCardModule, MatTableModule, MatButtonModule, MatIconModule,
    MatSelectModule, MatFormFieldModule, MatPaginatorModule,
    MatProgressSpinnerModule, MatChipsModule, MatTooltipModule
  ],
  templateUrl: './coaching-relationships.component.html'
})
export class CoachingRelationshipsComponent extends BaseComponent implements OnInit {
  private service = inject(CoachingService);
  private usersService = inject(UsersService);
  private dialog = inject(MatDialog);

  records: CoachingRelationship[] = [];
  total = 0;
  page = 1;
  pageSize = 20;
  filterStatus = '';

  displayedColumns = ['studentId', 'coachId', 'status', 'startDate', 'endDate', 'actions'];

  statusOptions: { value: string; label: string }[] = [
    { value: '', label: 'Tümü' },
    { value: 'Active', label: 'Aktif' },
    { value: 'Paused', label: 'Duraklatıldı' },
    { value: 'Completed', label: 'Tamamlandı' },
    { value: 'Cancelled', label: 'İptal Edildi' }
  ];

  statusColors: Record<string, string> = {
    Active: 'primary', Paused: 'accent', Completed: 'primary', Cancelled: 'warn'
  };

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading.set(true);
    this.service.getRelationships({ status: this.filterStatus || undefined, page: this.page, pageSize: this.pageSize })
      .pipe(takeUntil(this.destroy$), finalize(() => this.loading.set(false)))
      .subscribe({
        next: r => { this.records = r.items; this.total = r.total; },
        error: e => this.handleError(e, 'Koçluk ilişkileri yüklenemedi')
      });
  }

  openCreate(): void {
    const ref = this.dialog.open(CreateRelationshipDialogComponent, { width: '420px' });
    const instance = ref.componentInstance;

    this.usersService.getUsers(undefined, 'Student').pipe(takeUntil(this.destroy$))
      .subscribe(users => instance.students = users);
    this.usersService.getUsers(undefined, 'Coach').pipe(takeUntil(this.destroy$))
      .subscribe(users => instance.coaches = users);

    ref.afterClosed().pipe(takeUntil(this.destroy$)).subscribe(result => {
      if (!result) return;
      this.service.createRelationship(result)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => { this.handleSuccess('Koçluk ilişkisi oluşturuldu'); this.load(); },
          error: e => this.handleError(e)
        });
    });
  }

  onFilter(): void { this.page = 1; this.load(); }

  onPage(e: PageEvent): void { this.page = e.pageIndex + 1; this.pageSize = e.pageSize; this.load(); }

  statusLabel(s: string): string {
    return this.statusOptions.find(o => o.value === s)?.label ?? s;
  }

  async endRelationship(rel: CoachingRelationship): Promise<void> {
    const studentLabel = (rel as any).studentName || rel.studentId.slice(0, 8) + '...';
    const ok = await this.confirm(`"${studentLabel}" ilişkisini sonlandırmak istiyor musunuz?`);
    if (!ok) return;
    this.service.updateRelationshipStatus(rel.id, { status: 'Completed' })
      .pipe(takeUntil(this.destroy$))
      .subscribe({ next: () => { this.handleSuccess('İlişki sonlandırıldı'); this.load(); }, error: e => this.handleError(e) });
  }
}
