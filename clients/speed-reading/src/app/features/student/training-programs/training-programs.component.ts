import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize, forkJoin } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { StudentProgramInfo } from '../../../core/services/student-program.service';

interface TrainingTemplate { id: string; name: string; description: string; totalDays: number; programType: number; examType?: string; }

@Component({
  selector: 'app-training-programs', standalone: true, imports: [RouterLink],
  template: `
    <section>
      <h1>Eğitim Programları</h1>
      <p>Admin ve öğretmen eğitimi: günün tüm egzersizlerini bitirince sonraki gün hemen açılır.
        Aktif programı tamamladıktan sonra istediğiniz programı yeniden veya ilk kez başlatabilirsiniz.</p>
      @if (loading()) { <p role="status">Programlar yükleniyor…</p> }
      @if (error()) { <p role="alert">{{ error() }}</p><button type="button" (click)="load()">Yeniden dene</button> }
      @if (active(); as program) {
        <aside><h2>Aktif: {{ program.templateName }}</h2>
          <p>Hafta {{ program.currentWeek }} · Gün {{ program.currentDay }} · {{ program.totalDaysCompleted }} gün tamamlandı</p>
          <a routerLink="/student/daily-exercises">Eğitime devam et</a></aside>
      }
      <div class="program-grid">
        @for (program of templates(); track program.id) {
          <article><h2>{{ program.name }}</h2><p>{{ program.description }}</p>
            <p>{{ program.totalDays }} eğitim günü @if (program.examType) { · {{ program.examType }} }</p>
            <button type="button" [disabled]="busy() || !!active()" (click)="start(program.id)">Programa kaydol ve başla</button>
          </article>
        }
      </div>
      @if (history().length) {
        <h2>Program geçmişim</h2>
        @for (program of history(); track program.progressId) {
          <p>{{ program.templateName }} · {{ program.totalDaysCompleted }} gün tamamlandı
            @if (program.completedDate) { · Tamamlandı }</p>
        }
      }
    </section>
  `,
  styles: [`section{max-width:1100px;margin:auto;padding:24px} .program-grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(240px,1fr));gap:16px}
    article,aside{padding:20px;border:1px solid #dce2ef;border-radius:16px;background:var(--card-bg,#fff);margin-bottom:16px}
    button,a{display:inline-block;padding:10px 16px;border-radius:8px}button:disabled{opacity:.5} [role=alert]{color:#b42318}`]
})
export class TrainingProgramsComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
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
