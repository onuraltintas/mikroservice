import { Component, EventEmitter, HostListener, Output, computed, effect, inject, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CourseLesson, CourseManagementService } from '../../course-management.service';
import { CoursePanelApi, PanelStudentRow, SeriesPreview, SeriesRequest } from '../panel-api.service';
import { CoursePanelState, PanelSection, formatDateTime, formatTime, problemText, readHashQuery, writeHashQuery } from '../panel-state';

const DAY_MS = 86_400_000;
const TR_OFFSET = 3 * 3_600_000; // Türkiye has been UTC+3 all year since 2016.
const FIRST_HOUR = 8;
const LAST_HOUR = 22;
const HOUR_PX = 56;
const WEEKDAYS = ['Pazartesi', 'Salı', 'Çarşamba', 'Perşembe', 'Cuma', 'Cumartesi', 'Pazar'];
const MONTHS = ['Ocak', 'Şubat', 'Mart', 'Nisan', 'Mayıs', 'Haziran', 'Temmuz', 'Ağustos', 'Eylül', 'Ekim', 'Kasım', 'Aralık'];
const pad = (value: number) => String(value).padStart(2, '0');

/** A calendar day as YYYY-MM-DD (Türkiye), and the instant of a Türkiye wall clock time on it. */
const isoDay = (ms: number) => { const d = new Date(ms + TR_OFFSET); return `${d.getUTCFullYear()}-${pad(d.getUTCMonth() + 1)}-${pad(d.getUTCDate())}`; };
const instant = (day: string, time: string) => new Date(`${day}T${time}:00+03:00`);
const mondayOf = (ms: number) => { const d = new Date(ms + TR_OFFSET); const back = (d.getUTCDay() + 6) % 7;
  return Date.UTC(d.getUTCFullYear(), d.getUTCMonth(), d.getUTCDate() - back) - TR_OFFSET; };

type Menu = { x: number; y: number; day: string; time: string; lesson: CourseLesson | null };
type Editor = {
  kind: 'single' | 'series' | 'timeoff'; courseId: string; target: 'group' | 'student'; groupId: string; studentId: string; studentName: string;
  teacherUserId: string; roomId: string; day: string; time: string; duration: number; days: boolean[];
  repeat: 'weekly' | 'biweekly' | 'monthly'; monthWeek: number; span: 'month' | 'quarter' | 'term' | 'year' | 'custom'; endsOn: string;
  endTime: string; reason: string;
};

/**
 * Takvim: a week at a glance. Click an empty hour to add a lesson there; right-click (or the menu key) for "Ders ekle",
 * "Tekrarlayan ders ekle" and "Öğretmen izni ekle"; right-click a lesson to take attendance, move or cancel it.
 * A repeating lesson can run a month, a term or a school year, every week, every other week or once a month; its clashes
 * are listed before saving and can be skipped.
 */
