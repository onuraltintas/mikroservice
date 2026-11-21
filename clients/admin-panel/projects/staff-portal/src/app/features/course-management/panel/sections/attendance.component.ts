import { Component, DestroyRef, computed, effect, inject, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CourseAssignment, CourseLesson, CourseManagementService } from '../../course-management.service';
import { CompletionMark, CoursePanelApi, PanelCompletion } from '../panel-api.service';
import { CoursePanelState, formatDateTime, formatTime, problemText, readHashQuery, writeHashQuery } from '../panel-state';
import { PanelConfirm } from '../panel-confirm';
import { HomeworkFilesComponent } from './homework-files.component';
import { MakeupCreditsComponent } from './makeup-credits.component';

const DAY_MS = 86_400_000;
const ATTENDANCE = [
  { value: 1, key: 'Present', label: 'Geldi', tone: 'ok' },
  { value: 2, key: 'Absent', label: 'Gelmedi', tone: 'bad' },
  { value: 4, key: 'Late', label: 'Geç', tone: 'warn' },
  { value: 3, key: 'Excused', label: 'İzinli', tone: 'soft' }
] as const;
const HOMEWORK: { value: CompletionMark; key: PanelCompletion['completion']; label: string; tone: string }[] = [
  { value: 1, key: 'Done', label: 'Yaptı', tone: 'ok' },
  { value: 2, key: 'Partial', label: 'Eksik', tone: 'warn' },
  { value: 3, key: 'NotDone', label: 'Yapmadı', tone: 'bad' }
];

/**
 * Yoklama ve ödev: pick a lesson (today's, or one still waiting), mark each student with one tap, mark last lesson's
 * homework, and give the next one. Built for a phone in class: big targets, one screen, no files.
 */
