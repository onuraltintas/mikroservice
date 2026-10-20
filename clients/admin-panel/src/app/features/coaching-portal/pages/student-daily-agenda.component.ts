import { CommonModule } from '@angular/common';
import { Component, DestroyRef, HostListener, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { Subscription, finalize } from 'rxjs';
import { AgendaMode, CoachingDailyAgendaService, DailyAgendaPage } from '../../../core/services/coaching-daily-agenda.service';
import { CoachingIconComponent } from '../coaching-icon.component';
import { coachingFeedback } from '../coaching-feedback';
import { clock } from './weekly-calendar';

@Component({ selector: 'app-student-daily-agenda', standalone: true,
  imports: [CommonModule, RouterLink, CoachingIconComponent],
  templateUrl: './student-daily-agenda.component.html', styleUrl: './student-daily-agenda.component.scss' })
export class StudentDailyAgendaComponent {
  private readonly api = inject(CoachingDailyAgendaService);
  private readonly destroyRef = inject(DestroyRef);
  private request?: Subscription;
  private page = 1;
  readonly mode = signal<AgendaMode>('today');
  readonly data = signal<DailyAgendaPage | null>(null);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly totalPages = computed(() => Math.max(1, Math.ceil((this.data()?.totalCount ?? 0) / (this.data()?.pageSize ?? 20))));
  readonly filters = [ { key: 'today', label: 'Bugün', count: 'todayCount' },
    { key: 'upcoming', label: 'Yaklaşan', count: 'upcomingCount' },
    { key: 'overdue', label: 'Geciken', count: 'overdueCount' } ] as const;
  readonly labels = { study: 'Çalışma', assignment: 'Ödev', decision: 'Karar' };
  readonly clock = clock;
  ngOnInit() { this.load(); }
  select(mode: AgendaMode) { if (mode !== this.mode()) { this.mode.set(mode); this.load(1); } }
  load(page = 1) {
    this.request?.unsubscribe(); this.page = page; this.data.set(null); this.error.set(''); this.loading.set(true);
    this.request = this.api.get(this.mode(), page).pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.loading.set(false))).subscribe({
      next: value => { this.page = value.pageNumber; this.data.set(value); },
      error: () => this.error.set('Günlük işlerin yüklenemedi. Bağlantını ve oturumunu kontrol edip yeniden deneyebilirsin.')
    });
  }
  refresh() { this.load(this.page); }
  @HostListener('window:focus') onFocus() { if (!this.loading()) this.refresh(); }
  private readonly feedback = coachingFeedback(undefined, this.error);
}
