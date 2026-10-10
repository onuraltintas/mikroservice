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
      <header class="training-header"><span class="eyebrow">KENDİ HIZINIZDA EĞİTİM</span><h1>Eğitim Programları</h1>
      <p>Admin ve öğretmen eğitimi: günün tüm egzersizlerini bitirince sonraki gün hemen açılır.
        Aktif programı tamamladıktan sonra istediğiniz programı yeniden veya ilk kez başlatabilirsiniz.</p></header>
      @if (loading()) { <p role="status">Programlar yükleniyor…</p> }
      @if (error()) { <p role="alert">{{ error() }}</p><button type="button" (click)="load()">Yeniden dene</button> }
      @if (active(); as program) {
        <aside class="active-program"><div><span class="status-badge">Aktif eğitiminiz</span><h2>{{ program.templateName }}</h2>
          <div class="progress-stats"><span>Hafta <strong>{{ program.currentWeek }}</strong></span><span>Gün <strong>{{ program.currentDay }}</strong></span><span><strong>{{ program.totalDaysCompleted }}</strong> gün tamamlandı</span></div>
          <p class="muted">Kaldığınız yerden devam edin. Günün egzersizleri bitince sonraki gün açılır.</p></div>
          <a class="primary-action" routerLink="/student/daily-exercises">Eğitime devam et <span aria-hidden="true">→</span></a></aside>
      }
      <div class="catalog-heading"><div><span class="eyebrow">PROGRAM KATALOĞU</span><h2>Size uygun programı keşfedin</h2></div><span>{{ filteredTemplates().length }} program</span></div>
      <div class="catalog-toolbar"><label for="program-age">Yaş grubu</label><select id="program-age" [value]="selectedAge()" (change)="selectedAge.set($any($event.target).value)"><option value="">Tüm yaş grupları</option>@for (age of ageGroups; track age) { <option [value]="age">{{ age }}</option> }</select><span>Yaş grubu · Seviye sırasıyla</span></div>
      @if (active()) { <p class="enrollment-notice">Önce aktif programınızı tamamlayın; ardından aşağıdaki programlardan birini başlatabilirsiniz.</p> }
      <div class="program-grid">
        @for (program of filteredTemplates(); track program.id) {
          <article class="program-card"><div class="card-meta"><span class="age-badge">{{ ageGroup(program) }}</span><span class="level-badge">Seviye {{ level(program) }}</span>@if (program.examType) { <span>{{ program.examType }}</span> }</div>
            <h3>{{ program.name }}</h3><div class="program-facts"><span><strong>{{ program.totalDays }}</strong> gün</span><span><strong>{{ program.totalWeeks || 4 }}</strong> hafta</span><span>Seviye <strong>{{ level(program) }} → {{ program.maxDifficultyLevel || level(program) }}</strong></span></div><details class="description"><summary>Program ayrıntıları</summary><p>{{ program.description }}</p></details>
            <button class="primary-action" type="button" [disabled]="busy() || !!active()" (click)="start(program.id)">{{ active() ? 'Aktif programın ardından' : busy() ? 'Program başlatılıyor…' : 'Programa kaydol ve başla' }}</button>
          </article>
        }
      </div>
      @if (!loading() && !error() && !filteredTemplates().length) { <p class="empty-state" role="status">Bu yaş grubunda program bulunamadı.</p> }
      @if (history().length) {
        <h2>Program geçmişim</h2>
        @for (program of history(); track program.progressId) {
          <p class="history-row">{{ program.templateName }} · {{ program.totalDaysCompleted }} gün tamamlandı
            @if (program.completedDate) { · Tamamlandı }</p>
        }
      }
    </section>
  `,
  styles: [`
    :host{display:block;color:var(--text-primary,#172b4d)}
    section{max-width:1200px;margin:auto;padding:32px 24px}
    .training-header{display:flex;flex-direction:column;align-items:flex-start;gap:10px;margin-bottom:28px;max-width:850px}.training-header h1,.training-header p{margin:0}.eyebrow{font-size:12px;letter-spacing:.12em;font-weight:700;color:var(--primary-blue,#1976d2)}
    h1{font-size:clamp(26px,3vw,36px);font-weight:750;line-height:1.2;margin:10px 0 14px}h2{font-size:22px;font-weight:700;line-height:1.35;margin:12px 0}h3{font-size:19px;font-weight:700;line-height:1.4;margin:18px 0 12px}
    p{font-size:15px;line-height:1.7}.training-header p,.muted,.description{color:var(--text-secondary,#52647b)}
    .active-program{display:flex;align-items:center;justify-content:space-between;gap:24px;padding:28px;background:var(--card-bg,#fff);border:1px solid var(--primary-blue,#1976d2);border-left-width:5px;border-radius:18px;box-shadow:0 8px 28px #172b4d08;margin-bottom:32px}
    .status-badge,.card-meta span{display:inline-block;font-size:12px;font-weight:650;border-radius:6px;padding:5px 9px;background:var(--background-secondary,#eef4fb);color:var(--primary-blue,#1976d2)}
    .progress-stats{display:flex;flex-wrap:wrap;gap:10px 22px;font-size:14px}.progress-stats strong{font-weight:700}.muted{margin:12px 0 0}
    .primary-action{display:inline-flex;align-items:center;justify-content:center;gap:14px;min-height:46px;padding:12px 18px;border:1px solid transparent;border-radius:10px;background:var(--primary-blue,#1976d2);color:#fff;font-size:14px;font-weight:650;text-decoration:none;cursor:pointer;line-height:1.4}
    .primary-action:hover:not(:disabled){background:var(--primary-blue-dark,#0d47a1)}.primary-action:focus-visible{outline:3px solid var(--primary-blue,#1976d2);outline-offset:4px}.primary-action:disabled{background:var(--background-secondary,#f0f3f7);color:var(--text-secondary,#52647b);border-color:var(--border-color,#dce2ef);cursor:not-allowed}
    .active-program a{flex-shrink:0}.catalog-heading{display:flex;align-items:center;justify-content:space-between;gap:16px;margin-bottom:12px}.catalog-heading span{font-size:14px;color:var(--text-secondary,#52647b)}
    .enrollment-notice{padding:12px 16px;border-radius:10px;background:var(--background-secondary,#eef4fb);margin-bottom:20px}
    .program-grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(260px,1fr));gap:20px;margin-bottom:32px}
    .program-card{display:flex;flex-direction:column;min-width:0;padding:24px;border:1px solid var(--border-color,#dce2ef);border-radius:16px;background:var(--card-bg,#fff);box-shadow:0 4px 16px #172b4d04}.card-meta{display:flex;flex-wrap:wrap;gap:8px}.description{margin:0 0 24px;overflow-wrap:anywhere}.program-card button{margin-top:auto;width:100%}
    .history-row{padding:14px 18px;border:1px solid var(--border-color,#dce2ef);border-radius:10px;background:var(--card-bg,#fff)}[role=alert]{color:#b42318}
    .training-header{max-width:760px;margin-bottom:24px}.training-header p{font-size:14px}.active-program{padding:22px 26px;border-left-width:4px;background:linear-gradient(110deg,var(--card-bg,#fff),var(--background-secondary,#eef4fb))}.active-program h2{font-size:20px;max-width:720px}.muted{font-size:13px}
    .catalog-heading h2{margin:4px 0;font-size:24px}.catalog-toolbar{display:flex;align-items:center;gap:12px;margin:20px 0;color:var(--text-secondary,#52647b);font-size:13px}.catalog-toolbar label{font-weight:650}.catalog-toolbar select{font:inherit;color:var(--text-primary,#172b4d);background:var(--card-bg,#fff);border:1px solid var(--border-color,#dce2ef);border-radius:10px;padding:11px 36px 11px 14px;min-width:200px}.catalog-toolbar select:focus-visible{outline:3px solid var(--primary-blue,#1976d2);outline-offset:2px}.catalog-toolbar>span{margin-left:auto}
    .program-grid{grid-template-columns:repeat(3,minmax(0,1fr));gap:16px}.program-card{padding:22px;box-shadow:0 3px 14px #172b4d05;transition:box-shadow .15s,border-color .15s}.program-card:hover{border-color:var(--primary-blue,#1976d2);box-shadow:0 8px 24px #172b4d0c}.program-card h3{font-size:17px;margin:16px 0;line-height:1.45}.card-meta{justify-content:space-between}.card-meta .age-badge{color:var(--text-secondary,#52647b);background:var(--background-secondary,#eef4fb)}.program-facts{display:flex;flex-wrap:wrap;gap:12px;padding:12px 0;border-top:1px solid var(--border-color,#dce2ef);border-bottom:1px solid var(--border-color,#dce2ef);font-size:12px;color:var(--text-secondary,#52647b)}.description{font-size:13px;margin:16px 0 20px}.description summary{cursor:pointer;font-weight:600}.description p{font-size:13px;line-height:1.6;margin:10px 0}.empty-state{padding:28px;text-align:center;background:var(--card-bg,#fff);border:1px dashed var(--border-color,#dce2ef);border-radius:14px}.enrollment-notice{font-size:13px}
    @media(max-width:960px){.program-grid{grid-template-columns:repeat(2,minmax(0,1fr))}}
    @media(max-width:640px){.catalog-toolbar{flex-wrap:wrap}.catalog-toolbar>span{margin-left:0;width:100%}.catalog-toolbar select{flex:1}}
    @media(max-width:640px){section{padding:24px 16px}.active-program{align-items:stretch;flex-direction:column;padding:22px}.program-grid{grid-template-columns:1fr}.active-program a{width:100%}.program-card{padding:22px}}
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
        error: error => this.error.set(error?.error?.message || 'Programlar yüklenemedi. Lütfen yeniden deneyin.')
      });
  }
  start(templateId: string): void {
    if (this.busy() || this.loading() || this.active()) return;
    this.busy.set(true); this.error.set(null);
    this.http.post(`${environment.speedReadingApiUrl}/staff-training/start`, { templateId })
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.busy.set(false))).subscribe({
        next: () => this.router.navigate(['/student/daily-exercises']),
        error: error => this.error.set(error?.error?.message || 'Program başlatılamadı. Lütfen yeniden deneyin.')
      });
  }
}