@Component({
  selector: 'course-calendar-section',
  standalone: true,
  imports: [FormsModule],
  styleUrl: '../panel.scss',
  template: `
    <header class="page-head">
      <div><h1>Takvim</h1><p>{{ weekLabel() }}</p></div>
      <div class="actions">
        <button type="button" class="btn-secondary" (click)="shift(-7)">Önceki hafta</button>
        <button type="button" class="btn-secondary" (click)="today()">Bu hafta</button>
        <button type="button" class="btn-secondary" (click)="shift(7)">Sonraki hafta</button>
        @if (!state.readOnly()) { <button type="button" class="btn" (click)="openEditor('single', todayIso(), nextHour())">Ders planla</button> }
      </div>
    </header>
    @if (notice()) { <p class="notice" role="status">{{ notice() }}</p> }
    @if (error()) { <p class="alert" role="alert">{{ error() }}</p> }
    @if (state.isOwner()) {
      <div class="segmented" role="group" aria-label="Kimin takvimi" style="margin-bottom: 14px">
        <button type="button" [attr.aria-pressed]="!teacherFilter && !roomFilter" (click)="setFilter('', '')">Tüm kurum</button>
        @for (teacher of state.teachers(); track teacher.userId) {
          <button type="button" [attr.aria-pressed]="teacherFilter === teacher.userId" (click)="setFilter(teacher.userId, '')">{{ state.teacherName(teacher.userId) }}</button>
        }
        @for (room of state.rooms(); track room.id) {
          <button type="button" [attr.aria-pressed]="roomFilter === room.id" (click)="setFilter('', room.id)">{{ room.name }}</button>
        }
      </div>
    }
    <p class="muted hint-line">Boş bir saate tıklayın ya da sağ tıklayın; derslere sağ tıklayınca yoklama, taşıma ve iptal seçenekleri çıkar.</p>
    <section class="card cal-wrap" aria-label="Haftalık takvim">
      <div class="cal">
        <div class="corner"></div>
        @for (day of days(); track day.iso) {
          <div class="day-head" [class.is-today]="day.iso === todayIso()">{{ day.label }}</div>
        }
        <div class="times">@for (hour of hours; track hour) { <div class="time num">{{ pad(hour) }}.00</div> }</div>
        @for (day of days(); track day.iso) {
          <div class="day-col" [class.is-today]="day.iso === todayIso()">
            @for (hour of hours; track hour) {
              <button type="button" class="slot" [disabled]="state.readOnly()"
                [attr.aria-label]="day.long + ' ' + pad(hour) + '.00, ders ekle'"
                (click)="openEditor('single', day.iso, pad(hour) + ':00')"
                (contextmenu)="openMenu($event, day.iso, pad(hour) + ':00', null)"></button>
            }
            @for (lesson of lessonsOn(day.iso); track lesson.id) {
              <button type="button" class="lesson" [class]="'lesson tone-' + tone(lesson)" [class.cancelled]="lesson.status === 'Cancelled'"
                [style.top.px]="top(lesson)" [style.height.px]="height(lesson)"
                [attr.aria-label]="lessonLabel(lesson)"
                (click)="openMenu($event, day.iso, '', lesson)" (contextmenu)="openMenu($event, day.iso, '', lesson)">
                <strong>{{ lesson.groupName ?? studentNames(lesson) }}</strong>
                <span>{{ time(lesson.startsAt) }}–{{ time(lesson.endsAt) }}{{ lesson.roomName ? ', ' + lesson.roomName : '' }}</span>
              </button>
            }
          </div>
        }
      </div>
    </section>

    @if (menu(); as at) {
      <div class="menu card" role="menu" [style.left.px]="at.x" [style.top.px]="at.y" (keydown.escape)="menu.set(null)">
        @if (at.lesson; as lesson) {
          <p class="menu-title">{{ lesson.courseName }}{{ lesson.groupName ? ', ' + lesson.groupName : '' }}<br>
            <span class="muted">{{ dateTime(lesson.startsAt) }}, {{ state.teacherName(lesson.teacherUserId) }}</span></p>
          @if (lesson.status === 'Scheduled') {
            <button type="button" role="menuitem" (click)="takeAttendance()">Yoklama al</button>
            @if (!state.readOnly() && !lesson.seriesId && isFuture(lesson)) { <button type="button" role="menuitem" (click)="openMove(lesson)">Taşı</button> }
            @if (!state.readOnly() && isFuture(lesson)) { <button type="button" role="menuitem" (click)="cancel(lesson)">Bu dersi iptal et</button> }
            @if (!state.readOnly() && lesson.seriesId && isFuture(lesson)) { <button type="button" role="menuitem" (click)="cancelSeries(lesson)">Serinin kalan derslerini iptal et</button> }
          } @else { <p class="muted menu-title">{{ lesson.status === 'Cancelled' ? 'İptal edildi' : 'Tamamlandı' }}</p> }
        } @else {
          <p class="menu-title muted">{{ dayLong(at.day) }} {{ at.time.replace(':', '.') }}</p>
          <button type="button" role="menuitem" (click)="openEditor('single', at.day, at.time)">Ders ekle</button>
          <button type="button" role="menuitem" (click)="openEditor('series', at.day, at.time)">Tekrarlayan ders ekle</button>
          <button type="button" role="menuitem" (click)="openEditor('timeoff', at.day, at.time)">Öğretmen izni ekle</button>
        }
      </div>
    }

    @if (editor(); as form) {
      <div class="dialog-backdrop" (click)="closeEditor()">
        <section class="dialog" role="dialog" aria-modal="true" aria-labelledby="lesson-editor-title" (click)="$event.stopPropagation()" (keydown.escape)="closeEditor()">
          <h2 id="lesson-editor-title">{{ form.kind === 'timeoff' ? 'Öğretmen izni ekle' : form.kind === 'series' ? 'Tekrarlayan ders ekle' : moving() ? 'Dersi taşı' : 'Ders ekle' }}</h2>
          @if (form.kind !== 'timeoff' && !moving()) {
            <div class="tabs" role="tablist" aria-label="Ders türü">
              <button type="button" role="tab" [attr.aria-selected]="form.kind === 'single'" (click)="form.kind = 'single'; preview.set(null)">Tek ders</button>
              <button type="button" role="tab" [attr.aria-selected]="form.kind === 'series'" (click)="form.kind = 'series'; preview.set(null)">Tekrarlayan</button>
            </div>
          }
          @if (editorError()) { <p class="alert" role="alert">{{ editorError() }}</p> }
          <div class="form-grid">
            @if (form.kind !== 'timeoff' && !moving()) {
              <label class="field">Kurs<select [(ngModel)]="form.courseId" (ngModelChange)="form.groupId = ''; preview.set(null)"><option value="">Kurs seçin</option>
                @for (course of state.courses(); track course.id) { <option [value]="course.id">{{ course.name }}</option> }</select></label>
              <label class="field">Kimlere<select [(ngModel)]="form.target" (ngModelChange)="preview.set(null)"><option value="group">Bir gruba</option><option value="student">Tek öğrenciye (birebir)</option></select></label>
              @if (form.target === 'group') {
                <label class="field">Grup<select [(ngModel)]="form.groupId" (ngModelChange)="groupPicked(form)"><option value="">Grup seçin</option>
                  @for (group of groupsFor(form.courseId); track group.id) { <option [value]="group.id">{{ group.name }}</option> }</select></label>
              } @else {
                <label class="field">Öğrenci<input [(ngModel)]="form.studentName" (ngModelChange)="findStudents($event)" list="lesson-students" placeholder="Adını yazın" autocomplete="off">
                  <datalist id="lesson-students">@for (student of studentOptions(); track student.id) { <option [value]="student.displayName"></option> }</datalist></label>
              }
            }
            @if (state.isOwner() && !moving()) {
              <label class="field">Öğretmen<select [(ngModel)]="form.teacherUserId" (ngModelChange)="preview.set(null)">
                @for (teacher of state.teachers(); track teacher.userId) { <option [value]="teacher.userId">{{ state.teacherName(teacher.userId) }}</option> }</select></label>
            }
            @if (form.kind !== 'timeoff') {
              <label class="field">Derslik<select [(ngModel)]="form.roomId" (ngModelChange)="preview.set(null)"><option value="">Seçilmedi</option>
                @for (room of state.rooms(); track room.id) { <option [value]="room.id">{{ room.branchName }}, {{ room.name }}</option> }</select></label>
            }
            <label class="field">{{ form.kind === 'series' ? 'Başlangıç günü' : 'Gün' }}<input type="date" [(ngModel)]="form.day" (ngModelChange)="dayChanged(form)"></label>
            <label class="field">{{ form.kind === 'timeoff' ? 'Başlangıç saati' : 'Saat' }}<input type="time" step="300" [(ngModel)]="form.time" (ngModelChange)="preview.set(null)"></label>
            @if (form.kind === 'timeoff') {
              <label class="field">Bitiş saati<input type="time" step="300" [(ngModel)]="form.endTime"></label>
              <label class="field">Açıklama<input [(ngModel)]="form.reason" maxlength="200" placeholder="İsteğe bağlı"></label>
            } @else {
              <label class="field">Süre<select [(ngModel)]="form.duration" (ngModelChange)="preview.set(null)">
                @for (minutes of durations; track minutes) { <option [ngValue]="minutes">{{ minutesLabel(minutes) }}</option> }</select></label>
            }
          </div>
          @if (form.kind === 'series') {
            <fieldset class="days"><legend>Günler</legend>
              @for (name of weekdays; track name; let i = $index) {
                <label class="check"><input type="checkbox" [(ngModel)]="form.days[i]" (ngModelChange)="preview.set(null)" [disabled]="form.repeat === 'monthly' && !form.days[i] && checkedDays(form) >= 1">{{ name }}</label>
              }
            </fieldset>
            <div class="form-grid">
              <label class="field">Tekrar<select [(ngModel)]="form.repeat" (ngModelChange)="repeatChanged(form)">
                <option value="weekly">Her hafta</option><option value="biweekly">İki haftada bir</option><option value="monthly">Ayda bir</option></select></label>
              @if (form.repeat === 'monthly') {
                <label class="field">Ayın kaçıncı haftası<select [(ngModel)]="form.monthWeek" (ngModelChange)="preview.set(null)">
                  <option [ngValue]="1">İlk</option><option [ngValue]="2">İkinci</option><option [ngValue]="3">Üçüncü</option><option [ngValue]="4">Dördüncü</option><option [ngValue]="5">Son</option></select>
                  <span class="hint">Örneğin her ayın ilk {{ firstCheckedDay(form) }}.</span></label>
              }
              <label class="field">Ne kadar sürsün<select [(ngModel)]="form.span" (ngModelChange)="spanChanged(form)">
                <option value="month">1 ay</option><option value="quarter">3 ay</option><option value="term">Dönem sonuna kadar</option><option value="year">1 yıl</option><option value="custom">Bitiş tarihi seçeceğim</option></select></label>
              <label class="field">Son ders en geç<input type="date" [(ngModel)]="form.endsOn" (ngModelChange)="form.span = 'custom'; preview.set(null)" [max]="maxEnd(form)"></label>
            </div>
            @if (preview(); as checked) {
              <div [class]="checked.conflictCount ? 'banner' : 'notice'" role="status">
                {{ checked.sessionCount }} ders oluşacak.
                @if (checked.conflictCount) { {{ checked.conflictCount }} tanesi çakışıyor: }
                @else { Çakışma yok. }
              </div>
              @if (checked.conflictCount) {
                <ul class="conflicts">
                  @for (item of conflictItems(checked); track item.startsAt) { <li><span class="num">{{ dateTime(item.startsAt) }}</span>: {{ item.text }}</li> }
                </ul>
                <label class="check"><input type="checkbox" [(ngModel)]="skipConflicts">Çakışan {{ checked.conflictCount }} dersi atla, kalanları kaydet</label>
              }
            }
          }
          <div class="dialog-actions">
            <button type="button" class="btn-secondary" (click)="closeEditor()">Vazgeç</button>
            @if (form.kind === 'series' && !preview()) {
              <button type="button" class="btn" (click)="check(form)" [disabled]="busy() || !ready(form)">Dersleri göster</button>
            } @else {
              <button type="button" class="btn" (click)="saveEditor(form)" [disabled]="busy() || !ready(form) || (form.kind === 'series' && !!preview()?.conflictCount && !skipConflicts)">
                {{ form.kind === 'timeoff' ? 'İzni kaydet' : moving() ? 'Taşı' : 'Kaydet' }}</button>
            }
          </div>
        </section>
      </div>
    }
  `,
  styles: `
    .hint-line { margin: 0 0 10px; font-size: 14px; }
    .cal-wrap { overflow-x: auto; }
    .cal { display: grid; grid-template-columns: 64px repeat(7, minmax(120px, 1fr)); min-width: 920px; }
    .corner, .day-head { position: sticky; top: 0; background: var(--ds-surface); z-index: 2; }
    .day-head { padding: 10px 8px; font-weight: 600; font-size: 14px; border-bottom: 1px solid var(--ds-line); border-left: 1px solid var(--ds-line); }
    .day-head.is-today { background: var(--ds-warning-soft); }
    .times { grid-row: 2; grid-column: 1; }
    .time { height: 56px; box-sizing: border-box; padding: 4px 8px; font-size: 13px; color: var(--ds-muted); border-top: 1px solid var(--ds-line); }
    .day-col { grid-row: 2; position: relative; border-left: 1px solid var(--ds-line); }
    .day-col.is-today { background: color-mix(in srgb, var(--ds-warning-soft) 35%, transparent); }
    .slot { display: block; width: 100%; height: 56px; border: 0; border-top: 1px solid var(--ds-line); background: transparent; cursor: copy; padding: 0; }
    .slot:hover:not(:disabled) { background: var(--ds-soft); }
    .lesson { position: absolute; left: 4px; right: 4px; box-sizing: border-box; padding: 6px 8px; border: 0; border-left: 3px solid; border-radius: 8px;
      text-align: left; font: inherit; font-size: 13px; cursor: pointer; overflow: hidden; display: flex; flex-direction: column; gap: 2px; z-index: 1; }
    .lesson strong { font-size: 14px; color: var(--ds-text); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .tone-0 { background: var(--ds-success-soft); border-color: var(--ds-success); color: var(--ds-success); }
    .tone-1 { background: var(--ds-warning-soft); border-color: var(--ds-warning); color: var(--ds-warning); }
    .tone-2 { background: var(--ds-soft); border-color: var(--ds-primary); color: var(--ds-text); }
    .lesson.cancelled { background: var(--ds-surface); border-color: var(--ds-field); color: var(--ds-muted); }
    .lesson.cancelled strong { text-decoration: line-through; color: var(--ds-muted); }
    .menu { position: fixed; z-index: 60; min-width: 240px; padding: 6px; display: flex; flex-direction: column; }
    .menu button { min-height: 44px; text-align: left; padding: 0 12px; border: 0; background: transparent; border-radius: 6px; font: inherit; font-size: 15px; color: var(--ds-text); cursor: pointer; }
    .menu button:hover, .menu button:focus-visible { background: var(--ds-soft); }
    .menu-title { margin: 6px 12px 8px; font-size: 14px; line-height: 1.5; }
    .days { border: 0; padding: 0; margin: 0; display: flex; flex-wrap: wrap; gap: 4px 16px; }
    .days legend { font-size: 14px; font-weight: 500; margin-bottom: 4px; }
    .conflicts { margin: 0; padding-left: 20px; max-height: 160px; overflow: auto; line-height: 1.7; font-size: 14px; }
  `
})
export class CalendarSectionComponent {
  @Output() readonly navigate = new EventEmitter<PanelSection>();
  readonly state = inject(CoursePanelState);
  private readonly api = inject(CoursePanelApi);
  private readonly service = inject(CourseManagementService);
  readonly hours = Array.from({ length: LAST_HOUR - FIRST_HOUR }, (_, index) => FIRST_HOUR + index);
  readonly weekdays = WEEKDAYS;
  readonly durations = [30, 40, 45, 50, 60, 80, 90, 120, 150, 180];
  readonly pad = pad;
  readonly time = formatTime;
  readonly dateTime = formatDateTime;
  readonly weekStart = signal(mondayOf(Date.now()));
  readonly lessons = signal<CourseLesson[]>([]);
  readonly menu = signal<Menu | null>(null);
  readonly editor = signal<Editor | null>(null);
  readonly moving = signal<CourseLesson | null>(null);
  readonly preview = signal<SeriesPreview | null>(null);
  readonly studentOptions = signal<PanelStudentRow[]>([]);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly editorError = signal('');
  readonly notice = signal('');
  teacherFilter = '';
  roomFilter = '';
  skipConflicts = false;
  private readonly courseTone = new Map<string, number>();

