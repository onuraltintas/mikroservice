import { CommonModule } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { finalize, forkJoin } from 'rxjs';
import { environment } from '../../../environments/environment';
import { StaffMySubscriptionComponent } from '../../shared/billing/my-subscription.component';
import { SpeedReadingInstitutionMember, SpeedReadingInstitutionService } from './speed-reading-institution.service';

export interface InstitutionSeat { userId: string; isSuspended: boolean; assignedAt: string; isCovered: boolean }
export interface InstitutionSeatPool {
  subscriptionId: string; planName: string; endsAt: string; capacity: number; used: number; extraSeats: number;
  extraSeatsUntil: string | null; extraSeatPrice: number | null; seats: InstitutionSeat[] | null;
}
export interface InstitutionSeats { enabled: boolean; pool: InstitutionSeatPool | null }

/**
 * The institution's Hızlı Okuma plan and who sits on its seats. The plan is bought (and extra seats added) in the
 * shared "Aboneliğim" page above; a student trains through the institution only while holding a seat.
 */
@Component({
  selector: 'staff-speed-reading-institution-seats',
  standalone: true,
  imports: [CommonModule, StaffMySubscriptionComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="seats" aria-labelledby="institution-seats-title">
      <h3 id="institution-seats-title">Abonelik ve koltuklar</h3>
      @if (seats(); as data) {
        @if (!data.enabled) {
          <p class="seats-muted">Bu kurulumda abonelik yönetimi açık değil.</p>
        } @else {
          <staff-my-subscription product="speed-reading" [roles]="['InstitutionAdmin']" />
          @if (data.pool; as pool) {
            <p data-testid="seat-summary"><strong>{{ pool.used }} / {{ pool.capacity }}</strong> koltuk dolu · {{ pool.planName }} ·
              {{ pool.endsAt | date:'dd.MM.yyyy' }} tarihine kadar</p>
          } @else {
            <p class="seats-muted" data-testid="seat-summary">Kurumun süren bir Hızlı Okuma planı yok. Koltuk vermek için önce yukarıdan bir kurum planı alın.</p>
          }
          @if (error()) { <div class="seats-error" role="alert">{{ error() }}</div> }
          <div class="seats-scroll">
            <table>
              <thead><tr><th>Öğrenci</th><th>Koltuk</th><th></th></tr></thead>
              <tbody>
                @for (row of rows(); track row.userId) {
                  <tr>
                    <td><strong>{{ row.name }}</strong>@if (!row.isMember) { <span class="seats-muted"> · kurumdan ayrılmış</span> }</td>
                    <td>{{ row.seat ? (row.seat.isCovered ? 'Koltuğu var' : 'Kapasite dışı (erişimi yok)') : 'Koltuğu yok' }}</td>
                    <td class="seats-actions">
                      @if (row.seat) {
                        <button type="button" [disabled]="busy()" (click)="change(row.userId, false)">Koltuğu geri al</button>
                      } @else if (data.pool) {
                        <button type="button" [disabled]="busy() || data.pool.used >= data.pool.capacity" (click)="change(row.userId, true)">Koltuk ver</button>
                      }
                    </td>
                  </tr>
                } @empty { <tr><td colspan="3" class="seats-muted">{{ loading() ? 'Yükleniyor…' : 'Kurumda etkin öğrenci yok.' }}</td></tr> }
              </tbody>
            </table>
          </div>
          @if (moreStudents()) { <p class="seats-muted">İlk 100 öğrenci gösteriliyor.</p> }
        }
      } @else if (error()) {
        <div class="seats-error" role="alert">{{ error() }} <button type="button" (click)="load()">Tekrar dene</button></div>
      } @else {
        <p class="seats-muted" role="status">Yükleniyor…</p>
      }
    </section>
  `,
  styles: [`
    .seats { display: grid; gap: 1rem; margin: 1rem 0; padding: 1rem; border: 1px solid var(--ui-brand-soft); border-radius: .75rem; background: var(--ui-surface); }
    h3 { margin: 0; font-size: 1.125rem; }
    .seats-muted { color: var(--ui-text-muted); font-size: .875rem; }
    .seats-error { padding: .75rem 1rem; border-radius: .5rem; background: var(--ui-danger-soft); color: var(--ui-danger); }
    .seats-scroll { overflow-x: auto; }
    table { width: 100%; border-collapse: collapse; font-size: .875rem; text-align: left; }
    th, td { padding: .625rem .75rem; border-bottom: 1px solid var(--ui-brand-soft); }
    .seats-actions { text-align: right; white-space: nowrap; }
    button { min-height: 2.75rem; padding: .5rem 1rem; border: 1px solid var(--ui-border); border-radius: .5rem; background: var(--ui-surface); font: inherit; cursor: pointer; }
    button:disabled { opacity: .55; cursor: not-allowed; }
    button:focus-visible { outline: 3px solid var(--ui-warning); outline-offset: 2px; }
  `]
})
export class SpeedReadingInstitutionSeatsComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly institutions = inject(SpeedReadingInstitutionService);

  readonly institutionId = input.required<string>();
  readonly seats = signal<InstitutionSeats | null>(null);
  readonly students = signal<SpeedReadingInstitutionMember[]>([]);
  readonly moreStudents = signal(false);
  readonly loading = signal(false);
  readonly busy = signal(false);
  readonly error = signal('');

  /** The institution's active students, then whoever still holds a seat without being one. */
  readonly rows = computed(() => {
    const held = new Map((this.seats()?.pool?.seats ?? []).filter(seat => !seat.isSuspended).map(seat => [seat.userId, seat]));
    const members = this.students().map(student => ({
      userId: student.userId, name: `${student.firstName} ${student.lastName}`.trim() || student.email || student.userId,
      isMember: true, seat: held.get(student.userId) ?? null
    }));
    const known = new Set(members.map(member => member.userId));
    const others = [...held.values()].filter(seat => !known.has(seat.userId))
      .map(seat => ({ userId: seat.userId, name: seat.userId, isMember: false, seat: seat as InstitutionSeat | null }));
    return [...members, ...others];
  });

  private get url(): string { return `${environment.apiUrl}/speed-reading/institutions/${encodeURIComponent(this.institutionId())}/seats`; }

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading.set(true);
    this.error.set('');
    forkJoin({
      seats: this.http.get<InstitutionSeats>(this.url),
      students: this.institutions.getMembers(this.institutionId(), 'Student', 1, 100, { isActive: true })
    }).pipe(finalize(() => this.loading.set(false))).subscribe({
      next: ({ seats, students }) => {
        this.seats.set(seats);
        this.students.set(students.items);
        this.moreStudents.set(students.totalCount > students.items.length);
      },
      error: error => this.error.set(seatError(error, 'Koltuklar yüklenemedi.'))
    });
  }

  change(userId: string, give: boolean): void {
    this.busy.set(true);
    this.error.set('');
    const call = give ? this.http.put<InstitutionSeats>(`${this.url}/${encodeURIComponent(userId)}`, {}) : this.http.delete<InstitutionSeats>(`${this.url}/${encodeURIComponent(userId)}`);
    call.pipe(finalize(() => this.busy.set(false))).subscribe({
      next: seats => this.seats.set(seats),
      error: error => this.error.set(seatError(error, give ? 'Koltuk verilemedi.' : 'Koltuk geri alınamadı.'))
    });
  }
}

/** The service's own sentence when it gave one. */
export function seatError(error: unknown, fallback: string): string {
  const message = error instanceof HttpErrorResponse ? error.error?.message : null;
  return typeof message === 'string' && message.trim() ? message : fallback;
}
