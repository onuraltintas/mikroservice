import { Component, DestroyRef, OnInit, inject, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize, forkJoin } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { StudentProgramInfo } from '../../../core/services/student-program.service';

interface TrainingTemplate { id: string; name: string; description: string; totalDays: number; programType: number; examType?: string; initialDifficultyLevel?: number; maxDifficultyLevel?: number; totalWeeks?: number; }

@Component({
  selector: 'app-training-programs', standalone: true, imports: [RouterLink],
  template: `
    <section>
      <header class="training-header"><h1>Eğitim programları</h1>
      <p>Personel eğitimi: günün tüm egzersizlerini bitirince sonraki gün hemen açılır.
        Aktif programı tamamladıktan sonra istediğiniz programı yeniden veya ilk kez başlatabilirsiniz.</p></header>
      @if (loading()) { <p role="status" class="state-line">Programlar yükleniyor…</p> }
      @if (error()) { <p role="alert" class="state-line state-line--error">{{ error() }}</p><button type="button" class="ghost-action" (click)="load()">Yeniden dene</button> }
      @if (active(); as program) {
        <aside class="active-program sp-rise"><div class="active-program__body"><span class="status-badge">Aktif eğitiminiz</span><h2>{{ program.templateName }}</h2>
          <div class="progress-stats"><span>Hafta <strong>{{ program.currentWeek }}</strong></span><span>Gün <strong>{{ program.currentDay }}</strong></span><span><strong>{{ program.totalDaysCompleted }}</strong> gün tamamlandı</span></div>
          @if (activeTotalDays(); as total) {
            <div class="active-progress" role="progressbar" [attr.aria-valuenow]="program.totalDaysCompleted" aria-valuemin="0" [attr.aria-valuemax]="total"
              [attr.aria-label]="'Tamamlanan gün: ' + program.totalDaysCompleted + '/' + total">
              <span class="active-progress__fill sp-grow-x" [style.width.%]="activePercent()"></span></div>
          }
          <p class="muted">Kaldığınız yerden devam edin. Günün egzersizleri bitince sonraki gün açılır.</p></div>
          <a class="primary-action" routerLink="/student/daily-exercises">Eğitime devam et</a></aside>
      }
      <div class="catalog-heading"><h2>Size uygun programı seçin</h2><span>{{ filteredTemplates().length }} program</span></div>
      <div class="catalog-toolbar"><label for="program-age">Yaş grubu</label><select id="program-age" [value]="selectedAge()" (change)="selectedAge.set($any($event.target).value)"><option value="">Tüm yaş grupları</option>@for (age of ageGroups; track age) { <option [value]="age">{{ age }}</option> }</select><span>Yaş grubu ve seviyeye göre sıralı</span></div>
      @if (active()) { <p class="enrollment-notice">Önce aktif programınızı tamamlayın; ardından aşağıdaki programlardan birini başlatabilirsiniz.</p> }
      <div class="program-grid">
        @for (program of filteredTemplates(); track program.id; let i = $index) {
          <article class="program-card sp-rise" [attr.data-tone]="tone(program)" [style.--sp-i]="i"><div class="card-meta"><span class="age-badge">{{ ageGroup(program) }}</span><span class="level-badge">Seviye {{ level(program) }}</span>@if (program.examType) { <span class="exam-badge">{{ program.examType }}</span> }</div>
            <h3>{{ program.name }}</h3><dl class="program-facts"><div><dt>Süre</dt><dd>{{ program.totalDays }} gün</dd></div><div><dt>Hafta</dt><dd>{{ program.totalWeeks || 4 }}</dd></div><div><dt>Seviye</dt><dd>{{ level(program) }}–{{ program.maxDifficultyLevel || level(program) }}</dd></div></dl><details class="description"><summary>Program ayrıntıları</summary><p>{{ program.description }}</p></details>
            <button class="card-action" type="button" [disabled]="busy() || !!active()" (click)="start(program.id)">{{ active() ? 'Aktif programın ardından' : busy() ? 'Program başlatılıyor…' : 'Programa kaydol ve başla' }}</button>
          </article>
        }
      </div>
      @if (!loading() && !error() && !filteredTemplates().length) { <p class="empty-state" role="status">Bu yaş grubunda program bulunamadı. Başka bir yaş grubu seçin.</p> }
      @if (history().length) {
        <h2 class="history-title">Program geçmişim</h2>
        <ul class="history-list">
        @for (program of history(); track program.progressId) {
          <li class="history-row"><span>{{ program.templateName }}</span><span class="history-row__meta">{{ program.totalDaysCompleted }} gün tamamlandı@if (program.completedDate) {<span class="done-chip">Tamamlandı</span>}</span></li>
        }
        </ul>
      }
    </section>
  `,
  styles: [`
    :host{display:block;color:var(--sp-text-1)}
    section{max-width:1200px;margin:auto;padding:32px 24px}
    .training-header{display:flex;flex-direction:column;align-items:flex-start;gap:8px;margin-bottom:24px;max-width:760px}
    .training-header h1{margin:0;font-size:clamp(26px,3vw,34px);font-weight:700;line-height:1.2}
    .training-header p{margin:0;font-size:15px;line-height:1.65;color:var(--sp-text-2)}
    h2{font-size:22px;font-weight:700;line-height:1.35;margin:0}
    .state-line{color:var(--sp-text-2)}.state-line--error{color:var(--sp-danger)}
    .ghost-action{min-height:44px;padding:10px 16px;border:1px solid var(--sp-border-strong,var(--sp-border));border-radius:999px;background:var(--sp-surface);color:var(--sp-text-1);font:inherit;font-weight:600;cursor:pointer}
    .active-program{position:relative;display:flex;align-items:center;justify-content:space-between;gap:24px;padding:26px 28px;margin-bottom:32px;border-radius:var(--sp-radius-card-lg,22px);background:var(--sp-tone-mint-soft);box-shadow:var(--sp-shadow-raised,var(--sp-shadow-card));overflow:hidden}
    .active-program::after{content:'';position:absolute;left:28px;right:28px;bottom:12px;height:3px;border-radius:3px;background:var(--sp-focus-line)}
    .active-program__body{min-width:0;flex:1}
    .active-program h2{margin:10px 0 12px;font-size:21px;max-width:720px}
    .status-badge{display:inline-block;padding:4px 10px;border-radius:999px;background:var(--sp-surface);color:var(--sp-tone-mint);font-size:13px;font-weight:700}
    .progress-stats{display:flex;flex-wrap:wrap;gap:8px 22px;font-size:14px;color:var(--sp-text-2)}.progress-stats strong{color:var(--sp-text-1);font-weight:700}
    .active-progress{height:10px;margin-top:14px;border-radius:999px;background:var(--sp-surface);overflow:hidden;max-width:520px}
    .active-progress__fill{display:block;height:100%;border-radius:inherit;background:var(--sp-primary);transform-origin:left center}
    .muted{margin:12px 0 4px;font-size:13px;color:var(--sp-text-2)}
    .primary-action{display:inline-flex;align-items:center;justify-content:center;flex-shrink:0;min-height:48px;padding:12px 22px;border-radius:999px;background:var(--sp-primary);color:var(--sp-on-primary);font-size:15px;font-weight:700;text-decoration:none}
    .primary-action:hover{background:var(--sp-primary-hover,var(--sp-primary))}
    .primary-action:focus-visible,.card-action:focus-visible,.ghost-action:focus-visible,select:focus-visible,summary:focus-visible{outline:3px solid var(--sp-focus-line);outline-offset:3px}
    .catalog-heading{display:flex;align-items:baseline;justify-content:space-between;gap:16px}.catalog-heading span{flex-shrink:0;white-space:nowrap;font-size:14px;color:var(--sp-text-2)}
    .catalog-toolbar{display:flex;align-items:center;gap:12px;margin:16px 0 20px;color:var(--sp-text-2);font-size:13px}
    .catalog-toolbar label{font-weight:700;color:var(--sp-text-1)}
    .catalog-toolbar select{min-height:44px;min-width:200px;padding:10px 36px 10px 14px;font:inherit;color:var(--sp-text-1);background:var(--sp-surface);border:1px solid var(--sp-border-strong,var(--sp-border));border-radius:12px}
    .catalog-toolbar>span{margin-left:auto}
    .enrollment-notice{margin:0 0 20px;padding:12px 16px;border-radius:14px;background:var(--sp-tone-sand-soft);color:var(--sp-text-1);font-size:14px}
    .program-grid{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:16px;margin-bottom:32px}
    .program-card{--tone:var(--sp-tone-mint);--tone-soft:var(--sp-tone-mint-soft);display:flex;flex-direction:column;min-width:0;padding:22px;border-radius:var(--sp-radius-card-lg,22px);background:var(--sp-surface);box-shadow:var(--sp-shadow-raised,var(--sp-shadow-card));border-top:5px solid var(--tone);transition:transform .2s ease,box-shadow .2s ease}
    .program-card:hover{transform:translateY(-3px);box-shadow:var(--sp-shadow-raised-hover,var(--sp-shadow-hover))}
    .program-card[data-tone=peach]{--tone:var(--sp-tone-peach);--tone-soft:var(--sp-tone-peach-soft)}
    .program-card[data-tone=sea]{--tone:var(--sp-tone-sea);--tone-soft:var(--sp-tone-sea-soft)}
    .program-card[data-tone=sand]{--tone:var(--sp-tone-sand);--tone-soft:var(--sp-tone-sand-soft)}
    .card-meta{display:flex;flex-wrap:wrap;gap:8px}
    .card-meta span{display:inline-block;padding:4px 10px;border-radius:999px;font-size:12px;font-weight:700;background:var(--sp-surface-2,var(--sp-tone-mint-soft));color:var(--sp-text-2)}
    .card-meta .age-badge{background:var(--tone-soft);color:var(--tone)}
    .program-card h3{margin:14px 0;font-size:17px;font-weight:700;line-height:1.45}
    .program-facts{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:8px;margin:0;padding:12px;border-radius:14px;background:var(--tone-soft)}
    .program-facts dt{font-size:12px;color:var(--sp-text-2)}.program-facts dd{margin:2px 0 0;font-size:15px;font-weight:700;font-variant-numeric:tabular-nums}
    .description{margin:14px 0 18px;font-size:13px;color:var(--sp-text-2);overflow-wrap:anywhere}
    .description summary{min-height:44px;display:flex;align-items:center;cursor:pointer;font-weight:700;color:var(--sp-primary)}
    .description p{margin:6px 0 0;line-height:1.6}
    .card-action{margin-top:auto;width:100%;min-height:46px;padding:10px 16px;border:2px solid var(--sp-primary);border-radius:999px;background:var(--sp-surface);color:var(--sp-primary);font:inherit;font-size:14px;font-weight:700;cursor:pointer}
    .card-action:hover:not(:disabled){background:var(--sp-primary);color:var(--sp-on-primary)}
    .card-action:disabled{border-color:var(--sp-border);background:var(--sp-surface-2,var(--sp-surface));color:var(--sp-text-2);cursor:not-allowed}
    .empty-state{padding:28px;text-align:center;border:1px dashed var(--sp-border-strong,var(--sp-border));border-radius:18px;background:var(--sp-surface);color:var(--sp-text-2)}
    .history-title{margin:8px 0 12px}
    .history-list{display:grid;gap:8px;margin:0;padding:0;list-style:none}
    .history-row{display:flex;align-items:center;justify-content:space-between;gap:12px;flex-wrap:wrap;padding:14px 18px;border-radius:16px;background:var(--sp-surface);box-shadow:var(--sp-shadow-sm,var(--sp-shadow-card))}
    .history-row__meta{display:inline-flex;align-items:center;gap:10px;color:var(--sp-text-2);font-size:14px}
    .done-chip{padding:3px 10px;border-radius:999px;background:var(--sp-tone-mint-soft);color:var(--sp-tone-mint);font-size:12px;font-weight:700}
    @media(max-width:960px){.program-grid{grid-template-columns:repeat(2,minmax(0,1fr))}}
    @media(max-width:640px){section{padding:24px 16px}.catalog-toolbar{flex-wrap:wrap}.catalog-toolbar>span{margin-left:0;width:100%}.catalog-toolbar select{flex:1}
      .active-program{align-items:stretch;flex-direction:column;padding:22px 22px 28px}.active-program a{width:100%}.program-grid{grid-template-columns:1fr}}
    @media(prefers-reduced-motion:reduce){.program-card:hover{transform:none}}
  `]
})
export class TrainingProgramsComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  readonly ageGroups = ['Çocuk', 'Genç', 'Genç Yetişkin', 'Yetişkin'];
  readonly selectedAge = signal('');
  readonly filteredTemplates = computed(() => this.templates()
    .filter(program => !this.selectedAge() || this.ageGroup(program) === this.selectedAge())
    .slice().sort((a, b) => {
      const order = (program: TrainingTemplate) => {
        const index = this.ageGroups.indexOf(this.ageGroup(program));
        return index < 0 ? this.ageGroups.length : index;
      };
      return order(a) - order(b) || this.level(a) - this.level(b) || a.name.localeCompare(b.name, 'tr');
    }));
  ageGroup(program: TrainingTemplate): string {
    const prefix = program.name.split('—')[0].trim();
    return this.ageGroups.find(age => age.toLocaleLowerCase('tr') === prefix.toLocaleLowerCase('tr')) || 'Genel';
  }
  level(program: TrainingTemplate): number {
    return program.initialDifficultyLevel ?? Number(program.name.match(/Seviye\s+(\d+)/i)?.[1] || 1);
  }
  /** Lively design: each age group wears one soft tone. */
  tone(program: TrainingTemplate): 'peach' | 'sea' | 'sand' | 'mint' {
    return ({ 'Çocuk': 'peach', 'Genç': 'sea', 'Genç Yetişkin': 'sand' } as Record<string, 'peach' | 'sea' | 'sand'>)[this.ageGroup(program)] ?? 'mint';
  }
  readonly activeTotalDays = computed(() => {
    const active = this.active();
    return active ? this.templates().find(item => item.id === active.templateId)?.totalDays ?? null : null;
  });
  readonly activePercent = computed(() => {
    const total = this.activeTotalDays(); const done = this.active()?.totalDaysCompleted ?? 0;
    return total ? Math.min(100, Math.round(done / total * 100)) : 0;
  });
  readonly templates = signal<TrainingTemplate[]>([]);
  readonly history = signal<StudentProgramInfo[]>([]);
  readonly active = signal<StudentProgramInfo | null>(null);
  readonly loading = signal(false);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  ngOnInit(): void { this.load(); }
  load(): void {
    if (this.loading() || this.busy()) return;
    this.loading.set(true); this.error.set(null);
    forkJoin({ templates: this.http.get<TrainingTemplate[]>(`${environment.speedReadingApiUrl}/staff-training/programs`),
      history: this.http.get<StudentProgramInfo[]>(`${environment.speedReadingApiUrl}/student-program/my-programs`) })
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.loading.set(false))).subscribe({
        next: result => { this.templates.set(result.templates); this.history.set(result.history);
          this.active.set(result.history.find(item => item.isActive) ?? null); },
        error: error => this.error.set(error?.error?.message || 'Programlar yüklenemedi. Lütfen yeniden dene.')
      });
  }
  start(templateId: string): void {
    if (this.busy() || this.loading() || this.active()) return;
    this.busy.set(true); this.error.set(null);
    this.http.post(`${environment.speedReadingApiUrl}/staff-training/start`, { templateId })
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.busy.set(false))).subscribe({
        next: () => this.router.navigate(['/student/daily-exercises']),
        error: error => this.error.set(error?.error?.message || 'Program başlatılamadı. Lütfen yeniden dene.')
      });
  }
}
