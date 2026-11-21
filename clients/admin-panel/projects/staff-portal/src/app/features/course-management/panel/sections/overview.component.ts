import { Component, EventEmitter, OnInit, Output, effect, inject, signal } from '@angular/core';
import { CoursePanelApi, PanelOverview } from '../panel-api.service';
import { CoursePanelState, PanelSection, formatDay, formatMoney, formatTime, formatDateTime, problemText } from '../panel-state';

const EMAIL_KINDS: Record<string, string> = {
  Absence: 'devamsızlık', HomeworkNotDone: 'ödev', InstallmentReminder: 'taksit hatırlatma', InstallmentOverdue: 'geciken taksit',
  LessonChanged: 'ders değişikliği', WeeklySchedule: 'haftalık program', PaymentReceived: 'ödeme bilgisi'
};

/** Genel bakış: today's lessons, lessons still waiting for attendance and, for the owner, money and e-mails. */
@Component({
  selector: 'course-overview-section',
  standalone: true,
  styleUrl: '../panel.scss',
  template: `
    <header class="page-head">
      <div><h1>Genel bakış</h1><p>{{ data() ? day(data()!.today) : '' }}</p></div>
      @if (state.isOwner() && !state.readOnly()) {
        <div class="actions">
          <button type="button" class="btn-secondary" (click)="navigate.emit('students')">Öğrenci ekle</button>
          <button type="button" class="btn" (click)="navigate.emit('calendar')">Ders planla</button>
        </div>
      }
    </header>
    @if (error()) { <p class="alert" role="alert">{{ error() }} <button type="button" class="btn-quiet" (click)="load()">Yeniden dene</button></p> }
    @if (state.isOwner() && state.setup(); as setup) {
      @if (!setup.closed && state.setupProgress().done < state.setupProgress().total) {
        <section class="card card-pad setup-card rise" aria-labelledby="setup-card-title" style="margin-bottom: 18px">
          <div class="setup-text"><h2 id="setup-card-title">Kurulum: {{ state.setupProgress().done }} / {{ state.setupProgress().total }} adım tamam</h2>
            <div class="progress" role="progressbar" aria-label="Kurulum ilerlemesi" aria-valuemin="0" [attr.aria-valuemax]="state.setupProgress().total"
              [attr.aria-valuenow]="state.setupProgress().done"><span [style.width.%]="100 * state.setupProgress().done / (state.setupProgress().total || 1)"></span></div>
            <p>Öğretmen, kurs, öğrenci, ücret ve ders programını adım adım girmenize yardım eder.</p></div>
          <button type="button" class="btn btn-accent" (click)="navigate.emit('setup')">Kuruluma devam et</button>
        </section>
      }
    }
    @if (data(); as view) {
      <div class="stack">
        <div class="cq"><section class="kpis" [class.four]="!!view.finance" aria-label="Özet">
          <button type="button" class="card tap kpi tone-info rise" style="--i: 0" (click)="navigate.emit('calendar')">
            <span class="kpi-top"><span class="kpi-label">Bugünkü dersler</span>
              <span class="kpi-icon" aria-hidden="true"><svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><rect x="3" y="5" width="18" height="16" rx="2"></rect><path d="M3 10h18M8 3v4M16 3v4"></path></svg></span></span>
            <span class="kpi-value">{{ heldCount(view) }}</span>
            <span class="kpi-label">{{ doneCount(view) }} tamamlandı{{ cancelledCount(view) ? ', ' + cancelledCount(view) + ' iptal' : '' }}</span></button>
          <button type="button" class="card tap kpi tone-warn rise" style="--i: 1" (click)="navigate.emit('attendance')">
            <span class="kpi-top"><span class="kpi-label">Yoklaması alınmamış</span>
              <span class="kpi-icon" aria-hidden="true"><svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="M9 11l3 3 8-8"></path><path d="M20 12v7a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h9"></path></svg></span></span>
            <span class="kpi-value">{{ view.lessonsWaitingForAttendance.length }}</span>
            <span class="kpi-label">Son 7 gün</span></button>
          @if (view.finance; as money) {
            <button type="button" class="card tap kpi rise" style="--i: 2" [class.tone-bad]="money.overdueCount > 0" [class.tone-ok]="money.overdueCount === 0" (click)="navigate.emit('finance')">
              <span class="kpi-top"><span class="kpi-label">Geciken taksit</span>
                <span class="kpi-icon" aria-hidden="true"><svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="9"></circle><path d="M12 7v6M12 16.5v.5"></path></svg></span></span>
              <span class="kpi-value">{{ money.overdueCount }}</span>
              <span class="kpi-label num">{{ formatMoney(money.overdueAmount) }}, {{ money.overdueStudentCount }} öğrenci</span></button>
            <button type="button" class="card tap kpi tone-ok rise" style="--i: 3" (click)="navigate.emit('finance')">
              <span class="kpi-top"><span class="kpi-label">Bu ayın tahsilatı</span>
                <span class="kpi-icon" aria-hidden="true"><svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="9"></circle><path d="M9 9.5h4.5a2 2 0 0 1 0 4H9.5M9 9.5V16M11 7v2M11 16v1"></path></svg></span></span>
              <span class="kpi-value num">{{ formatMoney(money.monthCollections) }}</span>
              <span class="kpi-label">İade ve düzeltmeler düşülmüş</span></button>
          }
        </section></div>
        <div class="row">
          <section class="card grow rise" style="--i: 4" aria-labelledby="today-title">
            <div class="card-head"><h2 id="today-title">Bugünkü dersler</h2>
              <button type="button" class="btn-quiet" (click)="navigate.emit('calendar')">Takvimi aç</button></div>
            @if (view.todayLessons.length) {
              <ul class="lessons">
                @for (lesson of view.todayLessons; track lesson.id) {
                  <li [class]="'is-' + status(lesson)">
                    <span class="bar" aria-hidden="true"></span>
                    <span class="num time">{{ time(lesson.startsAt) }}–{{ time(lesson.endsAt) }}</span>
                    <div class="what"><strong>{{ lesson.groupName ?? lesson.courseName }}</strong>
                      <span class="muted">{{ lesson.courseName }}{{ lesson.teacherName ? ', ' + lesson.teacherName : '' }}{{ lesson.roomName ? ', ' + lesson.roomName : '' }}</span></div>
                    <div class="actions">
                      @switch (status(lesson)) {
                        @case ('done') { <span class="badge ok">Yoklama alındı</span> }
                        @case ('now') { <span class="badge now">Şimdi</span> }
                        @case ('cancelled') { <span class="badge">İptal</span> }
                        @case ('waiting') { <span class="badge warn">Yoklama bekliyor</span> }
                        @default { <span class="badge">Sırada</span> }
                      }
                      @if (status(lesson) === 'now' || status(lesson) === 'waiting') {
                        <button type="button" class="btn btn-small" (click)="navigate.emit('attendance')">Yoklama al</button>
                      }
                    </div>
                  </li>
                }
              </ul>
            } @else { <p class="empty">Bugün ders yok. Takvimden ders planlayabilirsiniz.</p> }
          </section>
          <section class="card side rise" style="--i: 5" aria-labelledby="waiting-title">
            <div class="card-head"><h2 id="waiting-title">Yoklama bekleyenler</h2></div>
            @if (view.lessonsWaitingForAttendance.length) {
              <ul class="lessons">
                @for (lesson of view.lessonsWaitingForAttendance; track lesson.id) {
                  <li><div class="what"><strong>{{ lesson.groupName ?? lesson.courseName }}</strong>
                    <span class="muted">{{ dateTime(lesson.startsAt) }}</span></div>
                    <button type="button" class="btn-secondary btn-small" (click)="navigate.emit('attendance')">Yoklama al</button></li>
                }
              </ul>
            } @else { <p class="empty">Yoklaması eksik ders yok.</p> }
          </section>
        </div>
        @if (emailsOff()) {
          <section class="card card-pad actions" style="justify-content: space-between" aria-labelledby="emails-off-title">
            <div><strong id="emails-off-title">Velilere e-posta gitmiyor</strong>
              <p class="muted" style="margin: 2px 0 0">Devamsızlık, yapılmayan ödev ve taksit e-postaları başta kapalıdır. Açmak birkaç dakika sürer.</p></div>
            <button type="button" class="btn" (click)="navigate.emit('settings')">E-posta bildirimlerini aç</button>
          </section>
        }
        @if (view.emails; as mail) {
          <section class="card card-pad actions" style="justify-content: space-between">
            <div><strong>Dün {{ mail.sent }} e-posta gönderildi</strong>
              <p class="muted" style="margin: 2px 0 0">{{ emailLine(view) }}</p></div>
            <button type="button" class="btn-secondary" (click)="navigate.emit('settings')">E-posta kayıtları</button>
          </section>
        }
      </div>
    } @else if (!error()) { <p class="empty" role="status">Yükleniyor…</p> }
  `,
  styles: `
    .setup-card { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 16px;
      background: var(--ds-visual-bg); color: var(--ds-visual-text); border-color: transparent; }
    .setup-text { flex: 1 1 320px; min-width: 0; display: flex; flex-direction: column; gap: 10px; }
    .setup-card h2 { font-size: 17px; }
    .setup-card p { margin: 0; color: var(--ds-visual-muted); line-height: 1.5; }
    .progress { height: 8px; border-radius: 999px; background: color-mix(in srgb, var(--ds-visual-text) 18%, transparent); overflow: hidden; max-width: 420px; }
    .progress span { display: block; height: 100%; border-radius: 999px; background: var(--ds-accent); animation: fill 1s .25s cubic-bezier(.2, .7, .2, 1) both; }
    @keyframes fill { from { width: 0; } }
    .kpi { gap: 8px; }
    .kpi .kpi-label { color: var(--ds-text); opacity: .8; }
    .lessons { list-style: none; margin: 0; padding: 0; }
    .lessons li { display: grid; grid-template-columns: 4px auto minmax(0, 1fr) auto; gap: 14px; align-items: center; padding: 14px 20px; border-top: 1px solid var(--ds-line); }
    .lessons .bar { align-self: stretch; border-radius: 4px; background: var(--ds-line); }
    .lessons li.is-now { background: var(--ds-now-soft); } .lessons li.is-now .bar { background: var(--ds-accent); }
    .lessons li.is-done .bar { background: var(--ds-success); opacity: .55; }
    .lessons li.is-waiting .bar { background: var(--ds-warning); }
    .lessons li.is-next .bar { background: var(--ds-info); opacity: .5; }
    .side .lessons li { grid-template-columns: minmax(0, 1fr) auto; }
    .time { font-weight: 600; min-width: 96px; }
    .what { display: flex; flex-direction: column; gap: 2px; min-width: 0; }
    .what strong { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .what .muted { font-size: 14px; }
    @media (max-width: 560px) {
      .grow .lessons li { grid-template-columns: 4px minmax(0, 1fr) auto; row-gap: 2px; column-gap: 12px; padding: 12px 16px; }
      .grow .lessons .bar { grid-column: 1; grid-row: 1 / span 2; }
      .grow .lessons li .time { grid-column: 2; grid-row: 1; min-width: 0; font-size: 14px; }
      .grow .lessons li .what { grid-column: 2; grid-row: 2; }
      .grow .lessons li .actions { grid-column: 3; grid-row: 1 / span 2; flex-direction: column; align-items: flex-end; gap: 6px; }
      .side .lessons li { padding: 12px 16px; }
    }
  `
})
export class OverviewSectionComponent implements OnInit {
  @Output() readonly navigate = new EventEmitter<PanelSection>();
  readonly state = inject(CoursePanelState);
  private readonly api = inject(CoursePanelApi);
  readonly data = signal<PanelOverview | null>(null);
  /** The owner has not turned any e-mail on yet: new workspaces start with every e-mail off (20.11.2026 live test). */
  readonly emailsOff = signal(false);
  readonly error = signal('');
  readonly formatMoney = formatMoney;
  readonly time = formatTime;
  readonly dateTime = formatDateTime;

