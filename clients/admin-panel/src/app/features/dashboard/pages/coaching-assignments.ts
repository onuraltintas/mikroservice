import { CommonModule, isPlatformBrowser } from '@angular/common';
import { Component, inject, OnInit, PLATFORM_ID, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { ADMIN_PERMISSIONS } from '../../../core/auth/permissions';
import { CoachingLabelPipe } from '../../coaching-portal/coaching-labels';
import {
  CoachingAdminAssignmentListItem,
  CoachingAdminService
} from '../../../core/services/coaching-admin.service';

@Component({
  selector: 'app-coaching-assignments',
  standalone: true,
  imports: [CoachingLabelPipe, CommonModule, FormsModule, RouterModule],
  template: `
    <section class="space-y-6">
      <div class="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 class="text-2xl font-bold text-(--ui-text)">Koçluk ödevleri</h1>
          <p class="text-sm text-(--ui-text-muted)">Kitap ödevleri ve fotoğraf teslimlerinin kayıtları.</p>
      </div>
        <div class="flex gap-2">
          @if (canManage()) {
            <a routerLink="/dashboard/coaching/assignments/new" class="rounded-lg bg-(--ui-brand) px-4 py-2 text-sm font-medium text-(--ui-brand-contrast) hover:bg-(--ui-brand)">Yeni ödev</a>
          }
          <button type="button" (click)="load()" [disabled]="loading()"
            class="rounded-lg border border-(--ui-border) px-4 py-2 text-sm font-medium text-(--ui-text) disabled:opacity-50">
            Yenile
          </button>
        </div>
      </div>

      <div class="grid grid-cols-1 gap-3 rounded-xl border border-(--ui-border) bg-(--ui-surface) p-4 shadow-sm md:grid-cols-4">
        <label class="text-sm text-(--ui-text-muted)">Arama
          <input [(ngModel)]="search" (keyup.enter)="applyFilters()" maxlength="200"
            class="mt-1 w-full rounded-lg border border-(--ui-border) px-3 py-2" placeholder="Başlık veya açıklama" />
        </label>
        <label class="text-sm text-(--ui-text-muted)">Kaynak
          <select [(ngModel)]="source" (change)="applyFilters()"
            class="mt-1 w-full rounded-lg border border-(--ui-border) px-3 py-2">
            <option value="">Tümü</option><option value="Digital">Dijital</option><option value="Book">Kitap</option><option value="Mixed">Karma</option>
          </select>
        </label>
        <label class="text-sm text-(--ui-text-muted)">Durum
          <select [(ngModel)]="status" (change)="applyFilters()"
            class="mt-1 w-full rounded-lg border border-(--ui-border) px-3 py-2">
            <option value="">Tümü</option><option value="Active">Aktif</option><option value="Completed">Tamamlandı</option><option value="Cancelled">İptal</option>
          </select>
        </label>
        <button type="button" (click)="applyFilters()"
          class="self-end rounded-lg border border-(--ui-border) px-4 py-2 text-sm font-medium text-(--ui-text)">
          Filtrele
        </button>
      </div>

      @if (error()) { <div class="rounded-lg border border-(--ui-danger-soft) bg-(--ui-danger-soft) p-4 text-sm text-(--ui-danger)">{{ error() }}</div> }

      <div class="overflow-x-auto rounded-xl border border-(--ui-border) bg-(--ui-surface) shadow-sm">
        <table class="min-w-full divide-y divide-(--ui-border) text-sm">
          <thead class="bg-(--ui-surface-muted) text-left text-xs text-(--ui-text-muted)">
            <tr><th class="px-4 py-3">Başlık</th><th class="px-4 py-3">Kaynak</th><th class="px-4 py-3">Kitap aralığı</th><th class="px-4 py-3">Öğrenci</th><th class="px-4 py-3">Ek</th><th class="px-4 py-3">Durum</th></tr>
          </thead>
          <tbody class="divide-y divide-(--ui-border)">
            @for (assignment of assignments(); track assignment.id) {
              <tr>
                <td class="px-4 py-3 font-medium text-(--ui-text)"><a class="text-(--ui-brand-text) hover:underline" [routerLink]="['/dashboard/coaching/assignments', assignment.id]">{{ assignment.title }}</a></td>
                <td class="px-4 py-3">{{ sourceLabel(assignment.source) }}</td>
                <td class="px-4 py-3">{{ bookRange(assignment) }}</td>
                <td class="px-4 py-3">{{ assignment.submittedStudentCount }}/{{ assignment.studentCount }}</td>
                <td class="px-4 py-3">{{ assignment.attachmentCount }}</td>
                <td class="px-4 py-3">{{ assignment.status | coachingLabel }}</td>
              </tr>
            } @empty {
              <tr><td colspan="6" class="px-4 py-10 text-center text-(--ui-text-muted)">Kayıt bulunamadı.</td></tr>
            }
          </tbody>
        </table>
      </div>

      <div class="flex items-center justify-between text-sm text-(--ui-text-muted)">
        <span>{{ totalCount() }} kayıt, sayfa {{ page() }} / {{ totalPages() }}</span>
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
  private readonly auth = inject(AuthService);
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

  canManage() { return this.auth.hasPermission(ADMIN_PERMISSIONS.coachingManage); }

  ngOnInit() { if (isPlatformBrowser(this.platformId)) this.load(); }

  load() {
    this.loading.set(true);
    this.error.set(null);
    this.service.getAssignments({ pageNumber: this.page(), pageSize: this.pageSize, source: this.source, status: this.status, search: this.search })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: response => { this.assignments.set(response.items); this.totalCount.set(response.totalCount); this.totalPages.set(Math.max(1, response.totalPages || 1)); },
        error: () => this.error.set('Koçluk ödevleri yüklenemedi. Sayfayı yenileyip yeniden deneyin.')
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