  readonly days = computed(() => Array.from({ length: 7 }, (_, index) => {
    const ms = this.weekStart() + index * DAY_MS;
    const d = new Date(ms + TR_OFFSET);
    return { iso: isoDay(ms), label: `${WEEKDAYS[index].slice(0, 3)} ${d.getUTCDate()}`, long: `${WEEKDAYS[index]} ${d.getUTCDate()} ${MONTHS[d.getUTCMonth()]}` };
  }));
  readonly weekLabel = computed(() => {
    const first = new Date(this.weekStart() + TR_OFFSET);
    const last = new Date(this.weekStart() + 6 * DAY_MS + TR_OFFSET);
    return `${first.getUTCDate()} ${MONTHS[first.getUTCMonth()]} – ${last.getUTCDate()} ${MONTHS[last.getUTCMonth()]} ${last.getUTCFullYear()} haftası`;
  });

  constructor() {
    const query = readHashQuery();
    const week = query.get('hafta');
    if (week && /^\d{4}-\d{2}-\d{2}$/.test(week)) this.weekStart.set(mondayOf(instant(week, '12:00').getTime()));
    this.teacherFilter = query.get('ogretmen') ?? '';
    this.roomFilter = query.get('derslik') ?? '';
    effect(() => { this.state.id(); this.state.version(); this.weekStart(); untracked(() => void this.load()); });
  }

