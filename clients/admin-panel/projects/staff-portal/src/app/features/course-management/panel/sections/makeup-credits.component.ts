import { Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CourseMakeupCredit, CourseManagementService } from '../../course-management.service';
import { CoursePanelState, formatDate, formatDateTime, problemText } from '../panel-state';

const STATUS: Record<string, { label: string; tone: string }> = {
  PendingTeacherApproval: { label: 'Onay bekliyor', tone: 'warn' },
  Available: { label: 'Planlanabilir', tone: 'ok' },
  Scheduled: { label: 'Telafi dersi planlandı', tone: '' },
  Consumed: { label: 'Kullanıldı', tone: '' },
  Expired: { label: 'Süresi doldu', tone: 'bad' }
};

/**
 * Telafi hakları (moved from the earlier single-page screen, 21.11.2026). A lesson cancelled for a student opens a
 * credit; an early cancellation by the student waits for the teacher's approval. The credit is used by planning a
 * makeup lesson before it runs out. A teacher sees only the credits of their own lessons (the server filters).
 */
@Component({
  selector: 'course-makeup-credits',
  standalone: true,
  imports: [FormsModule],
  styleUrl: '../panel.scss',
  template: `
    <section class="card" aria-labelledby="makeup-title">
      <div class="card-head"><h2 id="makeup-title">Telafi hakları</h2>
        @if (waiting().length) { <span class="badge warn">{{ waiting().length }} işlem bekliyor</span> }</div>
      @if (error()) { <p class="alert" role="alert" style="margin: 0 20px 12px">{{ error() }}</p> }
      @if (notice()) { <p class="notice" role="status" style="margin: 0 20px 12px">{{ notice() }}</p> }
      <ul class="credits">
        @for (credit of shown(); track credit.id) {
          <li>
            <div class="what">
              <strong>{{ credit.studentName }}</strong>
              <span class="badge" [class]="'badge ' + status(credit).tone">{{ status(credit).label }}</span>
              <span class="muted">İptal edilen ders {{ dateTime(credit.sourceLessonStartsAt) }}, {{ credit.durationMinutes }} dk. Son gün {{ date(credit.expiresAt) }}.</span>
            </div>
            @if (!state.readOnly()) {
              @if (credit.status === 'PendingTeacherApproval') {
                <button type="button" class="btn-secondary btn-small" [disabled]="busy()" (click)="approve(credit)">Telafi hakkını onayla</button>
              }
              @if (credit.status === 'Available') {
                <form class="plan" (ngSubmit)="schedule(credit)">
                  <label class="field">Gün<input type="date" [name]="'d' + credit.id" [(ngModel)]="days[credit.id]" [min]="today()" [max]="credit.expiresAt.slice(0, 10)"></label>
                  <label class="field">Saat<input type="time" [name]="'t' + credit.id" [(ngModel)]="times[credit.id]" step="300"></label>
                  <button type="submit" class="btn btn-small" [disabled]="busy() || !canSchedule(credit)">Telafi dersini planla</button>
                </form>
              }
            }
          </li>
        } @empty { <li class="empty">Onay ya da planlama bekleyen telafi hakkı yok.</li> }
      </ul>
      @if (credits().length > shown().length) {
        <div style="padding: 0 20px 16px"><button type="button" class="btn-quiet" (click)="all.set(true)">Kullanılan ve süresi dolanları da göster</button></div>
      }
      <p class="muted" style="margin: 0; padding: 0 20px 16px; font-size: 14px">Telafi dersi, son günden önce bitecek şekilde planlanmalı ve tamamlanmalı.
        Öğrencinin erken iptali öğretmen onayından sonra kullanılabilir.</p>
    </section>
  `,
  styles: `
    .credits { list-style: none; margin: 0; padding: 0; }
    .credits li { display: flex; flex-wrap: wrap; align-items: flex-end; justify-content: space-between; gap: 10px; padding: 12px 20px; border-top: 1px solid var(--ds-line); }
    .what { display: flex; flex-wrap: wrap; align-items: center; gap: 6px 10px; min-width: 0; flex: 1 1 280px; }
    .what .muted { flex-basis: 100%; font-size: 14px; }
    .plan { display: flex; flex-wrap: wrap; align-items: flex-end; gap: 8px; }
    .plan .field { min-width: 140px; }
  `
})
export class MakeupCreditsComponent {
  readonly state = inject(CoursePanelState);
  private readonly service = inject(CourseManagementService);
  readonly credits = signal<CourseMakeupCredit[]>([]);
  readonly all = signal(false);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly notice = signal('');
  readonly date = formatDate;
  readonly dateTime = formatDateTime;
  readonly waiting = computed(() => this.credits().filter(credit => credit.status === 'PendingTeacherApproval' || credit.status === 'Available'));
  readonly shown = computed(() => this.all() ? this.credits() : this.waiting());
  days: Record<string, string> = {};
  times: Record<string, string> = {};

  constructor() {
    effect(() => { this.state.id(); this.state.version(); untracked(() => void this.load()); });
  }

  async load(): Promise<void> {
    const id = this.state.id();
    if (!id) return;
    try { this.credits.set(await this.service.getMakeupCredits(id)); }
    catch (error) { this.error.set(problemText(error, 'Telafi hakları yüklenemedi.')); }
  }

  status(credit: CourseMakeupCredit) { return STATUS[credit.status] ?? { label: 'Bilinmiyor', tone: '' }; }
  today(): string { return new Date(Date.now() + 3 * 3_600_000).toISOString().slice(0, 10); }

  private startsAt(credit: CourseMakeupCredit): Date | null {
    const day = this.days[credit.id]; const time = this.times[credit.id];
    return day && time ? new Date(`${day}T${time}:00+03:00`) : null;
  }

  canSchedule(credit: CourseMakeupCredit): boolean {
    const start = this.startsAt(credit);
    if (!start || Number.isNaN(start.getTime())) return false;
    const end = start.getTime() + credit.durationMinutes * 60_000;
    return start.getTime() > Date.now() && end <= Date.parse(credit.expiresAt);
  }

  approve(credit: CourseMakeupCredit): Promise<void> {
    return this.act(() => this.service.approveMakeupCredit(this.state.id(), credit.id),
      `${credit.studentName} için telafi hakkı onaylandı; ${this.date(credit.expiresAt)} tarihine kadar planlayın.`);
  }

  schedule(credit: CourseMakeupCredit): Promise<void> {
    const start = this.startsAt(credit);
    if (!start || !this.canSchedule(credit)) return Promise.resolve();
    const end = new Date(start.getTime() + credit.durationMinutes * 60_000);
    return this.act(async () => {
      await this.service.createLesson(this.state.id(), credit.sourceCourseId, null, credit.studentId, credit.sourceTeacherUserId,
        start.toISOString(), end.toISOString(), null, credit.id);
      this.state.changed();
    }, `${credit.studentName} için telafi dersi planlandı.`);
  }

  private async act(work: () => Promise<unknown>, done: string): Promise<void> {
    this.busy.set(true); this.error.set(''); this.notice.set('');
    try { await work(); this.notice.set(done); await this.load(); }
    catch (error) { this.error.set(problemText(error)); }
    finally { this.busy.set(false); }
  }
}
