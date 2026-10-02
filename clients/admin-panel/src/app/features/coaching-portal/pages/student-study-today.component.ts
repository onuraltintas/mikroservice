import { CommonModule } from '@angular/common';
import { Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, finalize, forkJoin, of, switchMap, throwError } from 'rxjs';
import { CoachingStudyPlanningService, StudyTask } from '../../../core/services/coaching-study-planning.service';

@Component({ selector: 'app-student-study-today', standalone: true, imports: [CommonModule, RouterLink], template: `
  <section class="my-5 rounded-2xl border border-slate-200 bg-white p-5 dark:border-slate-800 dark:bg-slate-900" aria-labelledby="today-plan-title">
    <h2 id="today-plan-title" class="text-lg font-semibold">Çalışma planım</h2>
    <p *ngIf="busy()" role="status" class="mt-3 text-sm">Çalışmalar yükleniyor…</p>
    <div *ngIf="error()" role="alert" class="mt-3 text-sm text-red-600">{{ error() }} <button type="button" (click)="load()" class="underline" [disabled]="busy()">Yeniden dene</button></div>
    <ng-container *ngIf="!busy() && !error()">
      <p *ngIf="!title()" class="mt-3 text-sm">Henüz aktif çalışma planın yok. Manuel veya otomatik bir taslak hazırlayabilirsin.</p>
      <ng-container *ngIf="title()">
        <p class="mt-2 font-medium">{{ title() }}</p><p class="text-xs text-slate-500">{{ today() }} · {{ timeZone() }} · Öğrenci beyanı</p>
        <h3 class="mt-3 font-medium">Bugünkü çalışmalar</h3>
        <p *ngIf="!todayTasks().length" class="text-sm text-slate-500">Bugün için tamamlanmamış çalışma yok.</p>
        <ul class="mt-2 space-y-2"><li *ngFor="let task of todayTasks()" class="rounded-lg bg-slate-50 p-3 text-sm dark:bg-slate-800">{{ task.title }} · {{ task.plannedMinutes }} dakika</li></ul>
        <p *ngIf="overdue().length" class="mt-3 text-sm text-amber-700 dark:text-amber-300">{{ overdue().length }} gecikmiş çalışma var. Plan ekranından tamamlayabilir veya tarihini değiştirebilirsin.</p>
        <ul class="mt-2 space-y-2"><li *ngFor="let task of overdue().slice(0, 5)" class="text-sm">{{ task.title }} · {{ task.plannedDate | date:'dd.MM.yyyy' }}</li></ul>
      </ng-container>
    </ng-container>
    <div class="mt-4 flex flex-wrap gap-4 text-sm text-indigo-600 dark:text-indigo-300"><a routerLink="/coaching-portal/study-plans" class="underline">Planımı yönet</a><a routerLink="/coaching-portal/exam-results" class="underline">Sınav sonucu ekle</a><a routerLink="/coaching-portal/progress" class="underline">Raporlarım</a></div>
  </section>` })
export class StudentStudyTodayComponent implements OnInit {
  private readonly service = inject(CoachingStudyPlanningService);
  private readonly destroyRef = inject(DestroyRef);
  readonly busy = signal(false); readonly error = signal<string | null>(null);
  readonly title = signal<string | null>(null); readonly timeZone = signal(''); readonly today = signal('');
  readonly todayTasks = signal<StudyTask[]>([]); readonly overdue = signal<StudyTask[]>([]);
  ngOnInit() { this.load(); }
  load() {
    if (this.busy()) return;
    this.busy.set(true); this.error.set(null); this.title.set(null); this.todayTasks.set([]); this.overdue.set([]);
    forkJoin({ plans: this.service.list(1, 'Active'), hours: this.service.getAvailability().pipe(catchError(error =>
      error?.status === 404 ? of({ timeZoneId: Intl.DateTimeFormat().resolvedOptions().timeZone }) : throwError(() => error))) })
      .pipe(switchMap(({ plans, hours }) => {
        this.timeZone.set(hours.timeZoneId);
        const parts = new Intl.DateTimeFormat('en-US', { timeZone: hours.timeZoneId, year: 'numeric', month: '2-digit', day: '2-digit' }).formatToParts(new Date(Date.now()));
        const part = (type: string) => parts.find(x => x.type === type)!.value;
        this.today.set(`${part('year')}-${part('month')}-${part('day')}`);
        return plans.items.length ? this.service.get(plans.items[0].id) : of(null);
      }), takeUntilDestroyed(this.destroyRef), finalize(() => this.busy.set(false)))
      .subscribe({ next: plan => {
        this.title.set(plan?.title ?? null);
        const pending = (plan?.tasks ?? []).filter(x => !x.isCompleted).sort((a, b) => a.plannedDate.localeCompare(b.plannedDate) || a.id.localeCompare(b.id));
        this.todayTasks.set(pending.filter(x => x.plannedDate === this.today()));
        this.overdue.set(pending.filter(x => x.plannedDate < this.today()));
      }, error: () => this.error.set('Çalışma planı yüklenemedi. Mevcut planın durumu doğrulanamadı.') });
  }
}
