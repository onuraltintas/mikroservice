import { Component, OnInit, inject, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { finalize, forkJoin, takeUntil } from 'rxjs';
import { MatDialog, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { BaseComponent } from '../../../core/components/base.component';
import {
  CoachingService, Goal, CoachingSession, Assignment, ExamResult, CoachingRelationship, Subject, StudySession
} from '../../../core/services/coaching.service';

// ── Hedef Check-in Dialog ──────────────────────────────────────────────────────
@Component({
  selector: 'app-goal-checkin-dialog',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <div class="sp-dialog">
      <h2 class="sp-dialog-title">İlerleme Güncelle</h2>
      <div class="sp-dialog-content" [formGroup]="form" style="display:flex;flex-direction:column;gap:12px;min-width:340px;padding-top:8px">
        <p style="margin:0;color:#666">Mevcut: %{{ data.current }}</p>
        <div class="sp-field">
          <label>Yeni Tamamlanma Yüzdesi (0-100)</label>
          <input type="number" formControlName="newPercentage" min="0" max="100">
        </div>
        <div class="sp-field">
          <label>Not (opsiyonel)</label>
          <textarea formControlName="note" rows="2" placeholder="Bu hafta ne yaptın?"></textarea>
        </div>
      </div>
      <div class="sp-dialog-actions" style="display:flex;justify-content:flex-end;gap:8px">
        <button class="sp-btn" (click)="ref.close()">İptal</button>
        <button class="sp-btn sp-btn--primary" [disabled]="form.invalid" (click)="submit()">Güncelle</button>
      </div>
    </div>
  `
})
export class GoalCheckinDialogComponent {
  form: FormGroup;
  private fb = inject(FormBuilder);
  ref = inject(MatDialogRef<GoalCheckinDialogComponent>);
  data: { current: number } = inject(MAT_DIALOG_DATA);

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

// ── Ödev Tamamlama Dialog ──────────────────────────────────────────────────────
@Component({
  selector: 'app-assignment-complete-dialog',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <div class="sp-dialog">
      <h2 class="sp-dialog-title">Ödevi Tamamla</h2>
      <div class="sp-dialog-content" [formGroup]="form" style="display:flex;flex-direction:column;gap:12px;min-width:340px;padding-top:8px">
        <p style="margin:0;color:#666"><strong>{{ data.title }}</strong></p>
        <div class="sp-field">
          <label>Tamamlama Notu (opsiyonel)</label>
          <textarea formControlName="completionNote" rows="3"
            placeholder="Ödev hakkında bir not bırak..."></textarea>
        </div>
      </div>
      <div class="sp-dialog-actions" style="display:flex;justify-content:flex-end;gap:8px">
        <button class="sp-btn" (click)="ref.close()">İptal</button>
        <button class="sp-btn sp-btn--primary" (click)="submit()">Tamamlandı</button>
      </div>
    </div>
  `
})
export class AssignmentCompleteDialogComponent {
  form: FormGroup;
  private fb = inject(FormBuilder);
  ref = inject(MatDialogRef<AssignmentCompleteDialogComponent>);
  data: { title: string } = inject(MAT_DIALOG_DATA);

  constructor() {
    this.form = this.fb.group({ completionNote: [''] });
  }

  submit(): void {
    this.ref.close({ completionNote: this.form.value.completionNote || undefined });
  }
}

// ── Seans Rating Dialog ────────────────────────────────────────────────────────
@Component({
  selector: 'app-session-rating-dialog',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <div class="sp-dialog">
      <h2 class="sp-dialog-title">Seanı Değerlendir</h2>
      <div class="sp-dialog-content" style="min-width:320px;padding-top:8px">
        <p style="margin:0 0 12px;color:#666">{{ data.scheduledAt | date:'dd.MM.yyyy HH:mm' }}</p>
        <div style="display:flex;gap:8px;justify-content:center;margin-bottom:8px">
          <button *ngFor="let star of [1,2,3,4,5]"
            (click)="rating = star"
            style="background:none;border:none;cursor:pointer;font-size:28px;padding:4px;"
            [style.color]="star <= rating ? '#F57C00' : '#ccc'">
            {{ star <= rating ? '★' : '☆' }}
          </button>
        </div>
        <p style="text-align:center;color:#666;font-size:13px">{{ ratingLabel }}</p>
      </div>
      <div class="sp-dialog-actions" style="display:flex;justify-content:flex-end;gap:8px">
        <button class="sp-btn" (click)="ref.close()">İptal</button>
        <button class="sp-btn sp-btn--primary" [disabled]="!rating" (click)="submit()">Gönder</button>
      </div>
    </div>
  `
})
export class SessionRatingDialogComponent {
  rating = 0;
  ref = inject(MatDialogRef<SessionRatingDialogComponent>);
  data: { scheduledAt: string } = inject(MAT_DIALOG_DATA);

  get ratingLabel(): string {
    const labels = ['', 'Çok Kötü', 'Kötü', 'Ortalama', 'İyi', 'Mükemmel'];
    return labels[this.rating] ?? '';
  }

  submit(): void {
    if (!this.rating) return;
    this.ref.close({ studentRating: this.rating });
  }
}

// ── Yeni Hedef Dialog ─────────────────────────────────────────────────────────
@Component({
  selector: 'app-student-new-goal-dialog',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <div class="sp-dialog">
      <h2 class="sp-dialog-title">Yeni Hedef</h2>
      <div class="sp-dialog-content" [formGroup]="form" style="display:flex;flex-direction:column;gap:12px;min-width:340px;padding-top:8px">
        <div class="sp-field">
          <label>Başlık *</label>
          <input formControlName="title" placeholder="Hedefini yaz...">
        </div>
        <div class="sp-field">
          <label>Seviye</label>
          <select formControlName="level">
            <option value="Yearly">Yıllık</option>
            <option value="Monthly">Aylık</option>
            <option value="Weekly">Haftalık</option>
            <option value="Daily">Günlük</option>
          </select>
        </div>
        <div class="sp-field">
          <label>Hedef Tarih *</label>
          <input type="date" formControlName="targetDate">
        </div>
        <div class="sp-field">
          <label>Başarı Kriteri</label>
          <textarea formControlName="successCriteria" rows="2" placeholder="Ne zaman başardın sayılırsın?"></textarea>
        </div>
      </div>
      <div class="sp-dialog-actions" style="display:flex;justify-content:flex-end;gap:8px">
        <button class="sp-btn" (click)="ref.close()">İptal</button>
        <button class="sp-btn sp-btn--primary" [disabled]="form.invalid" (click)="submit()">Oluştur</button>
      </div>
    </div>
  `
})
export class StudentNewGoalDialogComponent {
  form: FormGroup;
  private fb = inject(FormBuilder);
  ref = inject(MatDialogRef<StudentNewGoalDialogComponent>);

  constructor() {
    const nextMonth = new Date();
    nextMonth.setMonth(nextMonth.getMonth() + 1);
    this.form = this.fb.group({
      title: ['', Validators.required],
      level: ['Monthly'],
      targetDate: [nextMonth.toISOString().substring(0, 10), Validators.required],
      successCriteria: ['']
    });
  }

  submit(): void {
    if (this.form.invalid) return;
    const v = this.form.value;
    this.ref.close({
      title: v.title,
      level: v.level,
      targetDate: v.targetDate,
      successCriteria: v.successCriteria || undefined
    });
  }
}

// ── Sınav Sonucu Ekle Dialog ──────────────────────────────────────────────────
@Component({
  selector: 'app-student-new-exam-dialog',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <div class="sp-dialog">
      <h2 class="sp-dialog-title">Sınav Sonucu Gir</h2>
      <div class="sp-dialog-content" [formGroup]="form" style="display:flex;flex-direction:column;gap:12px;min-width:340px;padding-top:8px">
        <div class="sp-field">
          <label>Sınav Adı *</label>
          <input formControlName="examName" placeholder="Örn: 5. TYT Denemesi">
        </div>
        <div class="sp-field">
          <label>Sınav Tarihi *</label>
          <input type="date" formControlName="examDate">
        </div>
        <div class="sp-field">
          <label>Sınav Tipi</label>
          <select formControlName="examType">
            <option value="YKS_TYT">YKS TYT</option>
            <option value="YKS_AYT_Sayisal">AYT Sayısal</option>
            <option value="YKS_AYT_EsitAgirlik">AYT Eşit Ağırlık</option>
            <option value="YKS_AYT_Sozel">AYT Sözel</option>
            <option value="LGS">LGS</option>
            <option value="General">Genel</option>
          </select>
        </div>
        <div class="sp-field">
          <label>Not (opsiyonel)</label>
          <textarea formControlName="notes" rows="2"></textarea>
        </div>
      </div>
      <div class="sp-dialog-actions" style="display:flex;justify-content:flex-end;gap:8px">
        <button class="sp-btn" (click)="ref.close()">İptal</button>
        <button class="sp-btn sp-btn--primary" [disabled]="form.invalid" (click)="submit()">Kaydet</button>
      </div>
    </div>
  `
})
export class StudentNewExamDialogComponent {
  form: FormGroup;
  private fb = inject(FormBuilder);
  ref = inject(MatDialogRef<StudentNewExamDialogComponent>);

  constructor() {
    this.form = this.fb.group({
      examName: ['', Validators.required],
      examDate: [new Date().toISOString().substring(0, 10), Validators.required],
      examType: ['YKS_TYT'],
      notes: ['']
    });
  }

  submit(): void {
    if (this.form.invalid) return;
    const v = this.form.value;
    this.ref.close({
      examName: v.examName,
      examDate: v.examDate,
      examType: v.examType,
      notes: v.notes || undefined
    });
  }
}

// ── Çalışma Seansı Kaydet Dialog ───────────────────────────────────────────────
@Component({
  selector: 'app-log-study-session-dialog',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <div class="sp-dialog">
      <h2 class="sp-dialog-title">Çalışma Seansı Kaydet</h2>
      <div class="sp-dialog-content" [formGroup]="form" style="display:flex;flex-direction:column;gap:12px;min-width:360px;padding-top:8px">
        <div class="sp-field">
          <label>Tarih</label>
          <input type="date" formControlName="date">
        </div>
        <div class="sp-field">
          <label>Ders</label>
          <select formControlName="subjectId">
            <option value="">-- Seçiniz --</option>
            <option *ngFor="let s of data.subjects" [value]="s.id">{{ s.name }}</option>
          </select>
        </div>
        <div class="sp-field">
          <label>Çalışma Türü</label>
          <select formControlName="studyType">
            <option value="Reading">Okuma</option>
            <option value="Practice">Pratik</option>
            <option value="Review">Tekrar</option>
            <option value="PastExam">Çıkmış Soru</option>
            <option value="Video">Video</option>
            <option value="Other">Diğer</option>
          </select>
        </div>
        <div class="sp-field">
          <label>Çalışma Süresi (dakika)</label>
          <input type="number" formControlName="actualMinutes" min="1">
        </div>
        <div class="sp-field">
          <label>Çözülen Soru Sayısı</label>
          <input type="number" formControlName="questionsSolved" min="0">
        </div>
        <div class="sp-field">
          <label>Doğru Sayısı</label>
          <input type="number" formControlName="correctAnswers" min="0">
        </div>
        <div class="sp-field">
          <label>Not (opsiyonel)</label>
          <textarea formControlName="notes" rows="2" placeholder="Bu çalışma hakkında bir not..."></textarea>
        </div>
      </div>
      <div class="sp-dialog-actions" style="display:flex;justify-content:flex-end;gap:8px">
        <button class="sp-btn" (click)="ref.close()">İptal</button>
        <button class="sp-btn sp-btn--primary" [disabled]="form.invalid" (click)="submit()">Kaydet</button>
      </div>
    </div>
  `
})
export class LogStudySessionDialogComponent {
  form: FormGroup;
  private fb = inject(FormBuilder);
  ref = inject(MatDialogRef<LogStudySessionDialogComponent>);
  data: { subjects: Subject[] } = inject(MAT_DIALOG_DATA);

  constructor() {
    const today = new Date().toISOString().substring(0, 10);
    this.form = this.fb.group({
      date: [today, Validators.required],
      subjectId: ['', Validators.required],
      studyType: ['Practice', Validators.required],
      actualMinutes: [null, [Validators.required, Validators.min(1)]],
      questionsSolved: [0],
      correctAnswers: [null],
      notes: ['']
    });
  }

  submit(): void {
    if (this.form.invalid) return;
    const v = this.form.value;
    this.ref.close({
      date: v.date,  // YYYY-MM-DD — backend DateOnly
      subjectId: v.subjectId,
      studyType: v.studyType,
      plannedMinutes: v.actualMinutes,
      actualMinutes: v.actualMinutes,
      questionsSolved: v.questionsSolved ?? 0,
      correctAnswers: v.correctAnswers || undefined,
      notes: v.notes || undefined
    });
  }
}

// ── Ana Component ──────────────────────────────────────────────────────────────
@Component({
  selector: 'app-student-coaching',
  standalone: true,
  imports: [
    CommonModule
  ],
  templateUrl: './student-coaching.component.html'
})
export class StudentCoachingComponent extends BaseComponent implements OnInit {
  private service = inject(CoachingService);
  private dialog = inject(MatDialog);

  relationship: CoachingRelationship | null = null;
  goals: Goal[] = [];
  sessions: CoachingSession[] = [];
  assignments: Assignment[] = [];
  examResults: ExamResult[] = [];
  studySessions: StudySession[] = [];
  subjects: Subject[] = [];
  reviewReminders: string[] = [];  // subject names due for review

  activeTab = 0;

  studyTypeLabels: Record<string, string> = {
    Reading: 'Okuma', Practice: 'Pratik', Review: 'Tekrar',
    PastExam: 'Çıkmış Soru', Video: 'Video', Other: 'Diğer'
  };
  levelLabels: Record<string, string> = { Yearly: 'Yıllık', Monthly: 'Aylık', Weekly: 'Haftalık', Daily: 'Günlük' };
  statusLabels: Record<string, string> = { Active: 'Aktif', Completed: 'Tamamlandı', Failed: 'Başarısız', Paused: 'Duraklıyor' };
  sessionStatusLabels: Record<string, string> = { Scheduled: 'Planlandı', Completed: 'Tamamlandı', Cancelled: 'İptal', NoShow: 'Gelmedi' };
  sessionTypeLabels: Record<string, string> = { Regular: 'Rutin', GoalReview: 'Hedef', ExamReview: 'Sınav', Emergency: 'Acil', Closing: 'Kapanış' };
  assignmentStatusLabels: Record<string, string> = { Pending: 'Bekliyor', Completed: 'Tamamlandı', PartiallyCompleted: 'Kısmen', Overdue: 'Gecikti', Cancelled: 'İptal' };
  examTypeLabels: Record<string, string> = { YKS_TYT: 'TYT', YKS_AYT_Sayisal: 'AYT Say.', YKS_AYT_EsitAgirlik: 'AYT E.A.', YKS_AYT_Sozel: 'AYT Söz.', LGS: 'LGS', General: 'Genel' };

  ngOnInit(): void {
    this.loadAll();
  }

  loadAll(): void {
    this.loading.set(true);
    forkJoin({
      rels: this.service.getRelationships({ status: 'Active', pageSize: 1 }),
      goals: this.service.getGoals({ page: 1, pageSize: 50 }),
      sessions: this.service.getCoachingSessions({ page: 1, pageSize: 20 }),
      assignments: this.service.getAssignments({ page: 1, pageSize: 20 }),
      exams: this.service.getExamResults({ page: 1, pageSize: 20 }),
      studySessions: this.service.getStudySessions({ page: 1, pageSize: 30 }),
      subjects: this.service.getSubjects()
    })
      .pipe(takeUntil(this.destroy$), finalize(() => this.loading.set(false)))
      .subscribe({
        next: r => {
          this.relationship = r.rels.items[0] ?? null;
          this.goals = r.goals.items;
          this.sessions = r.sessions.items;
          this.assignments = r.assignments.items;
          this.examResults = r.exams.items;
          this.studySessions = r.studySessions.items;
          this.subjects = r.subjects;
          this.computeReviewReminders();
        },
        error: e => this.handleError(e, 'Koçluk verileri yüklenemedi')
      });
  }

  private computeReviewReminders(): void {
    const sevenDaysAgo = new Date();
    sevenDaysAgo.setDate(sevenDaysAgo.getDate() - 7);
    const subjectMap = new Map<string, Date>();
    for (const s of this.studySessions) {
      const d = new Date(s.date);
      const prev = subjectMap.get(s.subjectId);
      if (!prev || d > prev) subjectMap.set(s.subjectId, d);
    }
    this.reviewReminders = [];
    subjectMap.forEach((lastDate, subjectId) => {
      if (lastDate < sevenDaysAgo) {
        const sub = this.subjects.find(s => s.id === subjectId);
        if (sub) this.reviewReminders.push(sub.name);
      }
    });
  }

  openCheckin(goal: Goal): void {
    const ref = this.dialog.open(GoalCheckinDialogComponent, {
      data: { current: goal.completionPercentage }, width: '380px'
    });
    ref.afterClosed().subscribe(data => {
      if (!data) return;
      this.service.checkInGoal(goal.id, data)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => { this.handleSuccess('İlerleme güncellendi'); this.reloadGoals(); },
          error: e => this.handleError(e)
        });
    });
  }

  openComplete(assignment: Assignment): void {
    const ref = this.dialog.open(AssignmentCompleteDialogComponent, {
      data: { title: assignment.title }, width: '380px'
    });
    ref.afterClosed().subscribe(data => {
      if (!data) return;
      this.service.updateAssignment(assignment.id, { status: 'Completed', ...data })
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => { this.handleSuccess('Ödev tamamlandı'); this.reloadAssignments(); },
          error: e => this.handleError(e)
        });
    });
  }

  openNewGoal(): void {
    const ref = this.dialog.open(StudentNewGoalDialogComponent, { width: '400px' });
    ref.afterClosed().subscribe(data => {
      if (!data) return;
      this.service.createGoal(data)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => { this.handleSuccess('Hedef oluşturuldu'); this.reloadGoals(); },
          error: e => this.handleError(e)
        });
    });
  }

  openNewExam(): void {
    const ref = this.dialog.open(StudentNewExamDialogComponent, { width: '400px' });
    ref.afterClosed().subscribe(data => {
      if (!data) return;
      this.service.createExamResult(data)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => { this.handleSuccess('Sınav sonucu kaydedildi'); this.reloadExams(); },
          error: e => this.handleError(e)
        });
    });
  }

  openLogStudySession(): void {
    const ref = this.dialog.open(LogStudySessionDialogComponent, {
      data: { subjects: this.subjects }, width: '420px'
    });
    ref.afterClosed().subscribe(data => {
      if (!data) return;
      this.service.createStudySession(data)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => { this.handleSuccess('Çalışma seansı kaydedildi'); this.reloadStudySessions(); },
          error: e => this.handleError(e)
        });
    });
  }

  openRating(session: CoachingSession): void {
    const ref = this.dialog.open(SessionRatingDialogComponent, {
      data: { scheduledAt: session.scheduledAt }, width: '360px'
    });
    ref.afterClosed().subscribe(data => {
      if (!data) return;
      this.service.updateCoachingSession(session.id, data)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => { this.handleSuccess('Değerlendirme kaydedildi'); this.reloadSessions(); },
          error: e => this.handleError(e)
        });
    });
  }

  private reloadGoals(): void {
    this.service.getGoals({ page: 1, pageSize: 50 })
      .pipe(takeUntil(this.destroy$))
      .subscribe(r => this.goals = r.items);
  }

  private reloadAssignments(): void {
    this.service.getAssignments({ page: 1, pageSize: 20 })
      .pipe(takeUntil(this.destroy$))
      .subscribe(r => this.assignments = r.items);
  }

  private reloadSessions(): void {
    this.service.getCoachingSessions({ page: 1, pageSize: 20 })
      .pipe(takeUntil(this.destroy$))
      .subscribe(r => this.sessions = r.items);
  }

  private reloadExams(): void {
    this.service.getExamResults({ page: 1, pageSize: 20 })
      .pipe(takeUntil(this.destroy$))
      .subscribe(r => this.examResults = r.items);
  }

  private reloadStudySessions(): void {
    this.service.getStudySessions({ page: 1, pageSize: 30 })
      .pipe(takeUntil(this.destroy$))
      .subscribe(r => {
        this.studySessions = r.items;
        this.computeReviewReminders();
      });
  }

  get activeGoals(): number { return this.goals.filter(g => g.status === 'Active').length; }
  get pendingAssignments(): number { return this.assignments.filter(a => a.status === 'Pending').length; }
  get upcomingSession(): CoachingSession | null {
    return this.sessions.find(s => s.status === 'Scheduled') ?? null;
  }
  get totalStudyMinutesThisWeek(): number {
    const weekAgo = new Date(); weekAgo.setDate(weekAgo.getDate() - 7);
    return this.studySessions
      .filter(s => new Date(s.date) >= weekAgo)
      .reduce((sum, s) => sum + s.actualMinutes, 0);
  }
  getSubjectName(subjectId: string): string {
    return this.subjects.find(s => s.id === subjectId)?.name ?? '—';
  }
  isOverdue(a: Assignment): boolean {
    return a.status === 'Pending' && !!a.dueDate && new Date(a.dueDate) < new Date();
  }
}