@Component({
  selector: 'course-attendance-section',
  standalone: true,
  imports: [FormsModule, HomeworkFilesComponent, MakeupCreditsComponent],
  styleUrl: '../panel.scss',
  template: `
    <header class="page-head">
      <div><h1>Yoklama ve ödev</h1><p>Derste her öğrenci için bir dokunuş.</p></div>
      <label class="field" style="min-width: 260px">Ders<select [ngModel]="lessonId()" (ngModelChange)="choose($event)">
        @for (lesson of choices(); track lesson.id) { <option [value]="lesson.id">{{ lessonTitle(lesson) }}</option> }
        @if (!choices().length) { <option value="">Son 7 günde ders yok</option> }</select></label>
    </header>
    @if (notice()) { <p class="notice" role="status">{{ notice() }}</p> }
    @if (error()) { <p class="alert" role="alert">{{ error() }}</p> }
    @if (lesson(); as current) {
      <div class="stack">
        <section class="card" aria-labelledby="attendance-title">
          <div class="card-head">
            <div><h2 id="attendance-title">{{ current.groupName ?? current.courseName }}</h2>
              <p class="muted" style="margin: 2px 0 0">{{ current.courseName }}, {{ dateTime(current.startsAt) }}–{{ time(current.endsAt) }}{{ current.roomName ? ', ' + current.roomName : '' }}</p></div>
            <span class="badge" [class.ok]="markedCount(current) === activeStudents(current).length">{{ markedCount(current) }} / {{ activeStudents(current).length }} işaretlendi</span>
          </div>
          @if (!started(current)) { <p class="banner" style="margin: 0 20px 16px">Ders henüz başlamadı. Yoklama ders saatinde açılır.</p> }
          @if (current.status !== 'Scheduled') { <p class="banner" style="margin: 0 20px 16px">Ders {{ current.status === 'Completed' ? 'tamamlandı' : 'iptal edildi' }}; yoklama değiştirilemez.</p> }
          <ul class="people">
            @for (student of activeStudents(current); track student.studentId) {
              <li>
                <span class="name">{{ student.displayName }}</span>
                <div class="segmented" role="radiogroup" [attr.aria-label]="student.displayName + ' yoklama'">
                  @for (option of attendance; track option.value) {
                    <button type="button" role="radio" [class]="option.tone" [attr.aria-checked]="student.attendanceStatus === option.key"
                      [disabled]="!editable(current) || busy()" (click)="mark(current, student.studentId, option.value)">{{ option.label }}</button>
                  }
                </div>
              </li>
            } @empty { <li class="empty">Bu derste öğrenci yok.</li> }
          </ul>
          @if (editable(current)) {
            <div class="actions" style="padding: 12px 20px 18px">
              <button type="button" class="btn-secondary" (click)="restPresent(current)" [disabled]="busy() || markedCount(current) === activeStudents(current).length">Kalanlar geldi</button>
              <button type="button" class="btn" (click)="complete(current)" [disabled]="busy() || !ended(current) || markedCount(current) < activeStudents(current).length">Dersi tamamla</button>
              @if (!ended(current)) { <span class="muted" style="font-size: 14px">Ders bitince ({{ time(current.endsAt) }}) tamamlayabilirsiniz.</span> }
              @else if (markedCount(current) < activeStudents(current).length) { <span class="muted" style="font-size: 14px">Tamamlamak için bütün öğrencileri işaretleyin.</span> }
            </div>
          }
        </section>

        <section class="card" aria-labelledby="homework-check-title">
          <div class="card-head"><h2 id="homework-check-title">{{ previousHomework() ? 'Ödev: ' + previousHomework()!.title : 'Ödev kontrolü' }}</h2>
            @if (previousHomework(); as homework) { <span class="muted num">Teslim {{ dateTime(homework.dueAt) }}</span> }</div>
          @if (previousHomework()) {
            <ul class="people">
              @for (row of completions(); track row.studentId) {
                <li>
                  <span class="name">{{ row.studentName }}</span>
                  <div class="segmented" role="radiogroup" [attr.aria-label]="row.studentName + ' ödev'">
                    @for (option of homework; track option.value) {
                      <button type="button" role="radio" [class]="option.tone" [attr.aria-checked]="row.completion === option.key"
                        [disabled]="state.readOnly() || busy()" (click)="markHomework(row, option.value)">{{ option.label }}</button>
                    }
                  </div>
                </li>
              }
            </ul>
            <p class="muted" style="margin: 0; padding: 0 20px 16px; font-size: 14px">Kurum açtıysa "yapmadı" ve "eksik" işaretleri velilere e-postayla bildirilir.</p>
            <div style="padding: 0 20px 16px">
              <button type="button" class="btn-quiet" [attr.aria-expanded]="filesFor() === previousHomework()!.id" (click)="toggleFiles(previousHomework()!.id)">
                {{ filesFor() === previousHomework()!.id ? 'Dosyaları ve teslimleri gizle' : 'Dosyalar ve öğrenci teslimleri' }}</button>
              @if (filesFor() === previousHomework()!.id) { <course-homework-files [assignment]="previousHomework()!" /> }
            </div>
          } @else { <p class="empty">Bu grup için kontrol edilecek ödev yok.</p> }
        </section>

        @if (!state.readOnly()) {
          <form class="card card-pad stack" style="gap: 14px" (ngSubmit)="giveHomework(current)" aria-labelledby="homework-new-title">
            <h2 id="homework-new-title">Bu dersin ödevi</h2>
            @if (givenHomework().length && !editingHomework()) {
              <ul class="given" aria-label="Bu derste verilen ödev">
                @for (item of givenHomework(); track item.id) {
                  <li><div><strong>{{ item.title }}</strong><span class="muted num"> Teslim {{ dateTime(item.dueAt) }}</span>
                    @if (item.instructions) { <p class="muted" style="margin: 2px 0 0">{{ item.instructions }}</p> }</div>
                    <div class="actions">
                      <button type="button" class="btn-secondary btn-small" (click)="editHomework(item)" [disabled]="busy()">Düzelt</button>
                      <button type="button" class="btn-quiet" [attr.aria-expanded]="filesFor() === item.id" (click)="toggleFiles(item.id)">Dosyalar</button>
                      <button type="button" class="btn-quiet" (click)="removeHomework(item)" [disabled]="busy()">Geri al</button>
                    </div>
                    @if (filesFor() === item.id) { <div style="flex-basis: 100%"><course-homework-files [assignment]="item" /></div> }</li>
                }
              </ul>
            }
            <div class="form-grid">
              <label class="field">Ödev<input name="title" [(ngModel)]="title" maxlength="160" placeholder="Kesirler, sayfa 46–50"></label>
              <label class="field">Teslim<input name="due" type="date" [(ngModel)]="dueOn"></label>
            </div>
            <label class="field">Açıklama<textarea name="instructions" [(ngModel)]="instructions" maxlength="4000" placeholder="İsteğe bağlı"></textarea></label>
            <p class="muted" style="margin: 0; font-size: 14px">Ödevi bir sonraki derste bu ekrandan "yaptı, eksik, yapmadı" diye işaretlersiniz.</p>
            <div class="actions">
              <button type="submit" class="btn" [disabled]="busy() || !title.trim() || !dueOn">{{ editingHomework() ? 'Düzeltmeyi kaydet' : givenHomework().length ? 'Bir ödev daha ver' : 'Ödevi kaydet' }}</button>
              @if (editingHomework()) { <button type="button" class="btn-secondary" (click)="cancelHomeworkEdit()">Vazgeç</button> }
            </div>
          </form>
        }
      </div>
    } @else if (!error()) { <p class="empty">Son yedi günde ve bugün ders yok. Takvimden ders planlayabilirsiniz.</p> }
    <div style="margin-top: 18px"><course-makeup-credits /></div>
  `,
  styles: `
    .people { list-style: none; margin: 0; padding: 0; }
    .people li { display: flex; flex-wrap: wrap; justify-content: space-between; align-items: center; gap: 10px; padding: 12px 20px; border-top: 1px solid var(--ds-line); }
    .name { font-weight: 600; min-width: 160px; }
    .given { list-style: none; margin: 0; padding: 0; }
    .given li { display: flex; flex-wrap: wrap; justify-content: space-between; align-items: center; gap: 10px; padding: 10px 0; border-top: 1px solid var(--ds-line); }
    .segmented button { min-width: 72px; min-height: 44px; }
    @media (max-width: 600px) { .people li { flex-direction: column; align-items: stretch; } .segmented { display: grid; grid-template-columns: repeat(4, 1fr); } }
  `
})
export class AttendanceSectionComponent {
  readonly state = inject(CoursePanelState);
  private readonly api = inject(CoursePanelApi);
  private readonly service = inject(CourseManagementService);
  readonly attendance = ATTENDANCE;
  readonly homework = HOMEWORK;
  readonly time = formatTime;
  readonly dateTime = formatDateTime;
  readonly lessons = signal<CourseLesson[]>([]);
  readonly lessonId = signal('');
  readonly assignments = signal<CourseAssignment[]>([]);
  readonly completions = signal<PanelCompletion[]>([]);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly notice = signal('');
  title = ''; instructions = ''; dueOn = '';
  /** Homework of this lesson being corrected in the form below. */
  readonly editingHomework = signal<CourseAssignment | null>(null);
  /** The homework whose files and answers are open (moved from the earlier screen, 21.11.2026). */
  readonly filesFor = signal('');
  toggleFiles(id: string): void { this.filesFor.set(this.filesFor() === id ? '' : id); }
  private readonly confirm = inject(PanelConfirm);
  /** Clock for "started" and "ended": the screen opens attendance and completion on time without a reload. */
  readonly now = signal(Date.now());
  /** Homework given for this lesson: each homework belongs to the nearest lesson of its group (or student),
   * so homework from the 18:00 lesson is not shown under the 09:00 lesson of the same day (20.11.2026 live test). */
  readonly givenHomework = computed(() => {
    const current = this.lesson();
    if (!current) return [];
    return this.assignments().filter(item => this.homeworkLesson(item)?.id === current.id)
      .sort((a, b) => a.createdAt.localeCompare(b.createdAt));
  });

