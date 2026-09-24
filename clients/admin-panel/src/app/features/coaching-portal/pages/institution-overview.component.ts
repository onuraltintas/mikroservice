import { isPlatformBrowser } from '@angular/common';
import { Component, inject, OnInit, PLATFORM_ID, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CoachingAdminOverview, CoachingAdminService } from '../../../core/services/coaching-admin.service';

@Component({
  selector: 'app-institution-overview',
  standalone: true,
  imports: [RouterLink],
  template: `
    <section class="space-y-6" aria-labelledby="institution-heading">
      <div class="flex flex-wrap items-start justify-between gap-4">
        <div>
          <p class="text-xs font-semibold uppercase tracking-[0.18em] text-indigo-600 dark:text-indigo-300">Koçluk çalışma alanı</p>
          <h1 id="institution-heading" class="mt-2 text-2xl font-bold text-slate-950 dark:text-white">Kurum Özeti</h1>
          <p class="mt-1 text-sm text-slate-500 dark:text-slate-400">Yalnızca aktif kurumunuza ait koçluk verileri.</p>
        </div>
        <button type="button" (click)="load()" [disabled]="loading()" class="rounded-lg border border-indigo-200 px-4 py-2 text-sm font-semibold text-indigo-700 disabled:opacity-50 dark:border-indigo-800 dark:text-indigo-200">Yenile</button>
      </div>
      @if (error()) { <p role="alert" class="rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-700 dark:border-red-900 dark:bg-red-950/30 dark:text-red-200">{{ error() }}</p> }
      @if (loading() && !overview()) { <p role="status" class="text-sm text-slate-500">Kurum verileri yükleniyor…</p> }
      @if (overview(); as data) {
        <div class="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
          <div class="rounded-2xl border border-slate-200 bg-white p-5 dark:border-slate-800 dark:bg-slate-900"><p class="text-sm text-slate-500">Ödevler</p><p class="mt-2 text-3xl font-bold">{{ data.totalAssignments }}</p><p class="mt-1 text-xs text-slate-500">{{ data.activeAssignments }} aktif</p></div>
          <div class="rounded-2xl border border-slate-200 bg-white p-5 dark:border-slate-800 dark:bg-slate-900"><p class="text-sm text-slate-500">Seanslar</p><p class="mt-2 text-3xl font-bold">{{ data.totalSessions }}</p><p class="mt-1 text-xs text-slate-500">{{ data.upcomingSessions }} yaklaşan</p></div>
          <div class="rounded-2xl border border-slate-200 bg-white p-5 dark:border-slate-800 dark:bg-slate-900"><p class="text-sm text-slate-500">Sınavlar</p><p class="mt-2 text-3xl font-bold">{{ data.totalExams }}</p><p class="mt-1 text-xs text-slate-500">{{ data.totalExamResults }} sonuç</p></div>
          <div class="rounded-2xl border border-slate-200 bg-white p-5 dark:border-slate-800 dark:bg-slate-900"><p class="text-sm text-slate-500">Hedefler</p><p class="mt-2 text-3xl font-bold">{{ data.totalGoals }}</p><p class="mt-1 text-xs text-slate-500">{{ data.completedGoals }} tamamlandı</p></div>
        </div>
        <div class="grid gap-3 sm:grid-cols-2">
          <a routerLink="/coaching-portal/institution/students" class="rounded-2xl border border-indigo-200 bg-indigo-50 p-5 font-semibold text-indigo-800 hover:border-indigo-400 dark:border-indigo-900 dark:bg-indigo-950/30 dark:text-indigo-200">Öğrencileri incele →</a>
          <a routerLink="/coaching-portal/institution/teachers" class="rounded-2xl border border-slate-200 bg-white p-5 font-semibold hover:border-indigo-400 dark:border-slate-800 dark:bg-slate-900">Öğretmenleri incele →</a>
        </div>
      }
    </section>
  `
})
export class InstitutionOverviewComponent implements OnInit {
  private readonly service = inject(CoachingAdminService);
  private readonly platformId = inject(PLATFORM_ID);
  readonly institutionId = signal<string | null>(null);
  readonly overview = signal<CoachingAdminOverview | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  ngOnInit(): void {
    if (isPlatformBrowser(this.platformId)) this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.service.getReadScope().subscribe({
      next: scope => {
        if (scope.isGlobal || !scope.institutionId) {
          this.error.set('Aktif kurum kapsamı bulunamadı.');
          this.loading.set(false);
          return;
        }
        this.institutionId.set(scope.institutionId);
        this.service.getOverview().subscribe({
          next: data => { this.overview.set(data); this.loading.set(false); },
          error: () => { this.error.set('Kurum koçluk özeti yüklenemedi.'); this.loading.set(false); }
        });
      },
      error: () => { this.error.set('Kurum kapsamı yüklenemedi.'); this.loading.set(false); }
    });
  }
}
