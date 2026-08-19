import { CommonModule, isPlatformBrowser } from '@angular/common';
import { Component, inject, OnInit, PLATFORM_ID, signal } from '@angular/core';
import { finalize } from 'rxjs';
import { CoachingAdminOverview, CoachingAdminService } from '../../../core/services/coaching-admin.service';

@Component({
  selector: 'app-coaching-overview',
  standalone: true,
  imports: [CommonModule],
  template: `
    <section class="space-y-6">
      <div class="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 class="text-2xl font-bold text-gray-900 dark:text-white">Koçluk Özeti</h1>
          <p class="text-sm text-gray-500 dark:text-gray-400">Sistem yöneticisi için salt-okunur operasyon görünümü.</p>
        </div>
        <button type="button" (click)="load()" [disabled]="loading()"
          class="rounded-lg bg-indigo-600 px-4 py-2 text-sm font-medium text-white disabled:opacity-50">
          Yenile
        </button>
      </div>

      @if (error()) {
        <div class="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700">{{ error() }}</div>
      }

      @if (loading() && !overview()) {
        <div class="rounded-xl bg-white p-6 shadow-sm dark:bg-gray-800">Yükleniyor...</div>
      } @else if (overview(); as data) {
        <div class="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
          @for (card of cards(data); track card.label) {
            <div class="rounded-xl border border-gray-200 bg-white p-5 shadow-sm dark:border-gray-700 dark:bg-gray-800">
              <p class="text-sm text-gray-500 dark:text-gray-400">{{ card.label }}</p>
              <p class="mt-2 text-3xl font-bold text-gray-900 dark:text-white">{{ card.value }}</p>
            </div>
          }
        </div>

        <div class="rounded-xl border border-gray-200 bg-white shadow-sm dark:border-gray-700 dark:bg-gray-800">
          <div class="border-b border-gray-200 px-5 py-4 dark:border-gray-700">
            <h2 class="font-semibold text-gray-900 dark:text-white">Son ödevler</h2>
          </div>
          <div class="overflow-x-auto">
            <table class="min-w-full divide-y divide-gray-200 text-sm dark:divide-gray-700">
              <thead class="bg-gray-50 text-left text-xs uppercase text-gray-500 dark:bg-gray-900/40 dark:text-gray-400">
                <tr><th class="px-5 py-3">Başlık</th><th class="px-5 py-3">Durum</th><th class="px-5 py-3">Öğrenci</th><th class="px-5 py-3">Teslim</th><th class="px-5 py-3">Teslim tarihi</th></tr>
              </thead>
              <tbody class="divide-y divide-gray-200 dark:divide-gray-700">
                @for (assignment of data.recentAssignments; track assignment.id) {
                  <tr><td class="px-5 py-3 font-medium text-gray-900 dark:text-white">{{ assignment.title }}</td><td class="px-5 py-3">{{ assignment.status }}</td><td class="px-5 py-3">{{ assignment.studentCount }}</td><td class="px-5 py-3">{{ assignment.submittedStudentCount }}</td><td class="px-5 py-3">{{ assignment.dueDate | date:'shortDate' }}</td></tr>
                } @empty {
                  <tr><td colspan="5" class="px-5 py-8 text-center text-gray-500">Kayıt bulunamadı.</td></tr>
                }
              </tbody>
            </table>
          </div>
        </div>
      }
    </section>
  `
})
export class CoachingOverviewComponent implements OnInit {
  private readonly service = inject(CoachingAdminService);
  private readonly platformId = inject(PLATFORM_ID);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly overview = signal<CoachingAdminOverview | null>(null);

  ngOnInit() {
    if (isPlatformBrowser(this.platformId)) {
      this.load();
    }
  }

  load() {
    this.loading.set(true);
    this.error.set(null);
    this.service.getOverview().pipe(finalize(() => this.loading.set(false))).subscribe({
      next: value => this.overview.set(value),
      error: () => this.error.set('Koçluk özeti yüklenemedi.')
    });
  }

  cards(data: CoachingAdminOverview) {
    return [
      { label: 'Toplam ödev', value: data.totalAssignments },
      { label: 'Aktif ödev', value: data.activeAssignments },
      { label: 'Toplam sınav', value: data.totalExams },
      { label: 'Sınav sonucu', value: data.totalExamResults },
      { label: 'Toplam seans', value: data.totalSessions },
      { label: 'Yaklaşan seans', value: data.upcomingSessions },
      { label: 'Toplam hedef', value: data.totalGoals },
      { label: 'Tamamlanan hedef', value: data.completedGoals }
    ];
  }
}