  readonly choices = computed(() => {
    const now = Date.now();
    // A cancelled lesson has no attendance to take (20.11.2026 live test: it was offered in the list).
    return this.lessons().filter(lesson => lesson.status !== 'Cancelled' && new Date(lesson.startsAt).getTime() <= now + 12 * 3_600_000)
      .sort((a, b) => b.startsAt.localeCompare(a.startsAt));
  });
  readonly lesson = computed(() => this.lessons().find(item => item.id === this.lessonId()) ?? null);
  /** The latest homework of this lesson's group (or student) given before this lesson started. */
  readonly previousHomework = computed(() => {
    const current = this.lesson();
    if (!current) return null;
    const studentIds = new Set(current.students.map(item => item.studentId));
    return this.assignments().filter(item => item.courseId === current.courseId
        && (current.groupId ? item.groupId === current.groupId : !!item.studentId && studentIds.has(item.studentId))
        && new Date(item.createdAt) < new Date(current.startsAt))
      .sort((a, b) => b.createdAt.localeCompare(a.createdAt))[0] ?? null;
  });

  constructor() {
    const timer = setInterval(() => this.now.set(Date.now()), 20_000);
    inject(DestroyRef).onDestroy(() => clearInterval(timer));
    const wanted = readHashQuery().get('ders');
    effect(() => { this.state.id(); this.state.version(); untracked(() => void this.load(wanted)); });
    effect(() => { const homework = this.previousHomework(); untracked(() => void this.loadCompletions(homework)); });
  }

