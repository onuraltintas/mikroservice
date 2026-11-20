import { Component, effect, inject, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CourseInvitation, CourseManagementService, TeacherAvailabilityWindow, TeacherTimeOff } from '../../course-management.service';
import { CoursePanelApi, PanelTeacherInvitation } from '../panel-api.service';
import { CoursePanelState, formatDate, formatDateTime, problemText } from '../panel-state';
import { PanelConfirm } from '../panel-confirm';

const WEEKDAYS = ['Pazartesi', 'Salı', 'Çarşamba', 'Perşembe', 'Cuma', 'Cumartesi', 'Pazar'];

/** Öğretmenler: invite by e-mail, see who is in, their weekly hours and leave. Owner only. */
@Component({
  selector: 'course-teachers-section',
  standalone: true,
  imports: [FormsModule],
  styleUrl: '../panel.scss',
  template: `
    <header class="page-head"><div><h1>Öğretmenler</h1><p>Öğretmenler e-postayla gelen davetle katılır; yalnız kendi derslerini görürler.</p></div></header>
    @if (notice()) { <p class="notice" role="status">{{ notice() }}</p> }
    @if (error()) { <p class="alert" role="alert">{{ error() }}</p> }
    <div class="row">
      <div class="grow stack">
        <section class="card" aria-labelledby="teacher-list-title">
          <div class="card-head"><h2 id="teacher-list-title">Kurumdakiler</h2></div>
          <div class="table-wrap"><table>
            <thead><tr><th scope="col">Ad</th><th scope="col">Rol</th><th scope="col">Haftalık saatler</th><th scope="col"><span class="sr-only">İşlem</span></th></tr></thead>
            <tbody>
              @for (member of activeMembers(); track member.userId) {
                <tr>
                  <th scope="row">{{ member.displayName ?? 'Adı girilmemiş' }}</th>
                  <td>{{ member.role === 'Owner' ? 'Kurum sahibi' : 'Öğretmen' }}</td>
                  <td>{{ hoursLine(member.userId) }}</td>
                  <td class="right">@if (member.role === 'Teacher' && !state.readOnly()) {
                    <button type="button" class="btn-quiet" (click)="revoke(member.userId)">Kurumdan çıkar</button> }</td>
                </tr>
              }
            </tbody>
          </table></div>
        </section>
        @if (invitations().length || emailInvitations().length) {
          <section class="card" aria-labelledby="invite-list-title">
            <div class="card-head"><h2 id="invite-list-title">Davetler</h2></div>
            <ul class="plain">
              @for (invitation of emailInvitations(); track invitation.id) {
                <li><span><strong>{{ invitation.email }}</strong> {{ statusLine(invitation) }}</span>
                  @if (!state.readOnly() && (invitation.status === 'Pending' || invitation.status === 'Expired')) {
                    <span><button type="button" class="btn-quiet" (click)="resend(invitation)">Yeniden gönder</button>
                    @if (invitation.status === 'Pending') { <button type="button" class="btn-quiet" (click)="cancelEmail(invitation)">Daveti geri al</button> }</span>
                  }</li>
              }
              @for (invitation of invitations(); track invitation.id) {
                <li><span>Hesaba gönderildi, {{ date(invitation.expiresAt) }} tarihine kadar geçerli</span>
                  @if (!state.readOnly()) { <button type="button" class="btn-quiet" (click)="cancel(invitation)">Daveti geri al</button> }</li>
              }
            </ul>
          </section>
        }
        <section class="card" aria-labelledby="leave-title">
          <div class="card-head"><h2 id="leave-title">İzinler</h2><span class="muted">Önümüzdeki 90 gün</span></div>
          <ul class="plain">
            @for (item of timeOff(); track item.id) {
              <li><span><strong>{{ state.teacherName(item.teacherUserId) }}</strong> {{ dateTime(item.startsAt) }} – {{ dateTime(item.endsAt) }}{{ item.reason ? ', ' + item.reason : '' }}</span>
                @if (!state.readOnly()) { <button type="button" class="btn-quiet" (click)="removeLeave(item)">Kaldır</button> }</li>
            } @empty { <li class="muted">Kayıtlı izin yok. Takvimde sağ tıklayarak izin ekleyebilirsiniz.</li> }
          </ul>
        </section>
      </div>
      @if (!state.readOnly()) {
        <div class="side stack">
          <form class="card card-pad stack" style="gap: 14px" (ngSubmit)="invite()">
            <h2>Öğretmen davet et</h2>
            <label class="field">Öğretmenin e-posta adresi<input type="email" name="email" [(ngModel)]="email" autocomplete="off">
              <span class="hint">Bu adrese davet bağlantısı gider. Hesabı yoksa bağlantıdan birkaç adımda açar.</span></label>
            <div><button type="submit" class="btn" [disabled]="busy() || !email.includes('@')">Davet gönder</button></div>
          </form>
          <form class="card card-pad stack" style="gap: 14px" (ngSubmit)="addHours()">
            <h2>Haftalık çalışma saati</h2>
            <p class="muted" style="margin: 0; font-size: 14px">Saat tanımlanan öğretmene yalnız bu saatlerde ders konabilir.</p>
            <label class="field">Öğretmen<select name="teacher" [(ngModel)]="hoursTeacher">
              @for (member of activeMembers(); track member.userId) { <option [value]="member.userId">{{ state.teacherName(member.userId) }}</option> }</select></label>
            <label class="field">Gün<select name="weekday" [(ngModel)]="weekday">@for (name of weekdays; track name; let i = $index) { <option [ngValue]="i">{{ name }}</option> }</select></label>
            <div class="form-grid"><label class="field">Başlangıç<input type="time" name="from" [(ngModel)]="from"></label>
              <label class="field">Bitiş<input type="time" name="to" [(ngModel)]="to"></label></div>
            <div><button type="submit" class="btn-secondary" [disabled]="busy() || !hoursTeacher || to <= from">Saati ekle</button></div>
            @if (windowsOf(hoursTeacher).length) {
              <ul class="plain">@for (window of windowsOf(hoursTeacher); track window.id) {
                <li><span>{{ weekdays[window.weekday] }} {{ window.startTime.slice(0, 5) }}–{{ window.endTime.slice(0, 5) }}</span>
                  <button type="button" class="btn-quiet" (click)="removeHours(window)">Kaldır</button></li> }</ul>
            }
          </form>
        </div>
      }
    </div>
  `,
  styles: `
    .plain { list-style: none; margin: 0; padding: 0; }
    .plain li { display: flex; flex-wrap: wrap; justify-content: space-between; align-items: center; gap: 8px; padding: 10px 20px; border-top: 1px solid var(--ds-line); }
    .side .plain li { padding: 8px 0; }
  `
})
export class TeachersSectionComponent {
  readonly state = inject(CoursePanelState);
  private readonly confirm = inject(PanelConfirm);
  private readonly api = inject(CoursePanelApi);
  private readonly service = inject(CourseManagementService);
  readonly weekdays = WEEKDAYS;
  readonly invitations = signal<CourseInvitation[]>([]);
  readonly emailInvitations = signal<PanelTeacherInvitation[]>([]);
  readonly availability = signal<TeacherAvailabilityWindow[]>([]);
  readonly timeOff = signal<TeacherTimeOff[]>([]);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly notice = signal('');
  readonly date = formatDate;
  readonly dateTime = formatDateTime;
  email = ''; hoursTeacher = ''; weekday = 0; from = '09:00'; to = '18:00';