  @HostListener('document:click') closeMenu(): void { this.menu.set(null); }
  @HostListener('document:keydown.escape') escape(): void { this.menu.set(null); }

  async load(): Promise<void> {
    const id = this.state.id();
    if (!id) return;
    writeHashQuery('takvim', { hafta: isoDay(this.weekStart()), ogretmen: this.teacherFilter, derslik: this.roomFilter });
    try {
      this.lessons.set(await this.api.lessons(id, new Date(this.weekStart()), new Date(this.weekStart() + 7 * DAY_MS)));
      this.error.set('');
    } catch (error) { this.error.set(problemText(error, 'Takvim yüklenemedi.')); }
  }

  shift(days: number): void { this.weekStart.update(value => value + days * DAY_MS); }
  today(): void { this.weekStart.set(mondayOf(Date.now())); }
  todayIso(): string { return isoDay(Date.now()); }
  nextHour(): string { const d = new Date(Date.now() + TR_OFFSET); return `${pad(Math.min(d.getUTCHours() + 1, LAST_HOUR - 1))}:00`; }
  setFilter(teacher: string, room: string): void { this.teacherFilter = teacher; this.roomFilter = room; void this.load(); }

  lessonsOn(day: string): CourseLesson[] {
    return this.lessons().filter(lesson => isoDay(new Date(lesson.startsAt).getTime()) === day
      && (!this.teacherFilter || lesson.teacherUserId === this.teacherFilter)
      && (!this.roomFilter || lesson.roomId === this.roomFilter));
  }
  private minutes(value: string): number { const d = new Date(new Date(value).getTime() + TR_OFFSET); return d.getUTCHours() * 60 + d.getUTCMinutes(); }
  top(lesson: CourseLesson): number { return Math.max(0, (this.minutes(lesson.startsAt) - FIRST_HOUR * 60) * HOUR_PX / 60); }
  height(lesson: CourseLesson): number {
    return Math.max(28, (new Date(lesson.endsAt).getTime() - new Date(lesson.startsAt).getTime()) / 60000 * HOUR_PX / 60 - 2);
  }
  tone(lesson: CourseLesson): number {
    if (!this.courseTone.has(lesson.courseId)) this.courseTone.set(lesson.courseId, this.courseTone.size % 3);
    return this.courseTone.get(lesson.courseId)!;
  }
  studentNames(lesson: CourseLesson): string { return lesson.students.map(student => student.displayName).join(', ') || lesson.courseName; }
  lessonLabel(lesson: CourseLesson): string {
    return `${lesson.groupName ?? this.studentNames(lesson)}, ${lesson.courseName}, ${formatDateTime(lesson.startsAt)}`
      + (lesson.status === 'Cancelled' ? ', iptal edildi' : '') + '. Seçenekler için Enter.';
  }
  isFuture(lesson: CourseLesson): boolean { return new Date(lesson.startsAt) > new Date(); }
  dayLong(day: string): string { return this.days().find(item => item.iso === day)?.long ?? day; }
  minutesLabel(minutes: number): string { return minutes < 60 ? `${minutes} dakika` : minutes % 60 ? `${Math.floor(minutes / 60)} saat ${minutes % 60} dakika` : `${minutes / 60} saat`; }

