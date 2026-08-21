import { CommonModule, isPlatformBrowser } from '@angular/common';
import { Component, inject, OnInit, PLATFORM_ID, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { finalize } from 'rxjs';
import {
  CoachingAdminAssignmentListItem,
  CoachingAdminService
} from '../../../core/services/coaching-admin.service';

@Component({
  selector: 'app-coaching-assignments',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  template: `
    <section class="space-y-6">
      <div class="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 class="text-2xl font-bold text-gray-900 dark:text-white">Koçluk ödevleri</h1>
          <p class="text-sm text-gray-500 dark:text-gray-400">Kitap ödevleri ve fotoğraf teslim metadata'sı.</p>
        </div>
        <div class="flex gap-2">
          <a routerLink="/dashboard/coaching/assignments/new" class="rounded-lg bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-700">Yeni ödev</a>
          <button type="button" (click)="load()" [disabled]="loading()"
            class="rounded-lg border border-gray-300 px-4 py-2 text-sm font-medium text-gray-700 disabled:opacity-50 dark:border-gray-600 dark:text-gray-200">
            Yenile
          </button>
        </div>
      </div>

      <div class="grid grid-cols-1 gap-3 rounded-xl border border-gray-200 bg-white p-4 shadow-sm md:grid-cols-4 dark:border-gray-700 dark:bg-gray-800">
        <label class="text-sm text-gray-600 dark:text-gray-300">Arama
          <input [(ngModel)]="search" (keyup.enter)="applyFilters()" maxlength="200"
            class="mt-1 w-full rounded-lg border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-900" placeholder="Başlık veya açıklama" />
        </label>
        <label class="text-sm text-gray-600 dark:text-gray-300">Kaynak
          <select [(ngModel)]="source" (change)="applyFilters()"
            class="mt-1 w-full rounded-lg border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-900">
            <option value="">Tümü</option><option value="Digital">Dijital</option><option value="Book">Kitap</option><option value="Mixed">Karma</option>
          </select>
        </label>
        <label class="text-sm text-gray-600 dark:text-gray-300">Durum
          <select [(ngModel)]="status" (change)="applyFilters()"
            class="mt-1 w-full rounded-lg border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-900">
            <option value="">Tümü</option><option value="Active">Aktif</option><option value="Completed">Tamamlandı</option><option value="Cancelled">İptal</option>
          </select>
        </label>
        <button type="button" (click)="applyFilters()"
          class="self-end rounded-lg border border-gray-300 px-4 py-2 text-sm font-medium text-gray-700 dark:border-gray-600 dark:text-gray-200">
          Filtrele
        </button>
      </div>

      @if (error()) { <div class="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700">{{ error() }}</div> }

      <div class="overflow-x-auto rounded-xl border border-gray-200 bg-white shadow-sm dark:border-gray-700 dark:bg-gray-800">
        <table class="min-w-full divide-y divide-gray-200 text-sm dark:divide-gray-700">
          <thead class="bg-gray-50 text-left text-xs uppercase text-gray-500 dark:bg-gray-900/40 dark:text-gray-400">
            <tr><th class="px-4 py-3">Başlık</th><th class="px-4 py-3">Kaynak</th><th class="px-4 py-3">Kitap aralığı</th><th class="px-4 py-3">Öğrenci</th><th class="px-4 py-3">Ek</th><th class="px-4 py-3">Durum</th></tr>
          </thead>
          <tbody class="divide-y divide-gray-200 dark:divide-gray-700">
            @for (assignment of assignments(); track assignment.id) {
              <tr>
                <td class="px-4 py-3 font-medium text-gray-900 dark:text-white"><a class="text-indigo-600 hover:underline" [routerLink]="['/coaching/assignments', assignment.id]">{{ assignment.title }}</a></td>
                <td class="px-4 py-3">{{ sourceLabel(assignment.source) }}</td>
                <td class="px-4 py-3">{{ bookRange(assignment) }}</td>
                <td class="px-4 py-3">{{ assignment.submittedStudentCount }}/{{ assignment.studentCount }}</td>
                <td class="px-4 py-3">{{ assignment.attachmentCount }}</td>
                <td class="px-4 py-3">{{ assignment.status }}</td>
              </tr>
            } @empty {
              <tr><td colspan="6" class="px-4 py-10 text-center text-gray-500">Kayıt bulunamadı.</td></tr>
            }
          </tbody>
        </table>
      </div>

      <div class="flex items-center justify-between text-sm text-gray-600 dark:text-gray-300">
        <span>{{ totalCount() }} kayıt · Sayfa {{ page() }} / {{ totalPages() }}</span>
        <div class="flex gap-2">
          <button type="button" (click)="goTo(page() - 1)" [disabled]="page() <= 1 || loading()" class="rounded border px-3 py-1 disabled:opacity-40">Önceki</button>
          <button type="button" (click)="goTo(page() + 1)" [disabled]="page() >= totalPages() || loading()" class="rounded border px-3 py-1 disabled:opacity-40">Sonraki</button>
        </div>
      </div>
    </section>
  `
})
export class CoachingAssignmentsComponent implements OnInit {
  private readonly service = inject(CoachingAdminService);
  private readonly platformId = inject(PLATFORM_ID);
  readonly assignments = signal<CoachingAdminAssignmentListItem[]>([]);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly page = signal(1);
  readonly totalCount = signal(0);
  readonly totalPages = signal(1);
  readonly pageSize = 25;
  search = '';
  source = '';
  status = '';

  ngOnInit() { if (isPlatformBrowser(this.platformId)) this.load(); }

  load() {
    this.loading.set(true);
    this.error.set(null);
    this.service.getAssignments({ pageNumber: this.page(), pageSize: this.pageSize, source: this.source, status: this.status, search: this.search })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: response => { this.assignments.set(response.items); this.totalCount.set(response.totalCount); this.totalPages.set(Math.max(1, response.totalPages)); },
        error: () => this.error.set('Koçluk ödevleri yüklenemedi.')
      });
  }

  applyFilters() { this.page.set(1); this.load(); }
  goTo(page: number) { if (page >= 1 && page <= this.totalPages()) { this.page.set(page); this.load(); } }
  sourceLabel(source: string) { return source === 'Book' ? 'Kitap' : source === 'Mixed' ? 'Karma' : 'Dijital'; }
  bookRange(assignment: CoachingAdminAssignmentListItem) {
    return assignment.bookTitle && assignment.bookStartPage && assignment.bookEndPage
      ? `${assignment.bookTitle} · s. ${assignment.bookStartPage}-${assignment.bookEndPage}`
      : '—';
  }
}
