import { Component, OnInit, inject, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { finalize, takeUntil, forkJoin } from 'rxjs';
import { MatCardModule } from '@angular/material/card';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDialog, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialogModule } from '@angular/material/dialog';
import { BaseComponent } from '../../../core/components/base.component';
import {
  CoachingService, Goal, CoachingSession, Assignment, ExamResult, Subject, WeakSubjectAnalysis, StudySession
} from '../../../core/services/coaching.service';

// ── Hedef İlerleme Check-in Dialog ────────────────────────────────────────────
@Component({
  selector: 'app-coach-goal-checkin-dialog',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatDialogModule, MatFormFieldModule,
    MatInputModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>İlerleme Güncelle</h2>
    <mat-dialog-content [formGroup]="form" style="display:flex;flex-direction:column;gap:12px;min-width:320px;padding-top:8px">
      <p style="margin:0;color:#666">Mevcut: %{{ data.current }}</p>
      <mat-form-field appearance="outline">
        <mat-label>Yeni Tamamlanma Yüzdesi (0-100)</mat-label>
        <input matInput type="number" formControlName="newPercentage" min="0" max="100">
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>Not (opsiyonel)</mat-label>
        <textarea matInput formControlName="note" rows="2"></textarea>
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>İptal</button>
      <button mat-raised-button color="primary" [disabled]="form.invalid" (click)="submit()">Güncelle</button>
    </mat-dialog-actions>
  `
})
export class CoachGoalCheckinDialogComponent {
  form: FormGroup;
  private fb = inject(FormBuilder);
  private ref = inject(MatDialogRef<CoachGoalCheckinDialogComponent>);
  data: { current: number; title: string } = inject(MAT_DIALOG_DATA);

  constructor() {
    this.form = this.fb.group({
      newPercentage: [this.data.current, [Validators.required, Validators.min(0), Validators.max(100)]],
      note: ['']
    });
  }

  submit(): void {
    if (this.form.invalid) return;
    const v = this.form.value;
    this.ref.close({ newPercentage: v.newPercentage, note: v.note || undefined });
  }
}

// ── Yeni Hedef Dialog ─────────────────────────────────────────────────────────
@Component({
  selector: 'app-new-goal-dialog',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatDialogModule, MatFormFieldModule,
    MatInputModule, MatSelectModule, MatButtonModule, MatDatepickerModule, MatNativeDateModule],
  template: `
    <h2 mat-dialog-title>Yeni Hedef</h2>
    <mat-dialog-content [formGroup]="form" style="display:flex;flex-direction:column;gap:12px;min-width:360px;padding-top:8px">
      <mat-form-field appearance="outline">
        <mat-label>Başlık</mat-label>
        <input matInput formControlName="title">
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>Seviye</mat-label>
        <mat-select formControlName="level">
          <mat-option value="Yearly">Yıllık</mat-option>
          <mat-option value="Monthly">Aylık</mat-option>
          <mat-option value="Weekly">Haftalık</mat-option>
          <mat-option value="Daily">Günlük</mat-option>
        </mat-select>
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>Hedef Tarih</mat-label>
        <input matInput [matDatepicker]="dp" formControlName="targetDate">
        <mat-datepicker-toggle matIconSuffix [for]="dp"></mat-datepicker-toggle>
        <mat-datepicker #dp></mat-datepicker>
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>Başarı Kriteri</mat-label>
        <textarea matInput formControlName="successCriteria" rows="2" placeholder="Örn: Matematik netim 35 olacak"></textarea>
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>Açıklama</mat-label>
        <textarea matInput formControlName="description" rows="2"></textarea>
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>İptal</button>
      <button mat-raised-button color="primary" [disabled]="form.invalid" (click)="submit()">Kaydet</button>
    </mat-dialog-actions>
  `
})
export class NewGoalDialogComponent {
  form: FormGroup;
  private fb = inject(FormBuilder);
  private ref = inject(MatDialogRef<NewGoalDialogComponent>);
  @Inject(MAT_DIALOG_DATA) data: { studentId: string } = inject(MAT_DIALOG_DATA);

  constructor() {
    this.form = this.fb.group({
      title: ['', Validators.required],
      level: ['Monthly', Validators.required],
      targetDate: [null, Validators.required],
      successCriteria: [''],
      description: ['']
    });
  }

  submit(): void {
    if (this.form.invalid) return;
    const v = this.form.value;
    this.ref.close({
      studentId: this.data.studentId,
      title: v.title,
      level: v.level,
      targetDate: (v.targetDate as Date).toISOString().split('T')[0],
      successCriteria: v.successCriteria || undefined,
      description: v.description || undefined
    });
  }
}

// ── Yeni Seans Dialog ─────────────────────────────────────────────────────────
@Component({
  selector: 'app-new-session-dialog',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatDialogModule, MatFormFieldModule,
    MatInputModule, MatSelectModule, MatButtonModule, MatDatepickerModule, MatNativeDateModule],
  template: `
    <h2 mat-dialog-title>Seans Planla</h2>
    <mat-dialog-content [formGroup]="form" style="display:flex;flex-direction:column;gap:12px;min-width:360px;padding-top:8px">
      <mat-form-field appearance="outline">
        <mat-label>Tarih</mat-label>
        <input matInput [matDatepicker]="dp" formControlName="scheduledDate">
        <mat-datepicker-toggle matIconSuffix [for]="dp"></mat-datepicker-toggle>
        <mat-datepicker #dp></mat-datepicker>
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>Saat (HH:mm)</mat-label>
        <input matInput formControlName="scheduledTime" placeholder="09:00">
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
      <mat-form-field appearance="outline">
        <mat-label>Özel Notlar (Sadece siz görürsünüz)</mat-label>
        <textarea matInput formControlName="coachPrivateNotes" rows="2"></textarea>
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>İptal</button>
      <button mat-raised-button color="primary" [disabled]="form.invalid" (click)="submit()">Planla</button>
    </mat-dialog-actions>
  `
})
export class NewSessionDialogComponent {
  form: FormGroup;
  private fb = inject(FormBuilder);
  private ref = inject(MatDialogRef<NewSessionDialogComponent>);
  @Inject(MAT_DIALOG_DATA) data: { studentId: string; relationshipId: string } = inject(MAT_DIALOG_DATA);

  constructor() {
    this.form = this.fb.group({
      scheduledDate: [null, Validators.required],
      scheduledTime: ['10:00', [Validators.required, Validators.pattern(/^\d{2}:\d{2}$/)]],
      sessionType: ['Regular', Validators.required],
      plannedDurationMinutes: [60, [Validators.required, Validators.min(15)]],
      meetingLink: [''],
      coachPrivateNotes: ['']
    });
  }

  submit(): void {
    if (this.form.invalid) return;
    const v = this.form.value;
    const date = v.scheduledDate as Date;
    const [h, m] = (v.scheduledTime as string).split(':').map(Number);
    date.setHours(h, m, 0, 0);
    this.ref.close({
      studentId: this.data.studentId,
      coachingRelationshipId: this.data.relationshipId,
      scheduledAt: date.toISOString(),
      sessionType: v.sessionType,
      plannedDurationMinutes: v.plannedDurationMinutes,
      meetingLink: v.meetingLink || undefined,
      coachPrivateNotes: v.coachPrivateNotes || undefined
    });
  }
}

// ── Yeni Ödev Dialog ──────────────────────────────────────────────────────────
@Component({
  selector: 'app-new-assignment-dialog',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatDialogModule, MatFormFieldModule,
    MatInputModule, MatSelectModule, MatButtonModule, MatDatepickerModule, MatNativeDateModule],
  template: `
    <h2 mat-dialog-title>Ödev Ver</h2>
    <mat-dialog-content [formGroup]="form" style="display:flex;flex-direction:column;gap:12px;min-width:360px;padding-top:8px">
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
        <mat-label>Hedef Soru Sayısı</mat-label>
        <input matInput type="number" formControlName="targetQuestions">
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>Hedef Süre (dakika)</mat-label>
        <input matInput type="number" formControlName="targetMinutes">
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>Açıklama</mat-label>
        <textarea matInput formControlName="description" rows="2"></textarea>
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>İptal</button>
      <button mat-raised-button color="primary" [disabled]="form.invalid" (click)="submit()">Ver</button>
    </mat-dialog-actions>
  `
})
export class NewAssignmentDialogComponent {
  form: FormGroup;
  private fb = inject(FormBuilder);
  private ref = inject(MatDialogRef<NewAssignmentDialogComponent>);
  @Inject(MAT_DIALOG_DATA) data: { studentId: string; subjects: Subject[] } = inject(MAT_DIALOG_DATA);

  constructor() {
    this.form = this.fb.group({
      title: ['', Validators.required],
      subjectId: [null],
      dueDate: [null, Validators.required],
      targetQuestions: [null],
      targetMinutes: [null],
      description: ['']
    });
  }

  submit(): void {
    if (this.form.invalid) return;
    const v = this.form.value;
    this.ref.close({
      studentId: this.data.studentId,
      title: v.title,
      subjectId: v.subjectId || undefined,
      dueDate: (v.dueDate as Date).toISOString(),
      targetQuestions: v.targetQuestions || undefined,
      targetMinutes: v.targetMinutes || undefined,
      description: v.description || undefined
    });
  }
}

// ── Sınav Sonucu Dialog ───────────────────────────────────────────────────────
@Component({
  selector: 'app-new-exam-dialog',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatDialogModule, MatFormFieldModule,
    MatInputModule, MatSelectModule, MatButtonModule, MatDatepickerModule, MatNativeDateModule],
  template: `
    <h2 mat-dialog-title>Sınav Sonucu Gir</h2>
    <mat-dialog-content [formGroup]="form" style="display:flex;flex-direction:column;gap:12px;min-width:360px;padding-top:8px">
      <mat-form-field appearance="outline">
        <mat-label>Sınav Adı</mat-label>
        <input matInput formControlName="examName" placeholder="Örn: 5. TYT Denemesi">
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>Sınav Tarihi</mat-label>
        <input matInput [matDatepicker]="dp" formControlName="examDate">
        <mat-datepicker-toggle matIconSuffix [for]="dp"></mat-datepicker-toggle>
        <mat-datepicker #dp></mat-datepicker>
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>Sınav Tipi</mat-label>
        <mat-select formControlName="examType">
          <mat-option value="YKS_TYT">YKS TYT</mat-option>
          <mat-option value="YKS_AYT_Sayisal">AYT Sayısal</mat-option>
          <mat-option value="YKS_AYT_EsitAgirlik">AYT Eşit Ağırlık</mat-option>
          <mat-option value="YKS_AYT_Sozel">AYT Sözel</mat-option>
          <mat-option value="LGS">LGS</mat-option>
          <mat-option value="General">Genel</mat-option>
        </mat-select>
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>Notlar</mat-label>
        <textarea matInput formControlName="notes" rows="2"></textarea>
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>İptal</button>
      <button mat-raised-button color="primary" [disabled]="form.invalid" (click)="submit()">Kaydet</button>
    </mat-dialog-actions>
  `
})
export class NewExamDialogComponent {
  form: FormGroup;
  private fb = inject(FormBuilder);
  private ref = inject(MatDialogRef<NewExamDialogComponent>);
  @Inject(MAT_DIALOG_DATA) data: { studentId: string } = inject(MAT_DIALOG_DATA);

  constructor() {
    this.form = this.fb.group({
      examName: ['', Validators.required],
      examDate: [null, Validators.required],
      examType: ['YKS_TYT', Validators.required],
      notes: ['']
    });
  }

  submit(): void {
    if (this.form.invalid) return;
    const v = this.form.value;
    this.ref.close({
      studentId: this.data.studentId,
      examName: v.examName,
      examDate: (v.examDate as Date).toISOString().split('T')[0],
      examType: v.examType,
      notes: v.notes || undefined
    });
  }
}

// ── Seansı Tamamla Dialog ─────────────────────────────────────────────────────
@Component({
  selector: 'app-complete-session-detail-dialog',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatDialogModule, MatFormFieldModule,
    MatInputModule, MatSelectModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>Seansı Tamamla</h2>
    <mat-dialog-content [formGroup]="form" style="display:flex;flex-direction:column;gap:12px;min-width:420px;padding-top:8px">
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
        <mat-label>Koç Özel Notları</mat-label>
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
export class CompleteSessionDetailDialogComponent {
  form: FormGroup;
  private fb = inject(FormBuilder);
  private ref = inject(MatDialogRef<CompleteSessionDetailDialogComponent>);
  data: { session: CoachingSession } = inject(MAT_DIALOG_DATA);

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

// ── Ana Component ─────────────────────────────────────────────────────────────
@Component({
  selector: 'app-coach-student-detail',
  standalone: true,
  imports: [
    CommonModule, FormsModule,
    MatCardModule, MatTabsModule, MatTableModule, MatButtonModule, MatIconModule,
    MatFormFieldModule, MatInputModule, MatProgressSpinnerModule,
    MatProgressBarModule, MatTooltipModule, MatChipsModule
  ],
  templateUrl: './coach-student-detail.component.html'
})
export class CoachStudentDetailComponent extends BaseComponent implements OnInit {
  private service = inject(CoachingService);
  private route = inject(ActivatedRoute);
  private dialog = inject(MatDialog);

  studentId = '';
  studentName = '';
  relationshipId = '';

  goals: Goal[] = [];
  sessions: CoachingSession[] = [];
  studySessions: StudySession[] = [];
  assignments: Assignment[] = [];
  examResults: ExamResult[] = [];
  subjects: Subject[] = [];
  weakSubjects: WeakSubjectAnalysis[] = [];
  weakSubjectColumns = ['subjectName', 'averageNet', 'deficit', 'trend', 'dataPoints'];

  goalColumns = ['title', 'level', 'targetDate', 'progress', 'actions'];
  sessionColumns = ['scheduledAt', 'type', 'duration', 'status', 'sessionActions'];
  studySessionColumns = ['date', 'subject', 'studyType', 'duration', 'questions', 'verified', 'verifyAction'];
  assignmentColumns = ['title', 'dueDate', 'status'];
  examColumns = ['examName', 'examDate', 'examType', 'totalNet'];

  levelLabels: Record<string, string> = { Yearly: 'Yıllık', Monthly: 'Aylık', Weekly: 'Haftalık', Daily: 'Günlük' };
  statusLabels: Record<string, string> = { Active: 'Aktif', Completed: 'Tamamlandı', Failed: 'Başarısız', Paused: 'Duraklıyor' };
  sessionStatusLabels: Record<string, string> = { Scheduled: 'Planlandı', Completed: 'Tamamlandı', Cancelled: 'İptal', NoShow: 'Gelmedi' };
  sessionTypeLabels: Record<string, string> = { Regular: 'Rutin', GoalReview: 'Hedef', ExamReview: 'Sınav', Emergency: 'Acil', Closing: 'Kapanış' };
  assignmentStatusLabels: Record<string, string> = { Pending: 'Bekliyor', Completed: 'Tamamlandı', PartiallyCompleted: 'Kısmen', Overdue: 'Gecikti', Cancelled: 'İptal' };
  examTypeLabels: Record<string, string> = { YKS_TYT: 'TYT', YKS_AYT_Sayisal: 'AYT Say.', YKS_AYT_EsitAgirlik: 'AYT E.A.', YKS_AYT_Sozel: 'AYT Söz.', LGS: 'LGS', General: 'Genel' };
  studyTypeLabels: Record<string, string> = { Reading: 'Okuma', Practice: 'Alıştırma', Exam: 'Sınav', Review: 'Tekrar', Video: 'Video', Other: 'Diğer' };

  ngOnInit(): void {
    this.studentId = this.route.snapshot.paramMap.get('id') ?? '';
    this.loadAll();
  }

  loadAll(): void {
    this.loading.set(true);
    forkJoin({
      rels: this.service.getRelationships({ studentId: this.studentId, pageSize: 1 }),
      goals: this.service.getGoals({ studentId: this.studentId, pageSize: 50 }),
      sessions: this.service.getCoachingSessions({ studentId: this.studentId, pageSize: 20 }),
      studySessions: this.service.getStudySessions({ studentId: this.studentId, pageSize: 30 }),
      assignments: this.service.getAssignments({ studentId: this.studentId, pageSize: 20 }),
      exams: this.service.getExamResults({ studentId: this.studentId, pageSize: 20 }),
      subjects: this.service.getSubjects()
    })
      .pipe(takeUntil(this.destroy$), finalize(() => this.loading.set(false)))
      .subscribe({
        next: r => {
          const rel = r.rels.items[0];
          if (rel) {
            this.relationshipId = rel.id;
            this.studentName = rel.studentName || '';
          }
          this.goals = r.goals.items;
          this.sessions = r.sessions.items;
          this.studySessions = r.studySessions.items;
          this.assignments = r.assignments.items;
          this.examResults = r.exams.items;
          this.subjects = r.subjects;
          this.loadWeakSubjects();
        },
        error: e => this.handleError(e, 'Öğrenci verileri yüklenemedi')
      });
  }

  loadWeakSubjects(): void {
    this.service.getWeakSubjectAnalysis({ studentId: this.studentId, lastN: 5 })
      .pipe(takeUntil(this.destroy$))
      .subscribe({ next: r => this.weakSubjects = r });
  }

  // ── Dialog aksiyonları ────────────────────────────────────────────────────
  openNewGoal(): void {
    const ref = this.dialog.open(NewGoalDialogComponent, {
      data: { studentId: this.studentId }, width: '420px'
    });
    ref.afterClosed().subscribe(data => {
      if (!data) return;
      this.service.createGoal(data)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => { this.handleSuccess('Hedef oluşturuldu'); this.loadGoals(); },
          error: e => this.handleError(e)
        });
    });
  }

  openNewSession(): void {
    const ref = this.dialog.open(NewSessionDialogComponent, {
      data: { studentId: this.studentId, relationshipId: this.relationshipId }, width: '420px'
    });
    ref.afterClosed().subscribe(data => {
      if (!data) return;
      this.service.createCoachingSession(data)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => { this.handleSuccess('Seans planlandı'); this.loadSessions(); },
          error: e => this.handleError(e)
        });
    });
  }

  openNewAssignment(): void {
    const ref = this.dialog.open(NewAssignmentDialogComponent, {
      data: { studentId: this.studentId, subjects: this.subjects }, width: '420px'
    });
    ref.afterClosed().subscribe(data => {
      if (!data) return;
      this.service.createAssignment(data)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => { this.handleSuccess('Ödev verildi'); this.loadAssignments(); },
          error: e => this.handleError(e)
        });
    });
  }

  openNewExam(): void {
    const ref = this.dialog.open(NewExamDialogComponent, {
      data: { studentId: this.studentId }, width: '420px'
    });
    ref.afterClosed().subscribe(data => {
      if (!data) return;
      this.service.createExamResult(data)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => { this.handleSuccess('Sınav sonucu kaydedildi'); this.loadExams(); },
          error: e => this.handleError(e)
        });
    });
  }

  checkInGoal(goal: Goal): void {
    const ref = this.dialog.open(CoachGoalCheckinDialogComponent, {
      data: { current: goal.completionPercentage, title: goal.title }, width: '380px'
    });
    ref.afterClosed().pipe(takeUntil(this.destroy$)).subscribe(data => {
      if (!data) return;
      this.service.checkInGoal(goal.id, data)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => { this.handleSuccess('İlerleme güncellendi'); this.loadGoals(); },
          error: e => this.handleError(e)
        });
    });
  }

  completeSession(session: CoachingSession): void {
    const ref = this.dialog.open(CompleteSessionDetailDialogComponent, {
      data: { session }, width: '480px'
    });
    ref.afterClosed().pipe(takeUntil(this.destroy$)).subscribe(data => {
      if (!data) return;
      this.service.updateCoachingSession(session.id, data)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => { this.handleSuccess('Seans tamamlandı'); this.loadSessions(); },
          error: e => this.handleError(e)
        });
    });
  }

  verifyStudySession(id: string): void {
    this.service.verifyStudySession(id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => { this.handleSuccess('Çalışma doğrulandı'); this.loadStudySessions(); },
        error: e => this.handleError(e)
      });
  }

  getSubjectName(subjectId: string): string {
    return this.subjects.find(s => s.id === subjectId)?.name ?? subjectId.slice(0, 8) + '…';
  }

  // ── Tekil yükleyiciler ────────────────────────────────────────────────────
  loadGoals(): void {
    this.service.getGoals({ studentId: this.studentId, pageSize: 50 })
      .pipe(takeUntil(this.destroy$))
      .subscribe(r => this.goals = r.items);
  }

  loadSessions(): void {
    this.service.getCoachingSessions({ studentId: this.studentId, pageSize: 20 })
      .pipe(takeUntil(this.destroy$))
      .subscribe(r => this.sessions = r.items);
  }

  loadAssignments(): void {
    this.service.getAssignments({ studentId: this.studentId, pageSize: 20 })
      .pipe(takeUntil(this.destroy$))
      .subscribe(r => this.assignments = r.items);
  }

  loadExams(): void {
    this.service.getExamResults({ studentId: this.studentId, pageSize: 20 })
      .pipe(takeUntil(this.destroy$))
      .subscribe(r => this.examResults = r.items);
  }

  loadStudySessions(): void {
    this.service.getStudySessions({ studentId: this.studentId, pageSize: 30 })
      .pipe(takeUntil(this.destroy$))
      .subscribe(r => this.studySessions = r.items);
  }

  get activeGoals(): number { return this.goals.filter(g => g.status === 'Active').length; }
  get pendingAssignments(): number { return this.assignments.filter(a => a.status === 'Pending').length; }
  get lastSession(): CoachingSession | null { return this.sessions[0] ?? null; }
}