  openMenu(event: MouseEvent, day: string, time: string, lesson: CourseLesson | null): void {
    event.preventDefault();
    event.stopPropagation();
    if (!lesson && this.state.readOnly()) return;
    const target = event.currentTarget as HTMLElement;
    const box = target.getBoundingClientRect();
    const x = event.clientX || box.left + 12;
    const y = event.clientY || box.top + 12;
    this.menu.set({ x: Math.min(x, (globalThis.innerWidth ?? 1200) - 260), y: Math.min(y, (globalThis.innerHeight ?? 800) - 240), day, time, lesson });
    setTimeout(() => (document.querySelector('.menu [role=menuitem]') as HTMLElement | null)?.focus());
  }

  openEditor(kind: Editor['kind'], day: string, time: string): void {
    this.menu.set(null);
    this.moving.set(null);
    this.preview.set(null);
    this.skipConflicts = false;
    this.editorError.set('');
    const weekday = (new Date(`${day}T12:00:00+03:00`).getUTCDay() + 6) % 7;
    const days = Array.from({ length: 7 }, (_, index) => index === weekday);
    const teacher = this.teacherFilter || (this.state.isOwner() ? this.state.teachers()[0]?.userId ?? '' : this.state.myUserId());
    const [h, m] = time.split(':').map(Number);
    const endTime = `${pad(Math.min(h + 1, 23))}:${pad(m)}`;
    this.editor.set({ kind, courseId: this.state.courses()[0]?.id ?? '', target: 'group', groupId: '', studentId: '', studentName: '',
      teacherUserId: teacher, roomId: this.roomFilter, day, time, duration: 60, days, repeat: 'weekly', monthWeek: Math.min(5, Math.ceil(Number(day.slice(8)) / 7)),
      span: 'term', endsOn: this.spanEnd(day, 'term'), endTime, reason: '' });
  }