  constructor() { effect(() => { this.state.id(); this.state.version(); untracked(() => void this.load()); }); }

  activeMembers() { return this.state.members().filter(member => member.isActive && member.role !== 'Student'); }
  windowsOf(userId: string) { return this.availability().filter(window => window.teacherUserId === userId).sort((a, b) => a.weekday - b.weekday || a.startTime.localeCompare(b.startTime)); }
  hoursLine(userId: string): string {
    const windows = this.windowsOf(userId);
    return windows.length ? windows.map(window => `${WEEKDAYS[window.weekday].slice(0, 3)} ${window.startTime.slice(0, 5)}–${window.endTime.slice(0, 5)}`).join(', ') : 'Sınır yok';
  }

  async load(): Promise<void> {
    const id = this.state.id();
    if (!id) return;
    try {
      const [invitations, availability, timeOff, emailInvitations] = await Promise.all([
        this.service.getInvitations(id), this.service.getTeacherAvailability(id), this.service.getTeacherTimeOff(id),
        this.api.teacherInvitations(id).catch(() => [] as PanelTeacherInvitation[])]);
      this.invitations.set(invitations.filter(item => item.status === 'Pending' && item.role === 'Teacher'));
      // Accepted invitations show up among the members; the rest stay here so they can be sent again.
      this.emailInvitations.set(emailInvitations.filter(item => item.status !== 'Accepted' && item.status !== 'Cancelled'));
      this.availability.set(availability);
      this.timeOff.set(timeOff);
      if (!this.hoursTeacher) this.hoursTeacher = this.activeMembers()[0]?.userId ?? '';
    } catch (error) { this.error.set(problemText(error, 'Öğretmen bilgileri yüklenemedi.')); }
  }

