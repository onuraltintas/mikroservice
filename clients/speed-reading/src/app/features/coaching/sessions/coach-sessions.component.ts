import { Component, OnInit, inject, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { finalize, takeUntil } from 'rxjs';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatChipsModule } from '@angular/material/chips';
import { MatDividerModule } from '@angular/material/divider';
import { MatDialog, MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { BaseComponent } from '../../../core/components/base.component';
import {
  CoachingService, CoachingSession, CoachingRelationship, SessionBriefing, Subject
} from '../../../core/services/coaching.service';

// ── Yeni Seans Dialog ─────────────────────────────────────────────────────────
@Component({
  selector: 'app-new-coaching-session-dialog',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatDialogModule, MatFormFieldModule,
    MatInputModule, MatSelectModule, MatButtonModule, MatDatepickerModule, MatNativeDateModule],
  template: `
    <h2 mat-dialog-title>Seans Planla</h2>
    <mat-dialog-content [formGroup]="form" class="ui-dialog-content ui-dialog-form ui-dialog-content--lg">
      <mat-form-field appearance="outline">
        <mat-label>Öğrenci</mat-label>
        <mat-select formControlName="studentId" (selectionChange)="onStudentChange($event.value)">
          <mat-option *ngFor="let r of data.relationships" [value]="r.studentId">
            {{ r.studentName || (r.studentId.slice(0,8) + '...') }}
          </mat-option>
        </mat-select>
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>Tarih</mat-label>
        <input matInput [matDatepicker]="dp" formControlName="scheduledDate">
        <mat-datepicker-toggle matIconSuffix [for]="dp"></mat-datepicker-toggle>
        <mat-datepicker #dp></mat-datepicker>
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>Saat (HH:mm)</mat-label>
        <input matInput formControlName="scheduledTime" placeholder="10:00">
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>Seans Tipi</mat-label>
        <mat-select formControlName="sessionType">
          <mat-option value="Regular">Rutin</mat-option>
          <mat-option value="GoalReview">Hedef Gözden Geçirme</mat-option>
          <mat-option value="ExamReview">Sınav Analizi</mat-option>
          <mat-option value="Emergency">Acil</mat-option>
          <mat-option value="Closing">Kapanış</mat-option>
        </mat-select>
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>Süre (dakika)</mat-label>
        <input matInput type="number" formControlName="plannedDurationMinutes">
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>Meeting Linki</mat-label>
        <input matInput formControlName="meetingLink" placeholder="https://meet.google.com/...">
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>İptal</button>
      <button mat-raised-button color="primary" [disabled]="form.invalid" (click)="submit()">Planla</button>
    </mat-dialog-actions>
  `
})
export class NewCoachingSessionDialogComponent {
  form: FormGroup;
  private fb = inject(FormBuilder);
  private ref = inject(MatDialogRef<NewCoachingSessionDialogComponent>);
  @Inject(MAT_DIALOG_DATA) data: { relationships: CoachingRelationship[] } = inject(MAT_DIALOG_DATA);

  private selectedRelationshipId = '';

  constructor() {
    this.form = this.fb.group({
      studentId: [null, Validators.required],
      scheduledDate: [null, Validators.required],
      scheduledTime: ['10:00', [Validators.required, Validators.pattern(/^\d{2}:\d{2}$/)]],
      sessionType: ['Regular', Validators.required],
      plannedDurationMinutes: [60, [Validators.required, Validators.min(15)]],
      meetingLink: ['']
    });
  }

  onStudentChange(studentId: string): void {
    const rel = this.data.relationships.find(r => r.studentId === studentId);
    this.selectedRelationshipId = rel?.id ?? '';
  }