  openMove(lesson: CourseLesson): void {
    this.menu.set(null);
    this.openEditor('single', isoDay(new Date(lesson.startsAt).getTime()), formatTime(lesson.startsAt).replace('.', ':'));
    const minutes = (new Date(lesson.endsAt).getTime() - new Date(lesson.startsAt).getTime()) / 60000;
    this.editor.update(form => form && { ...form, duration: minutes, roomId: lesson.roomId ?? '' });
    this.moving.set(lesson);
  }

  closeEditor(): void { this.editor.set(null); this.moving.set(null); this.preview.set(null); }

  groupsFor(courseId: string) { return this.state.groups().filter(group => group.courseId === courseId); }
  groupPicked(form: Editor): void {
    const group = this.state.groups().find(item => item.id === form.groupId);
    if (group && this.state.isOwner()) form.teacherUserId = group.teacherUserId;
    this.preview.set(null);
  }
  async findStudents(text: string): Promise<void> {
    const form = this.editor();
    if (!form) return;
    const match = this.studentOptions().find(student => student.displayName === text);
    form.studentId = match?.id ?? '';
    if (match || text.trim().length < 2) return;
    try { this.studentOptions.set((await this.api.searchStudents(this.state.id(), { q: text.trim(), take: 20 })).items); } catch { /* typing continues */ }
  }