  constructor() { effect(() => { this.state.id(); this.state.version(); void this.load(); }); }
  ngOnInit(): void { /* loaded by the effect */ }

  async load(): Promise<void> {
    const id = this.state.id();
    if (!id) return;
    this.error.set('');
    try {
      const view = await this.api.overview(id);
      this.data.set(view);
      if (this.state.isOwner() && !this.state.readOnly()) {
        const settings = await this.api.emailSettings(id).catch(() => null);
        this.emailsOff.set(!!settings && (!settings.informedConsentAt || !settings.rules.some(rule => rule.enabled)));
      } else this.emailsOff.set(false);
    } catch (error) { this.error.set(problemText(error, 'Genel bakış yüklenemedi.')); }
  }

  day(value: string): string { return formatDay(`${value}T12:00:00+03:00`); }
  /** Today's lessons that take place: a cancelled lesson is listed but not counted (20.11.2026 live test). */
  heldCount(view: PanelOverview): number { return view.todayLessons.filter(item => item.status !== 'Cancelled').length; }
  cancelledCount(view: PanelOverview): number { return view.todayLessons.length - this.heldCount(view); }
  doneCount(view: PanelOverview): number { return view.todayLessons.filter(item => item.status === 'Completed' || item.attendanceTaken).length; }
  status(lesson: PanelOverview['todayLessons'][number]): 'done' | 'now' | 'cancelled' | 'waiting' | 'next' {
    if (lesson.status === 'Cancelled') return 'cancelled';
    if (lesson.attendanceTaken || lesson.status === 'Completed') return 'done';
    if (lesson.status === 'InProgress') return 'now';
    return new Date(lesson.endsAt) <= new Date() ? 'waiting' : 'next';
  }
  emailLine(view: PanelOverview): string {
    const mail = view.emails!;
    const parts = mail.byKind.map(item => `${item.count} ${EMAIL_KINDS[item.kind] ?? item.kind}`);
    const sent = parts.length ? parts.join(', ') : 'Gönderilen e-posta yok';
    return mail.skipped ? `${sent}. ${mail.skipped} e-posta gönderilemedi; nedenleri kayıtlarda.` : `${sent}.`;
  }
}
