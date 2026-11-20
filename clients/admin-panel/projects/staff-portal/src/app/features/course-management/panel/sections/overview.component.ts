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
        <section class="card card-pad setup-card" aria-labelledby="setup-card-title" style="margin-bottom: 18px">
          <div><h2 id="setup-card-title" style="margin: 0 0 4px; font-size: 17px">Kurulum: {{ state.setupProgress().done }} / {{ state.setupProgress().total }} adım tamam</h2>
            <p class="muted" style="margin: 0">Öğretmen, kurs, öğrenci, ücret ve ders programını adım adım girmenize yardım eder.</p></div>
          <button type="button" class="btn" (click)="navigate.emit('setup')">Kuruluma devam et</button>
        </section>
      }
    }
    @if (data(); as view) {
      <div class="stack">
        <section class="kpis" aria-label="Özet">
          <div class="card kpi"><span class="kpi-label">Bugünkü dersler</span><span class="kpi-value">{{ heldCount(view) }}</span>
            <span class="kpi-label">{{ doneCount(view) }} tamamlandı{{ cancelledCount(view) ? ', ' + cancelledCount(view) + ' iptal' : '' }}</span></div>
          <div class="card kpi"><span class="kpi-label">Yoklaması alınmamış</span><span class="kpi-value">{{ view.lessonsWaitingForAttendance.length }}</span>
            <span class="kpi-label">Son 7 gün</span></div>
          @if (view.finance; as money) {
            <div class="card kpi"><span class="kpi-label">Geciken taksit</span><span class="kpi-value">{{ money.overdueCount }}</span>
              <span class="kpi-label num">{{ formatMoney(money.overdueAmount) }}, {{ money.overdueStudentCount }} öğrenci</span></div>
            <div class="card kpi"><span class="kpi-label">Bu ayın tahsilatı</span><span class="kpi-value num">{{ formatMoney(money.monthCollections) }}</span>
              <span class="kpi-label">İade ve düzeltmeler düşülmüş</span></div>
          }
        </section>
        <div class="row">
          <section class="card grow" aria-labelledby="today-title">
            <div class="card-head"><h2 id="today-title">Bugünkü dersler</h2>
              <button type="button" class="btn-quiet" (click)="navigate.emit('calendar')">Takvimi aç</button></div>
            @if (view.todayLessons.length) {
              <ul class="lessons">
                @for (lesson of view.todayLessons; track lesson.id) {
                  <li>
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
          <section class="card side" aria-labelledby="waiting-title">
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
    .setup-card { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 12px; border-left: 3px solid var(--ds-accent); }
    .lessons { list-style: none; margin: 0; padding: 0; }
    .lessons li { display: grid; grid-template-columns: auto minmax(0, 1fr) auto; gap: 16px; align-items: center; padding: 14px 20px; border-top: 1px solid var(--ds-line); }
    .side .lessons li { grid-template-columns: minmax(0, 1fr) auto; }
    .time { font-weight: 600; min-width: 96px; }
    .what { display: flex; flex-direction: column; gap: 2px; min-width: 0; }
    .what .muted { font-size: 14px; }
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
