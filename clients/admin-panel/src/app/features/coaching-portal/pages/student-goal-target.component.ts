import { CommonModule } from '@angular/common';
import { Component, DestroyRef, HostListener, Input, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Observable, finalize, map } from 'rxjs';
import { CoachingStudyPlanningService, GoalTarget, TargetPage } from '../../../core/services/coaching-study-planning.service';

interface TargetResult { id: string; label: string; detail: string; minimumScore: number | null; scoreYear: number | null }
@Component({
  selector: 'app-student-goal-target', standalone: true, imports: [CommonModule, FormsModule],
  templateUrl: './student-goal-target.component.html'
})
export class StudentGoalTargetComponent {
  @Input({ required: true }) goalId = '';
  private readonly service = inject(CoachingStudyPlanningService);
  private readonly destroyRef = inject(DestroyRef);
  readonly expanded = signal(false);
  readonly busy = signal(false);
  readonly saving = signal(false);
  readonly target = signal<GoalTarget | null>(null);
  readonly results = signal<TargetResult[]>([]);
  readonly error = signal('');
  readonly success = signal('');
  readonly stale = signal(false);
  kind: 'school' | 'program' = 'school';
  query = ''; city = ''; district = ''; scoreType = '';
  page = 1; total = 0; searched = false;

  open() {
    if (this.busy() || this.saving()) return;
    this.expanded.set(true); this.target.set(null); this.results.set([]);
    this.stale.set(false); this.error.set(''); this.success.set(''); this.searched = false;
    this.busy.set(true);
    this.service.getGoalTarget(this.goalId).pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.busy.set(false))).subscribe({
      next: value => this.target.set(value),
      error: () => this.error.set('Hedef bilgisi yüklenemedi. Yeniden yükleyebilirsiniz.')
    });
  }
  changeKind(kind: 'school' | 'program') {
    if (this.busy() || this.saving()) return;
    this.kind = kind; this.results.set([]); this.page = 1; this.total = 0; this.searched = false;
  }
  searchTargets(page = 1) {
    if (!this.target()?.canEdit || this.busy() || this.saving() || this.stale()) return;
    this.results.set([]); this.error.set(''); this.success.set(''); this.busy.set(true); this.searched = false;
    const search: Observable<TargetPage<TargetResult>> = this.kind === 'school'
      ? this.service.searchSchools(this.query, this.city, this.district, page).pipe(map(value => ({ ...value, items: value.items.map(x => ({ ...x, label: x.name, detail: `${x.city} / ${x.district}` })) })))
      : this.service.searchPrograms(this.query, this.scoreType, page).pipe(map(value => ({ ...value, items: value.items.map(x => ({ ...x, label: `${x.universityName} — ${x.name}`, detail: [x.programCode, x.scoreType].filter(Boolean).join(' · ') })) })));
    search.pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.busy.set(false))).subscribe({
      next: (value: TargetPage<TargetResult>) => { this.results.set(value.items); this.page = value.pageNumber; this.total = value.totalCount; this.searched = true; },
      error: () => this.error.set('Arama sonuçları yüklenemedi. Filtreleri kontrol edip yeniden deneyin.')
    });
  }
  choose(id: string) {
    if (!this.results().some(x => x.id === id)) return;
    this.save(this.kind === 'program' ? id : null, this.kind === 'school' ? id : null);
  }
  clear() { this.save(null, null); }
  private save(programId: string | null, schoolId: string | null) {
    const target = this.target();
    if (!target?.canEdit || this.busy() || this.saving() || this.stale()) return;
    this.saving.set(true); this.error.set(''); this.success.set('');
    this.service.saveGoalTarget(this.goalId, target.version, programId, schoolId)
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.saving.set(false))).subscribe({
        next: value => { this.target.set(value); this.success.set(programId || schoolId ? 'Hedef bağlantısı kaydedildi.' : 'Hedef bağlantısı kaldırıldı.'); },
        error: err => {
          this.stale.set(err.status === 409);
          this.error.set(err.status === 409 ? 'Hedef değişti. Güncel bilgileri yeniden yükleyin.' : 'Bağlantı kaydedilemedi. Seçilen kayıt artık aktif olmayabilir; yeniden arayın.');
        }
      });
  }
  canLeavePage() { return !this.saving(); }
  @HostListener('window:beforeunload', ['$event'])
  beforeUnload(event: BeforeUnloadEvent) {
    if (this.saving()) { event.preventDefault(); event.returnValue = ''; }
  }
}