  submit(): void {
    if (this.form.invalid) return;
    const v = this.form.value;
    const date = v.scheduledDate as Date;
    const [h, m] = (v.scheduledTime as string).split(':').map(Number);
    date.setHours(h, m, 0, 0);
    this.ref.close({
      studentId: v.studentId,
      coachingRelationshipId: this.selectedRelationshipId,
      scheduledAt: date.toISOString(),
      sessionType: v.sessionType,
      plannedDurationMinutes: v.plannedDurationMinutes,
      meetingLink: v.meetingLink || undefined
    });
  }
}

// ── Seans Brifing Dialog ──────────────────────────────────────────────────────
@Component({
  selector: 'app-session-briefing-dialog',
  standalone: true,
  imports: [CommonModule, MatDialogModule, MatButtonModule, MatIconModule,
    MatChipsModule, MatDividerModule, MatProgressSpinnerModule],
  template: `
    <h2 mat-dialog-title>
      <mat-icon>summarize</mat-icon>
      Seans Brifing — {{ briefing?.studentName }}
    </h2>
    <mat-dialog-content class="ui-dialog-content ui-dialog-content--briefing">
      <div *ngIf="loading" class="loading-container"><mat-spinner diameter="40"></mat-spinner></div>
      <div *ngIf="!loading && briefing">
        <!-- Uyarılar -->
        <div *ngIf="briefing.alertFlags.length > 0" class="ui-section-block--bottom">
          <div *ngFor="let flag of briefing.alertFlags"
            class="ui-alert--warning ui-list-item">
            <mat-icon class="ui-icon--sm ui-icon--warning">warning</mat-icon>
            {{ flag }}
          </div>
        </div>

        <!-- Çalışma Özeti -->
        <div class="ui-section-block--bottom">
          <strong>Çalışma (son {{ briefing.daysSinceLastSession }} gün)</strong>
          <div class="ui-summary-metrics">
            <div><span class="ui-metric-value">{{ briefing.studySummary.totalMinutes }}</span><br><small>dakika</small></div>
            <div><span class="ui-metric-value">{{ briefing.studySummary.totalQuestions }}</span><br><small>soru</small></div>
            <div><span class="ui-metric-value">{{ briefing.studySummary.totalSessions }}</span><br><small>oturum</small></div>
          </div>
          <div *ngIf="briefing.studySummary.subjectsWorked.length > 0" class="ui-section-block">
            <mat-chip-set>
              <mat-chip *ngFor="let s of briefing.studySummary.subjectsWorked">{{ s }}</mat-chip>
            </mat-chip-set>
          </div>
        </div>
        <mat-divider></mat-divider>

        <!-- Tamamlanan Hedefler -->
        <div *ngIf="briefing.goalsCompleted.length > 0" class="ui-section-block">
          <strong class="ui-note--success"><mat-icon class="ui-icon--xs ui-icon--success">check_circle</mat-icon> Tamamlanan Hedefler ({{ briefing.goalsCompleted.length }})</strong>
          <div *ngFor="let g of briefing.goalsCompleted" class="ui-list-item">
            {{ g.title }} <small class="text-muted">({{ g.level }})</small>
          </div>
        </div>

        <!-- Geri Kalan Hedefler -->
        <div *ngIf="briefing.goalsStalled.length > 0" class="ui-section-block">
          <strong class="ui-note--danger"><mat-icon class="ui-icon--xs ui-icon--danger">warning</mat-icon> Geri Kalan Hedefler ({{ briefing.goalsStalled.length }})</strong>
          <div *ngFor="let g of briefing.goalsStalled" class="ui-list-item">
            {{ g.title }} — %{{ g.completionPercentage | number:'1.0-0' }}
            <small class="text-muted">(bitiş: {{ g.targetDate | date:'dd.MM' }})</small>
          </div>
        </div>

        <!-- Teslim Edilen Ödevler -->
        <div *ngIf="briefing.assignmentsSubmitted.length > 0" class="ui-section-block">
          <strong>Teslim Edilen Ödevler ({{ briefing.assignmentsSubmitted.length }})</strong>
          <div *ngFor="let a of briefing.assignmentsSubmitted" class="ui-list-item">
            {{ a.title }}
            <mat-icon *ngIf="a.isApprovedByCoach === true" class="ui-icon--xs ui-icon--success">verified</mat-icon>
            <mat-icon *ngIf="a.isApprovedByCoach === false" class="ui-icon--xs ui-icon--danger">unpublished</mat-icon>
            <mat-icon *ngIf="a.isApprovedByCoach == null" class="ui-icon--xs ui-icon--warning">pending</mat-icon>
          </div>
        </div>

        <!-- Sınavlar -->
        <div *ngIf="briefing.examsEntered.length > 0" class="ui-section-block">
          <strong>Girilen Sınavlar</strong>
          <div *ngFor="let e of briefing.examsEntered" class="ui-list-item">
            {{ e.examName }} — {{ e.examDate | date:'dd.MM' }}
            <strong *ngIf="e.totalNet">{{ e.totalNet | number:'1.1-1' }} net</strong>
          </div>
        </div>
      </div>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Kapat</button>
    </mat-dialog-actions>
  `
})
export class SessionBriefingDialogComponent implements OnInit {
  private service = inject(CoachingService);
  @Inject(MAT_DIALOG_DATA) data: { sessionId: string } = inject(MAT_DIALOG_DATA);

