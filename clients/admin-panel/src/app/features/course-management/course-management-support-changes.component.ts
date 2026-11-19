import { Component, EventEmitter, Input, Output, computed, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Observable } from 'rxjs';
import { CourseManagementInstitutionsService, SupportContact, SupportSection } from './course-management-admin.service';

interface ContactRow { id: string; displayName: string; email: string | null; phoneNumber: string | null; }
interface StudentRow extends ContactRow { guardians?: ContactRow[]; }
interface AttendanceRow { studentId: string; displayName: string; status: string | null; charged: boolean; }
interface LessonRow { id: string; courseName: string; startsAt: string; endsAt: string; status: string; seriesId: string | null; attendance: AttendanceRow[]; }
interface EmailRuleRow { kind: string; enabled: boolean; audience: string; }

const ATTENDANCE = [{ value: 1, key: 'Present', label: 'Geldi' }, { value: 2, key: 'Absent', label: 'Gelmedi' },
  { value: 4, key: 'Late', label: 'Geç geldi' }, { value: 3, key: 'Excused', label: 'İzinli' }];
const EMAIL_KINDS: Record<string, string> = {
  Absence: 'Devamsızlık', HomeworkNotDone: 'Yapılmayan ödev', InstallmentReminder: 'Taksit hatırlatma',
  InstallmentOverdue: 'Geciken taksit', LessonChanged: 'Ders değişikliği', WeeklySchedule: 'Haftalık program', PaymentReceived: 'Ödeme alındı'
};
const PROBLEMS: Record<string, string> = {
  'SupportAccess.LessonCharged': 'Bu ders için ücret yazılmış; yoklamayı yalnız kurum değiştirebilir.',
  'Attendance.LessonNotStarted': 'Ders henüz başlamadı.',
  'Attendance.LessonClosed': 'Ders iptal edilmiş.',
  'Lesson.AttendanceStarted': 'Dersin yoklaması başlamış ya da ders geçmişte; saati değiştirilemez.',
  'Lesson.SeriesOccurrenceMustUseSeriesEditor': 'Tekrarlayan dersin saatini yalnız kurum değiştirebilir.',
  'Lesson.TeacherConflict': 'Öğretmenin o saatte başka dersi var.',
  'Lesson.StudentConflict': 'Öğrencilerden birinin o saatte başka dersi var.',
  'Room.Conflict': 'Derslik o saatte dolu.',
  'Availability.TimeOffConflict': 'Öğretmen o saatte izinli.',
  'Availability.OutsideWorkingHours': 'Öğretmenin çalışma saatleri dışında.',
  'Lesson.InvalidTime': 'Saat geçersiz: gelecekte olmalı ve en çok 8 saat sürmeli.'
};

/**
 * Corrections under a support grant the owner opened with corrections allowed (19.11.2026): contact details, attendance,
 * one lesson's time, switching an e-mail kind off. Each one is logged for the owner; students and guardians are not told.
 */