  checkedDays(form: Editor): number { return form.days.filter(Boolean).length; }
  firstCheckedDay(form: Editor): string { return (WEEKDAYS[form.days.findIndex(Boolean)] ?? 'gününü').toLocaleLowerCase('tr-TR'); }
  repeatChanged(form: Editor): void {
    if (form.repeat === 'monthly' && this.checkedDays(form) > 1) {
      const first = form.days.findIndex(Boolean);
      form.days = form.days.map((_, index) => index === first);
    }
    this.preview.set(null);
  }
  dayChanged(form: Editor): void { if (form.span !== 'custom') form.endsOn = this.spanEnd(form.day, form.span); this.preview.set(null); }
  spanChanged(form: Editor): void { if (form.span !== 'custom') form.endsOn = this.spanEnd(form.day, form.span); this.preview.set(null); }
  maxEnd(form: Editor): string { return isoDay(instant(form.day, '12:00').getTime() + 366 * DAY_MS); }
  /** "Dönem sonu": the school year ends mid-June; after June it runs to the next June (capped at a year). */
  private spanEnd(day: string, span: Editor['span']): string {
    const start = instant(day, '12:00').getTime();
    const add = (days: number) => isoDay(start + days * DAY_MS);
    if (span === 'month') return add(30);
    if (span === 'quarter') return add(91);
    if (span === 'year') return add(365);
    const year = Number(day.slice(0, 4)) + (Number(day.slice(5, 7)) >= 6 ? 1 : 0);
    const termEnd = `${year}-06-19`;
    return termEnd > add(366) ? add(366) : termEnd < day ? add(30) : termEnd;
  }

  ready(form: Editor): boolean {
    if (!form.day || !form.time) return false;
    if (form.kind === 'timeoff') return !!form.teacherUserId && form.endTime > form.time;
    if (this.moving()) return true;
    const target = form.target === 'group' ? !!form.groupId : !!form.studentId;
    const days = form.kind !== 'series' || this.checkedDays(form) > 0 && !!form.endsOn && form.endsOn >= form.day;
    return !!form.courseId && !!form.teacherUserId && target && days;
  }