  briefing: SessionBriefing | null = null;
  loading = true;

  ngOnInit(): void {
    this.service.getSessionBriefing(this.data.sessionId).subscribe({
      next: b => { this.briefing = b; this.loading = false; },
      error: () => this.loading = false
    });
  }
}

// ── Seansı Tamamla Dialog ─────────────────────────────────────────────────────
@Component({
  selector: 'app-complete-session-dialog',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatDialogModule, MatFormFieldModule,
    MatInputModule, MatSelectModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>Seansı Tamamla</h2>
    <mat-dialog-content [formGroup]="form" class="ui-dialog-content ui-dialog-form ui-dialog-content--wide">
      <mat-form-field appearance="outline">
        <mat-label>Sonuç</mat-label>
        <mat-select formControlName="status">
          <mat-option value="Completed">Tamamlandı</mat-option>
          <mat-option value="NoShow">Öğrenci Gelmedi</mat-option>
        </mat-select>
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>Gerçek Süre (dakika)</mat-label>
        <input matInput type="number" formControlName="actualDurationMinutes">
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>Paylaşılan Notlar (öğrenci de görür)</mat-label>
        <textarea matInput formControlName="sharedNotes" rows="3"></textarea>
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>Koç Özel Notları (sadece siz görürsünüz)</mat-label>
        <textarea matInput formControlName="coachPrivateNotes" rows="2"></textarea>
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>Aksiyon Maddeleri (her satır bir madde)</mat-label>
        <textarea matInput formControlName="actionItems" rows="3" placeholder="Örn: Matematik Test 5 çöz&#10;TYT Türkçe konularını tekrar et"></textarea>
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>İptal</button>
      <button mat-raised-button color="primary" [disabled]="form.invalid" (click)="submit()">Kaydet</button>
    </mat-dialog-actions>
  `
})
export class CompleteSessionDialogComponent {
  form: FormGroup;
  private fb = inject(FormBuilder);
  private ref = inject(MatDialogRef<CompleteSessionDialogComponent>);
  @Inject(MAT_DIALOG_DATA) data: { session: CoachingSession } = inject(MAT_DIALOG_DATA);

  constructor() {
    this.form = this.fb.group({
      status: ['Completed', Validators.required],
      actualDurationMinutes: [this.data?.session?.plannedDurationMinutes ?? 60, [Validators.required, Validators.min(1)]],
      sharedNotes: [''],
      coachPrivateNotes: [''],
      actionItems: ['']
    });
  }

  submit(): void {
    if (this.form.invalid) return;
    const v = this.form.value;
    this.ref.close({
      status: v.status,
      actualDurationMinutes: v.actualDurationMinutes,
      sharedNotes: v.sharedNotes || undefined,
      coachPrivateNotes: v.coachPrivateNotes || undefined,
      actionItems: v.actionItems || undefined
    });
  }
}

// ── Action Item → Ödev Dialog ─────────────────────────────────────────────────
@Component({
  selector: 'app-action-to-assignment-dialog',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatDialogModule, MatFormFieldModule,
    MatInputModule, MatSelectModule, MatButtonModule, MatDatepickerModule, MatNativeDateModule],
  template: `
    <h2 mat-dialog-title>Action Item → Ödev</h2>
    <mat-dialog-content [formGroup]="form" class="ui-dialog-content ui-dialog-form ui-dialog-content--md">
      <mat-form-field appearance="outline">
        <mat-label>Ödev Başlığı</mat-label>
        <input matInput formControlName="title">
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>Ders</mat-label>
        <mat-select formControlName="subjectId">
          <mat-option [value]="null">-- Seçiniz --</mat-option>
          <mat-option *ngFor="let s of data.subjects" [value]="s.id">{{ s.name }}</mat-option>
        </mat-select>
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>Son Tarih</mat-label>
        <input matInput [matDatepicker]="dp" formControlName="dueDate">
        <mat-datepicker-toggle matIconSuffix [for]="dp"></mat-datepicker-toggle>
        <mat-datepicker #dp></mat-datepicker>
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>Hedef Soru</mat-label>
        <input matInput type="number" formControlName="targetQuestions">
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>Hedef Dakika</mat-label>
        <input matInput type="number" formControlName="targetMinutes">
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>İptal</button>
      <button mat-raised-button color="primary" [disabled]="form.invalid" (click)="submit()">Ödev Oluştur</button>
    </mat-dialog-actions>
  `
})
export class ActionToAssignmentDialogComponent {
  form: FormGroup;
  private fb = inject(FormBuilder);
  private ref = inject(MatDialogRef<ActionToAssignmentDialogComponent>);
  @Inject(MAT_DIALOG_DATA) data: { actionText: string; subjects: Subject[] } = inject(MAT_DIALOG_DATA);

  constructor() {
    this.form = this.fb.group({
      title: [this.data?.actionText || '', Validators.required],
      subjectId: [null],
      dueDate: [null, Validators.required],
      targetQuestions: [null],
      targetMinutes: [null]
    });
  }

  submit(): void {
    if (this.form.invalid) return;
    const v = this.form.value;
    this.ref.close({
      title: v.title,
      subjectId: v.subjectId || undefined,
      dueDate: (v.dueDate as Date).toISOString(),
      targetQuestions: v.targetQuestions || undefined,
      targetMinutes: v.targetMinutes || undefined
    });
  }
}

// ── Ana Component ─────────────────────────────────────────────────────────────
@Component({
  selector: 'app-coach-sessions',
  standalone: true,
  imports: [
    CommonModule, FormsModule, RouterModule,
    MatCardModule, MatTableModule, MatButtonModule, MatIconModule,
    MatSelectModule, MatFormFieldModule, MatPaginatorModule,
    MatProgressSpinnerModule, MatTooltipModule, MatChipsModule
  ],
  templateUrl: './coach-sessions.component.html'
})
export class CoachSessionsComponent extends BaseComponent implements OnInit {
  private service = inject(CoachingService);
  private dialog = inject(MatDialog);

  sessions: CoachingSession[] = [];
  relationships: CoachingRelationship[] = [];
  subjects: Subject[] = [];
  total = 0;
  page = 1;
  pageSize = 20;
  filterStatus = '';

  displayedColumns = ['scheduledAt', 'student', 'type', 'duration', 'status', 'actions'];

  statusOptions = [
    { value: '', label: 'Tüm Durumlar' },
    { value: 'Scheduled', label: 'Planlandı' },
    { value: 'Completed', label: 'Tamamlandı' },
    { value: 'Cancelled', label: 'İptal' },
    { value: 'NoShow', label: 'Gelmedi' },
    { value: 'Rescheduled', label: 'Ertelendi' }
  ];

  statusLabels: Record<string, string> = {
    Scheduled: 'Planlandı', Completed: 'Tamamlandı', Cancelled: 'İptal',
    NoShow: 'Gelmedi', Rescheduled: 'Ertelendi'
  };

  typeLabels: Record<string, string> = {
    Regular: 'Rutin', GoalReview: 'Hedef', ExamReview: 'Sınav', Emergency: 'Acil', Closing: 'Kapanış'
  };

  ngOnInit(): void {
    this.loadRelationships();
    this.load();
  }

  loadRelationships(): void {
    this.service.getRelationships({ status: 'Active', pageSize: 100 })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: r => {
          this.relationships = r.items;
          this.service.getSubjects().pipe(takeUntil(this.destroy$))
            .subscribe({ next: s => this.subjects = s });
        }
      });
  }

  load(): void {
    this.loading.set(true);
    this.service.getCoachingSessions({
      status: this.filterStatus || undefined,
      page: this.page,
      pageSize: this.pageSize
    })
      .pipe(takeUntil(this.destroy$), finalize(() => this.loading.set(false)))
      .subscribe({
        next: r => { this.sessions = r.items; this.total = r.total; },
        error: e => this.handleError(e, 'Seanslar yüklenemedi')
      });
  }

  openNewSession(): void {
    const ref = this.dialog.open(NewCoachingSessionDialogComponent, {
      data: { relationships: this.relationships }, width: '440px'
    });
    ref.afterClosed().subscribe(data => {
      if (!data) return;
      this.service.createCoachingSession(data)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => { this.handleSuccess('Seans planlandı'); this.load(); },
          error: e => this.handleError(e)
        });
    });
  }

  openBriefing(session: CoachingSession): void {
    this.dialog.open(SessionBriefingDialogComponent, {
      data: { sessionId: session.id }, width: '600px', maxHeight: '90vh'
    });
  }

  openActionAsAssignment(session: CoachingSession, actionText: string): void {
    const ref = this.dialog.open(ActionToAssignmentDialogComponent, {
      data: { actionText, subjects: this.subjects }, width: '400px'
    });
    ref.afterClosed().pipe(takeUntil(this.destroy$)).subscribe(data => {
      if (!data) return;
      this.service.createAssignmentFromSession(session.id, data)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => this.handleSuccess('Ödev oluşturuldu'),
          error: e => this.handleError(e)
        });
    });
  }

  getActionItems(session: CoachingSession): string[] {
    if (!session.actionItems) return [];
    return session.actionItems.split('\n').map(s => s.trim()).filter(s => s.length > 0);
  }

  completeSession(session: CoachingSession): void {
    const ref = this.dialog.open(CompleteSessionDialogComponent, {
      data: { session }, width: '480px'
    });
    ref.afterClosed().pipe(takeUntil(this.destroy$)).subscribe(data => {
      if (!data) return;
      this.service.updateCoachingSession(session.id, data)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => { this.handleSuccess('Seans tamamlandı'); this.load(); },
          error: e => this.handleError(e)
        });
    });
  }

  async cancelSession(s: CoachingSession): Promise<void> {
    const ok = await this.confirm('Bu seansı iptal etmek istiyor musunuz?');
    if (!ok) return;
    this.service.updateCoachingSession(s.id, { status: 'Cancelled' })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => { this.handleSuccess('Seans iptal edildi'); this.load(); },
        error: e => this.handleError(e)
      });
  }

  getStudentLabel(session: any): string {
    return session.studentName || (session.studentId.slice(0, 8) + '...');
  }

  onFilter(): void { this.page = 1; this.load(); }
  onPage(e: PageEvent): void { this.page = e.pageIndex + 1; this.pageSize = e.pageSize; this.load(); }
}