  async load(wanted: string | null): Promise<void> {
    const id = this.state.id();
    if (!id) return;
    try {
      const now = Date.now();
      const [lessons, assignments] = await Promise.all([
        this.api.lessons(id, new Date(now - 7 * DAY_MS), new Date(now + DAY_MS)),
        this.service.getAssignments(id)
      ]);
      const mine = this.state.isOwner() ? lessons : lessons.filter(item => item.teacherUserId === this.state.myUserId());
      this.lessons.set(mine);
      this.assignments.set(assignments);
      const keep = this.lessonId() || wanted;
      const pick = mine.find(item => item.id === keep) ?? this.bestLesson();
      this.lessonId.set(pick?.id ?? '');
      this.error.set('');
    } catch (error) { this.error.set(problemText(error, 'Dersler yüklenemedi.')); }
  }

  /** Now running, else the latest one that ended without attendance, else the latest. */
  private bestLesson(): CourseLesson | undefined {
    const now = Date.now();
    const list = this.choices();
    return list.find(item => item.status === 'Scheduled' && new Date(item.startsAt).getTime() <= now && new Date(item.endsAt).getTime() > now)
      ?? list.find(item => item.status === 'Scheduled' && new Date(item.endsAt).getTime() <= now && item.students.some(s => !s.attendanceStatus))
      ?? list[0];
  }

  choose(id: string): void { this.lessonId.set(id); this.notice.set(''); this.error.set(''); writeHashQuery('yoklama', { ders: id }); }

  /** The lesson a homework was given in: same course and group (or student), given from two hours before the lesson
   * to twelve hours after it, and of those the lesson nearest to the moment it was given. */
  private homeworkLesson(item: CourseAssignment): CourseLesson | undefined {
    const given = new Date(item.createdAt).getTime();
    let best: CourseLesson | undefined; let bestGap = Infinity;
    for (const lesson of this.lessons()) {
      if (lesson.status === 'Cancelled' || lesson.courseId !== item.courseId) continue;
      const sameAudience = lesson.groupId ? item.groupId === lesson.groupId
        : !!item.studentId && lesson.students.some(student => student.studentId === item.studentId);
      if (!sameAudience) continue;
      const start = new Date(lesson.startsAt).getTime(); const end = new Date(lesson.endsAt).getTime();
      if (given < start - 2 * 3_600_000 || given > end + 12 * 3_600_000) continue;
      const gap = given < start ? start - given : given > end ? given - end : 0;
      if (gap < bestGap) { best = lesson; bestGap = gap; }
    }
    return best;
  }
  lessonTitle(lesson: CourseLesson): string { return `${formatDateTime(lesson.startsAt)}, ${lesson.groupName ?? lesson.courseName}`; }
  activeStudents(lesson: CourseLesson) { return lesson.students.filter(item => item.isActive !== false); }
  markedCount(lesson: CourseLesson): number { return this.activeStudents(lesson).filter(item => item.attendanceStatus).length; }
  started(lesson: CourseLesson): boolean { return new Date(lesson.startsAt).getTime() <= this.now(); }
  ended(lesson: CourseLesson): boolean { return new Date(lesson.endsAt).getTime() <= this.now(); }
  editable(lesson: CourseLesson): boolean { return lesson.status === 'Scheduled' && this.started(lesson) && !this.state.readOnly(); }

  private patch(lessonId: string, studentId: string, status: string): void {
    this.lessons.update(list => list.map(item => item.id !== lessonId ? item
      : { ...item, students: item.students.map(student => student.studentId === studentId ? { ...student, attendanceStatus: status } : student) }));
  }

  async mark(lesson: CourseLesson, studentId: string, value: number): Promise<void> {
    const key = ATTENDANCE.find(item => item.value === value)!.key;
    const before = lesson.students.find(item => item.studentId === studentId)?.attendanceStatus ?? null;
    this.patch(lesson.id, studentId, key);
    try { await this.service.markAttendance(this.state.id(), lesson.id, studentId, value); this.error.set(''); }
    catch (error) { this.patch(lesson.id, studentId, before as string); this.error.set(problemText(error)); }
  }