@Component({
  selector: 'app-course-management-support-changes',
  standalone: true,
  imports: [FormsModule],
  template: `
    <section class="changes" [attr.aria-label]="'Düzeltme: ' + title()">
      <p class="note">Kurum düzeltme izni verdi. Yaptığınız her değişiklik önce ve sonra değeriyle kurumun kaydına geçer; öğrenci ve velilere bildirim gitmez. Para kayıtları değiştirilemez.</p>
      @if (message()) { <p class="ok" role="status">{{ message() }}</p> }
      @if (problem()) { <p class="alert" role="alert">{{ problem() }}</p> }

      @switch (sectionName()) {
        @case ('Students') {
          @for (student of students(); track student.id) {
            <div class="item">
              <strong>{{ student.displayName }}</strong>
              <button type="button" (click)="edit('student', student)">Öğrenciyi düzelt</button>
              @for (guardian of student.guardians ?? []; track guardian.id) {
                <span class="sub">Veli: {{ guardian.displayName }} <button type="button" (click)="edit('guardian', guardian)">Veliyi düzelt</button></span>
              }
            </div>
          }
          @if (editing(); as form) {
            <form class="form" (ngSubmit)="saveContact()">
              <h5>{{ form.kind === 'student' ? 'Öğrenci' : 'Veli' }} iletişim bilgisi</h5>
              <label>Ad soyad<input name="name" [(ngModel)]="form.contact.displayName" maxlength="200" required></label>
              <label>E-posta<input name="email" type="email" [(ngModel)]="form.contact.email" maxlength="320"></label>
              <label>Telefon<input name="phone" type="tel" [(ngModel)]="form.contact.phoneNumber" maxlength="40"></label>
              <div class="row"><button type="submit" class="primary" [disabled]="busy() || !form.contact.displayName.trim()">Kaydet</button>
                <button type="button" (click)="editing.set(null)">Vazgeç</button></div>
            </form>
          }
        }
        @case ('Schedule') {
          @for (lesson of lessons(); track lesson.id) {
            <div class="item">
              <strong>{{ lesson.courseName }}, {{ when(lesson.startsAt) }}</strong>
              @if (lesson.status === 'Cancelled') { <span class="sub">İptal edilmiş</span> }
              @if (started(lesson) && lesson.status !== 'Cancelled') {
                @for (row of lesson.attendance; track row.studentId) {
                  <label class="sub">{{ row.displayName }}
                    <select [ngModel]="statusValue(row.status)" (ngModelChange)="fixAttendance(lesson, row, $event)" [disabled]="busy() || row.charged">
                      <option [ngValue]="0" disabled>Yoklama yok</option>
                      @for (item of attendance; track item.value) { <option [ngValue]="item.value">{{ item.label }}</option> }
                    </select>
                    @if (row.charged) { <span class="muted">ücret yazılmış</span> }
                  </label>
                }
              } @else if (lesson.status === 'Scheduled' && !lesson.seriesId) {
                <button type="button" (click)="startMove(lesson)">Saati düzelt</button>
              }
            </div>
          }
          @if (moving(); as move) {
            <form class="form" (ngSubmit)="saveMove()">
              <h5>{{ move.lesson.courseName }}: yeni saat (Türkiye saati)</h5>
              <label>Başlangıç<input name="start" type="datetime-local" [(ngModel)]="move.start" required></label>
              <label>Bitiş<input name="end" type="datetime-local" [(ngModel)]="move.end" required></label>
              <div class="row"><button type="submit" class="primary" [disabled]="busy()">Kaydet</button>
                <button type="button" (click)="moving.set(null)">Vazgeç</button></div>
            </form>
          }
        }
        @case ('Settings') {
          @for (rule of rules(); track rule.kind) {
            <div class="item"><strong>{{ kindLabel(rule.kind) }}</strong>
              @if (rule.enabled) { <button type="button" (click)="switchOff(rule)" [disabled]="busy()">Bu bildirimi kapat</button> }
              @else { <span class="muted">Kapalı</span> }
            </div>
          }
          <p class="muted">Bildirimi açmak kurumun işidir; açarken öğrenci ve velileri bilgilendirdiğini kendisi onaylar.</p>
        }
      }
    </section>
  `,
  styles: `
    .changes { margin-top: 16px; padding: 16px; border: 1px solid var(--ds-line, #E1E4E9); border-radius: 10px; }
    .note { margin: 0 0 12px; color: var(--ds-muted, #555F6D); }
    .item { display: flex; flex-wrap: wrap; gap: 8px 14px; align-items: center; padding: 10px 0; border-top: 1px solid var(--ds-line, #E1E4E9); }
    .sub { display: inline-flex; gap: 8px; align-items: center; }
    .muted { color: var(--ds-muted, #555F6D); font-size: 14px; }
    .form { display: grid; gap: 10px; max-width: 480px; margin-top: 14px; }
    .form label { display: flex; flex-direction: column; gap: 4px; font-size: 14px; }
    h5 { margin: 0; font-size: 15px; }
    .row { display: flex; gap: 8px; }
    input, select { border: 1px solid var(--ds-field, #7A8492); border-radius: 8px; padding: 8px 10px; font: inherit; color: inherit; background: var(--ds-surface, #fff); min-height: 44px; }
    button { min-height: 44px; border: 1px solid var(--ds-field, #7A8492); border-radius: 8px; background: var(--ds-surface, #fff); color: inherit; padding: 0 14px; font: inherit; cursor: pointer; }
    button.primary { background: var(--ds-primary, #1C2430); color: var(--ds-on-primary, #fff); border-color: var(--ds-primary, #1C2430); }
    button:disabled, select:disabled { opacity: .6; cursor: not-allowed; }
    button:focus-visible, input:focus-visible, select:focus-visible { outline: 3px solid var(--ds-focus, #1C2430); outline-offset: 2px; }
    .ok { color: var(--ds-success, #146C43); } .alert { color: var(--ds-danger, #B42318); }
  `
})
export class CourseManagementSupportChangesComponent {
  private readonly service = inject(CourseManagementInstitutionsService);
  @Input({ required: true }) workspaceId!: string;
  readonly sectionName = input.required<SupportSection>({ alias: 'section' });
  readonly data = input<unknown>(null);
  /** Asks the parent to read the section again after a change. */
  @Output() changed = new EventEmitter<void>();