  private seriesRequest(form: Editor): SeriesRequest {
    return {
      courseId: form.courseId, groupId: form.target === 'group' ? form.groupId : null, studentId: form.target === 'student' ? form.studentId : null,
      teacherUserId: form.teacherUserId, startsOn: form.day, endsOn: form.endsOn, startTime: form.time, durationMinutes: form.duration,
      daysOfWeekMask: form.days.reduce((mask, on, index) => on ? mask | (1 << index) : mask, 0), roomId: form.roomId || null,
      repeatEveryWeeks: form.repeat === 'biweekly' ? 2 : 1, monthlyWeekOfMonth: form.repeat === 'monthly' ? form.monthWeek : null
    };
  }

  async check(form: Editor): Promise<void> {
    this.busy.set(true); this.editorError.set('');
    try { this.preview.set(await this.api.previewSeries(this.state.id(), this.seriesRequest(form))); }
    catch (error) { this.editorError.set(problemText(error)); }
    finally { this.busy.set(false); }
  }

  conflictItems(preview: SeriesPreview): { startsAt: string; text: string }[] {
    return preview.occurrences.filter(item => item.conflicts.length).slice(0, 30)
      .map(item => ({ startsAt: item.startsAt, text: item.conflicts.map(code => problemText({ error: { code } })).join(' ') }));
  }

  async saveEditor(form: Editor): Promise<void> {
    const id = this.state.id();
    this.busy.set(true); this.editorError.set('');
    try {
      const startsAt = instant(form.day, form.time);
      const endsAt = new Date(startsAt.getTime() + form.duration * 60000);
      const moving = this.moving();
      if (form.kind === 'timeoff') {
        await this.service.createTeacherTimeOff(id, form.teacherUserId, startsAt.toISOString(), instant(form.day, form.endTime).toISOString(), form.reason);
        this.notice.set('Öğretmen izni eklendi.');
      } else if (moving) {
        await this.service.rescheduleLesson(id, moving.id, startsAt.toISOString(), endsAt.toISOString(), form.roomId || null);
        this.notice.set('Ders taşındı. Kurum açtıysa öğrencilere ve velilere e-posta gider.');
      } else if (form.kind === 'single') {
        await this.service.createLesson(id, form.courseId, form.target === 'group' ? form.groupId : null,
          form.target === 'student' ? form.studentId : null, form.teacherUserId, startsAt.toISOString(), endsAt.toISOString(), form.roomId || null);
        this.notice.set('Ders eklendi.');
      } else {
        const result = await this.api.createSeries(id, this.seriesRequest(form), this.skipConflicts);
        this.notice.set(result.sessionsSkipped ? `${result.sessionsCreated} ders eklendi; çakışan ${result.sessionsSkipped} ders atlandı.` : `${result.sessionsCreated} ders eklendi.`);
      }
      this.closeEditor();
      this.state.changed();
    } catch (error) { this.editorError.set(problemText(error)); }
    finally { this.busy.set(false); }
  }

  async cancel(lesson: CourseLesson): Promise<void> {
    this.menu.set(null);
    if (!globalThis.confirm?.(`${formatDateTime(lesson.startsAt)} dersi iptal edilsin mi? Öğrencilere telafi hakkı açılır.`)) return;
    try { await this.service.cancelLesson(this.state.id(), lesson.id); this.notice.set('Ders iptal edildi; öğrencilere telafi hakkı açıldı.'); this.state.changed(); }
    catch (error) { this.error.set(problemText(error)); }
  }

  async cancelSeries(lesson: CourseLesson): Promise<void> {
    this.menu.set(null);
    if (!lesson.seriesId || !globalThis.confirm?.('Bu serinin bundan sonraki bütün dersleri iptal edilsin mi?')) return;
    try {
      const result = await this.service.cancelFutureLessonSeries(this.state.id(), lesson.seriesId);
      this.notice.set(`${result.sessionsCancelled} ders iptal edildi.`);
      this.state.changed();
    } catch (error) { this.error.set(problemText(error)); }
  }

  takeAttendance(): void {
    const lesson = this.menu()?.lesson;
    this.menu.set(null);
    if (lesson) globalThis.history?.replaceState(null, '', `#yoklama?ders=${lesson.id}`);
    this.navigate.emit('attendance');
  }
}