  async restPresent(lesson: CourseLesson): Promise<void> {
    this.busy.set(true);
    try {
      for (const student of this.activeStudents(lesson).filter(item => !item.attendanceStatus)) await this.mark(lesson, student.studentId, 1);
    } finally { this.busy.set(false); }
  }

  async complete(lesson: CourseLesson): Promise<void> {
    this.busy.set(true);
    try {
      await this.service.completeLesson(this.state.id(), lesson.id);
      this.lessons.update(list => list.map(item => item.id === lesson.id ? { ...item, status: 'Completed' } : item));
      this.notice.set('Ders tamamlandı.');
      this.state.changed();
    } catch (error) { this.error.set(problemText(error)); }
    finally { this.busy.set(false); }
  }

  private async loadCompletions(homework: CourseAssignment | null): Promise<void> {
    if (!homework) { this.completions.set([]); return; }
    try { this.completions.set(await this.api.completions(this.state.id(), homework.id)); }
    catch { this.completions.set([]); }
  }

  async markHomework(row: PanelCompletion, value: CompletionMark): Promise<void> {
    const homework = this.previousHomework();
    if (!homework) return;
    const key = HOMEWORK.find(item => item.value === value)!.key;
    const next: CompletionMark = row.completion === key ? 0 : value; // tapping the chosen mark again clears it
    try { this.completions.set(await this.api.markCompletions(this.state.id(), homework.id, [{ studentId: row.studentId, completion: next, note: null }])); }
    catch (error) { this.error.set(problemText(error)); }
  }

  editHomework(item: CourseAssignment): void {
    this.editingHomework.set(item);
    this.title = item.title;
    this.instructions = item.instructions ?? '';
    this.dueOn = isoDayTr(item.dueAt);
  }
  cancelHomeworkEdit(): void { this.editingHomework.set(null); this.title = ''; this.instructions = ''; this.dueOn = ''; }

  async removeHomework(item: CourseAssignment): Promise<void> {
    if (!await this.confirm.ask(`"${item.title}" ödevi geri alınsın mı? Öğrenciler bu ödevi artık görmez.`,
      { title: 'Ödev geri alınsın mı?', confirmText: 'Ödevi geri al', danger: true })) return;
    this.busy.set(true);
    try {
      await this.service.deleteAssignment(this.state.id(), item.id);
      this.notice.set('Ödev geri alındı.');
      this.assignments.set(await this.service.getAssignments(this.state.id()));
    } catch (error) { this.error.set(problemText(error)); }
    finally { this.busy.set(false); }
  }

  async giveHomework(lesson: CourseLesson): Promise<void> {
    this.busy.set(true);
    try {
      const editing = this.editingHomework();
      if (editing) {
        await this.service.reviseAssignment(this.state.id(), editing.id, { title: this.title.trim(),
          instructions: this.instructions.trim() || null, dueAt: new Date(`${this.dueOn}T18:00:00+03:00`).toISOString() });
        this.cancelHomeworkEdit();
        this.notice.set('Ödev düzeltildi.');
        this.assignments.set(await this.service.getAssignments(this.state.id()));
        return;
      }
      const single = !lesson.groupId && lesson.students.length === 1 ? lesson.students[0].studentId : null;
      await this.service.createAssignment(this.state.id(), { id: crypto.randomUUID(), courseId: lesson.courseId,
        groupId: lesson.groupId, studentId: lesson.groupId ? null : single, title: this.title.trim(),
        instructions: this.instructions.trim() || null, dueAt: new Date(`${this.dueOn}T18:00:00+03:00`).toISOString() });
      this.title = ''; this.instructions = ''; this.dueOn = '';
      this.notice.set('Ödev kaydedildi. Bir sonraki derste işaretleyebilirsiniz.');
      this.assignments.set(await this.service.getAssignments(this.state.id()));
    } catch (error) { this.error.set(problemText(error)); }
    finally { this.busy.set(false); }
  }
}

/** The Türkiye calendar day of an instant, as yyyy-MM-dd for a date field. */
function isoDayTr(value: string): string { return new Date(new Date(value).getTime() + 3 * 3_600_000).toISOString().slice(0, 10); }
