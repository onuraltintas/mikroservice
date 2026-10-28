import { HttpClient } from '@angular/common/http';
import { Component, Input, OnChanges, computed, signal } from '@angular/core';
import { Observable } from 'rxjs';

export interface MotivationBadge { code: string; label: string; description: string; earnedOn: string | null; }
export interface MotivationCelebration { goalId: string; goalTitle: string; completedAt: string; }
export interface StudentMotivation {
  studentId: string; currentStreak: number; longestStreak: number; activeToday: boolean; lastActiveDay: string | null;
  timeZoneId: string; badges: MotivationBadge[]; celebration: MotivationCelebration | null;
}

/** Roadmap D5: study streak, badges and goal celebration (`me` for the student, otherwise a student id). */
export class CoachingMotivationApi {
  constructor(private readonly http: HttpClient, private readonly apiUrl: string) {}
  get(student: 'me' | string): Observable<StudentMotivation> {
    return this.http.get<StudentMotivation>(`${this.apiUrl}/coaching/students/${student === 'me' ? 'me' : encodeURIComponent(student)}/motivation`);
  }
}

const DISMISSED = 'coaching.celebration.dismissed';

@Component({
  selector: 'edu-coaching-motivation', standalone: true,
  styles: [`
    :host{display:block;border:1px solid var(--ui-border,#e2e8f0);border-radius:.75rem;padding:1rem;margin:1rem 0}
    h2{margin:0;font-size:1rem;font-weight:600}
    p{margin:.25rem 0;font-size:.86rem;line-height:1.5}
    .muted{color:var(--ui-text-muted,#64748b);font-size:.8rem}
    .streak{display:flex;flex-wrap:wrap;align-items:baseline;gap:.35rem 1rem;margin:.4rem 0}
    .streak strong{font-size:1.6rem;line-height:1}
    ul{list-style:none;margin:.6rem 0 0;padding:0;display:flex;flex-wrap:wrap;gap:.4rem}
    li{border:1px solid var(--ui-border,#e2e8f0);border-radius:999px;padding:.2rem .65rem;font-size:.78rem;font-weight:600}
    li.locked{opacity:.5;font-weight:500}
    .celebrate{border-radius:.6rem;padding:.6rem .8rem;margin-bottom:.6rem;background:var(--ui-success-soft,#ecfdf5);color:var(--ui-success,#047857);display:flex;flex-wrap:wrap;gap:.5rem;align-items:center;justify-content:space-between}
    button{min-height:2rem;border:1px solid currentColor;border-radius:.5rem;background:transparent;color:inherit;padding:.2rem .6rem;font:inherit;font-size:.78rem;font-weight:600;cursor:pointer}
  `],
  template: `
    @if (celebration(); as goal) {
      <div class="celebrate" role="status" data-testid="motivation-celebration">
        <span>🎉 {{ own ? 'Tebrikler! "' + goal.goalTitle + '" hedefine ulaştın.' : '"' + goal.goalTitle + '" hedefine ulaşıldı.' }}</span>
        @if (own) { <button type="button" (click)="dismiss(goal.goalId)">Kapat</button> }
      </div>
    }
    <h2>{{ own ? 'Çalışma serin' : 'Çalışma serisi' }}</h2>
    @if (error()) { <p class="muted" role="alert">{{ error() }}</p> }
    @else if (data(); as m) {
      <div class="streak" data-testid="motivation-streak">
        <strong>{{ m.currentStreak }}</strong><span>günlük seri</span>
        <span class="muted">En uzun: {{ m.longestStreak }} gün</span>
      </div>
      <p class="muted">{{ hint() }}</p>
      <ul aria-label="Rozetler">
        @for (badge of m.badges; track badge.code) {
          <li [class.locked]="!badge.earnedOn" [attr.data-testid]="'badge-' + badge.code" [title]="badge.description">
            {{ badge.label }}<span class="muted">{{ badge.earnedOn ? '' : ' · henüz yok' }}</span>
          </li>
        }
      </ul>
    } @else { <p class="muted" role="status">Yükleniyor…</p> }
  `
})
export class CoachingMotivationComponent implements OnChanges {
  @Input({ required: true }) api!: CoachingMotivationApi;
  /** `me` on the student's own screens; a student id on parent and teacher screens. */
  @Input() student: 'me' | string = 'me';
  get own(): boolean { return this.student === 'me'; }

  readonly data = signal<StudentMotivation | null>(null);
  readonly error = signal('');
  private readonly dismissed = signal<string[]>(readDismissed());
  readonly celebration = computed(() => {
    const goal = this.data()?.celebration;
    return goal && !(this.own && this.dismissed().includes(goal.goalId)) ? goal : null;
  });
  readonly hint = computed(() => {
    const m = this.data(); if (!m) return '';
    if (m.activeToday) return this.own ? 'Bugünkü adımını attın. Yarın da devam et!' : 'Bugün çalıştı.';
    if (m.currentStreak > 0) return this.own ? 'Seriyi sürdürmek için bugün bir görev tamamla ya da bir ödev teslim et.' : 'Bugün henüz çalışma kaydı yok.';
    return this.own ? 'Plandan bir görev tamamladığın ya da ödev teslim ettiğin her gün seriye sayılır.' : 'Şu an süren bir seri yok.';
  });

  ngOnChanges(): void {
    this.error.set(''); this.data.set(null);
    this.api.get(this.student).subscribe({
      next: value => this.data.set(value),
      error: () => this.error.set('Çalışma serisi yüklenemedi.')
    });
  }

  dismiss(goalId: string): void {
    const next = [...this.dismissed(), goalId].slice(-20);
    this.dismissed.set(next);
    try { localStorage.setItem(DISMISSED, JSON.stringify(next)); } catch { /* storage may be unavailable */ }
  }
}

function readDismissed(): string[] {
  try {
    const value = JSON.parse(localStorage.getItem(DISMISSED) ?? '[]');
    return Array.isArray(value) ? value.filter(x => typeof x === 'string') : [];
  } catch { return []; }
}