  private async act(work: () => Promise<void>, done: string): Promise<void> {
    this.busy.set(true); this.error.set('');
    try { await work(); this.notice.set(done); await this.state.refreshLists(); await this.load(); }
    catch (error) { this.error.set(problemText(error)); }
    finally { this.busy.set(false); }
  }

  invite(): Promise<void> {
    return this.act(async () => { await this.api.inviteTeacher(this.state.id(), this.email.trim()); this.email = ''; },
      'Davet gönderildi. Öğretmen e-postadaki bağlantıyı açıp daveti kabul edince listede görünür.');
  }
  statusLine(invitation: PanelTeacherInvitation): string {
    return invitation.status === 'Expired' ? 'süresi doldu' : `bekliyor, ${this.date(invitation.expiresAt)} tarihine kadar geçerli`;
  }
  resend(invitation: PanelTeacherInvitation): Promise<void> {
    return this.act(() => this.api.inviteTeacher(this.state.id(), invitation.email).then(() => undefined), 'Davet yeniden gönderildi; önceki bağlantı artık açılmaz.');
  }
  cancelEmail(invitation: PanelTeacherInvitation): Promise<void> {
    return this.act(() => this.api.cancelTeacherInvitation(this.state.id(), invitation.id), 'Davet geri alındı.');
  }
  cancel(invitation: CourseInvitation): Promise<void> {
    return this.act(() => this.service.cancel(this.state.id(), invitation.id).then(() => undefined), 'Davet geri alındı.');
  }
  async revoke(userId: string): Promise<void> {
    if (!await this.confirm.ask(`${this.state.teacherName(userId)} kurumdan çıkarılır. Geçmiş dersleri ve yoklamaları kalır.`,
      { title: 'Öğretmen kurumdan çıkarılsın mı?', confirmText: 'Kurumdan çıkar', danger: true })) return;
    return this.act(() => this.service.revoke(this.state.id(), userId).then(() => undefined), 'Öğretmen kurumdan çıkarıldı.');
  }
  async addHours(): Promise<void> {
    // Once a teacher has working hours, lessons outside them stay but no new ones can be added there; say how many
    // planned lessons that is before saving (20.11.2026 live test: hours were added over lessons without a word).
    const outside = await this.lessonsOutside().catch(() => 0);
    if (outside > 0 && !await this.confirm.ask(
      `${this.state.teacherName(this.hoursTeacher)} öğretmeninin önümüzdeki 60 gündeki ${outside} dersi bu çalışma saatlerinin dışında kalıyor. Dersler yerinde kalır, iptal edilmez; ama bu saatlere yeni ders eklenemez.`,
      { title: 'Planlı dersler çalışma saati dışında kalacak', confirmText: 'Yine de ekle' })) return;
    return this.act(() => this.service.createTeacherAvailability(this.state.id(), this.hoursTeacher, this.weekday, this.from, this.to, 'Europe/Istanbul')
      .then(() => undefined), 'Çalışma saati eklendi.');
  }
  /** Planned lessons of the chosen teacher in the next 60 days that fall outside the hours with the new one added. */
  private async lessonsOutside(): Promise<number> {
    const toMinutes = (value: string) => Number(value.slice(0, 2)) * 60 + Number(value.slice(3, 5));
    const windows = [...this.windowsOf(this.hoursTeacher).map(window => ({ weekday: window.weekday, from: toMinutes(window.startTime), to: toMinutes(window.endTime) })),
      { weekday: this.weekday, from: toMinutes(this.from), to: toMinutes(this.to) }];
    const now = Date.now();
    const lessons = await this.api.lessons(this.state.id(), new Date(now), new Date(now + 60 * 86_400_000));
    const local = (value: string) => new Date(new Date(value).getTime() + 3 * 3_600_000);
    return lessons.filter(lesson => lesson.teacherUserId === this.hoursTeacher && lesson.status === 'Scheduled').filter(lesson => {
      const start = local(lesson.startsAt); const end = local(lesson.endsAt);
      const weekday = (start.getUTCDay() + 6) % 7;
      const from = start.getUTCHours() * 60 + start.getUTCMinutes(); const to = end.getUTCHours() * 60 + end.getUTCMinutes() || 24 * 60;
      return !windows.some(window => window.weekday === weekday && window.from <= from && to <= window.to);
    }).length;
  }
  removeHours(window: TeacherAvailabilityWindow): Promise<void> {
    return this.act(() => this.service.deleteTeacherAvailability(this.state.id(), window.id).then(() => undefined), 'Çalışma saati kaldırıldı.');
  }
  removeLeave(item: TeacherTimeOff): Promise<void> {
    return this.act(() => this.service.deleteTeacherTimeOff(this.state.id(), item.id).then(() => undefined), 'İzin kaldırıldı.');
  }
}