  readonly attendance = ATTENDANCE;
  readonly students = computed(() => this.sectionName() === 'Students' && Array.isArray(this.data()) ? this.data() as StudentRow[] : []);
  readonly lessons = computed(() => this.sectionName() === 'Schedule' && Array.isArray(this.data()) ? this.data() as LessonRow[] : []);
  readonly rules = computed(() => {
    const rules = (this.data() as { emailRules?: EmailRuleRow[] } | null)?.emailRules;
    return this.sectionName() === 'Settings' && Array.isArray(rules) ? rules : [];
  });
  readonly editing = signal<{ kind: 'student' | 'guardian'; id: string; contact: SupportContact } | null>(null);
  readonly moving = signal<{ lesson: LessonRow; start: string; end: string } | null>(null);
  readonly busy = signal(false);
  readonly message = signal('');
  readonly problem = signal('');

  title(): string { return { Students: 'öğrenciler', Schedule: 'takvim ve yoklama', Finance: 'tahsilat', Settings: 'ayarlar' }[this.sectionName()]; }
  kindLabel(kind: string): string { return EMAIL_KINDS[kind] ?? kind; }
  statusValue(status: string | null): number { return ATTENDANCE.find(item => item.key === status)?.value ?? 0; }
  started(lesson: LessonRow): boolean { return new Date(lesson.startsAt).getTime() <= Date.now(); }
  when(value: string): string {
    return new Intl.DateTimeFormat('tr-TR', { dateStyle: 'medium', timeStyle: 'short', timeZone: 'Europe/Istanbul' }).format(new Date(value));
  }

  edit(kind: 'student' | 'guardian', row: ContactRow): void {
    this.moving.set(null);
    this.editing.set({ kind, id: row.id, contact: { displayName: row.displayName, email: row.email, phoneNumber: row.phoneNumber } });
  }

  saveContact(): void {
    const form = this.editing();
    if (!form) return;
    const contact = { displayName: form.contact.displayName.trim(), email: form.contact.email?.trim() || null,
      phoneNumber: form.contact.phoneNumber?.trim() || null };
    this.run(form.kind === 'student' ? this.service.fixStudent(this.workspaceId, form.id, contact)
      : this.service.fixGuardian(this.workspaceId, form.id, contact), 'İletişim bilgisi düzeltildi.', () => this.editing.set(null));
  }

  fixAttendance(lesson: LessonRow, row: AttendanceRow, status: number): void {
    this.run(this.service.fixAttendance(this.workspaceId, lesson.id, row.studentId, status), `${row.displayName} için yoklama düzeltildi.`);
  }

  startMove(lesson: LessonRow): void {
    this.editing.set(null);
    this.moving.set({ lesson, start: local(lesson.startsAt), end: local(lesson.endsAt) });
  }

  saveMove(): void {
    const move = this.moving();
    if (!move?.start || !move.end) return;
    this.run(this.service.moveLesson(this.workspaceId, move.lesson.id, `${move.start}:00+03:00`, `${move.end}:00+03:00`),
      'Dersin saati düzeltildi.', () => this.moving.set(null));
  }

  switchOff(rule: EmailRuleRow): void {
    this.run(this.service.switchEmailOff(this.workspaceId, rule.kind), `${this.kindLabel(rule.kind)} bildirimi kapatıldı.`);
  }

  private run(request: Observable<unknown>, done: string, after?: () => void): void {
    this.busy.set(true); this.message.set(''); this.problem.set('');
    request.subscribe({
      next: () => { this.busy.set(false); this.message.set(done); after?.(); this.changed.emit(); },
      error: response => {
        this.busy.set(false);
        const code = response?.error?.code as string | undefined;
        this.problem.set((code && PROBLEMS[code]) ?? (response?.status === 404
          ? 'Düzeltme yapılamadı: erişim kapanmış, bu bölümde düzeltme izni yok ya da oturumunuz iki adımlı doğrulamayla açılmamış.'
          : 'Düzeltme kaydedilemedi. Bilgileri kontrol edip yeniden deneyin.'));
      }
    });
  }
}

/** An instant as a Türkiye-time datetime-local value. */
function local(value: string): string {
  const parts = new Intl.DateTimeFormat('sv-SE', { timeZone: 'Europe/Istanbul', year: 'numeric', month: '2-digit', day: '2-digit',
    hour: '2-digit', minute: '2-digit', hour12: false }).format(new Date(value));
  return parts.replace(' ', 'T');
}
