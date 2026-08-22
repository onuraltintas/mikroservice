import { CommonModule, isPlatformBrowser } from '@angular/common';
import { Component, inject, OnInit, PLATFORM_ID, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { finalize, Observable } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { ADMIN_PERMISSIONS } from '../../../core/auth/permissions';
import {
  CoachingAdminExamListItem,
  CoachingAdminGoalListItem,
  CoachingAdminSessionListItem,
  CoachingAdminService
} from '../../../core/services/coaching-admin.service';

type Resource = 'sessions' | 'exams' | 'goals';
type OperationalPage = { items: unknown[]; totalCount: number; totalPages: number };

@Component({
  selector: 'app-coaching-operational',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  template: `
    <section class="space-y-6">
      <div class="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 class="text-2xl font-bold text-gray-900 dark:text-white">Koçluk operasyonları</h1>
          <p class="text-sm text-gray-500 dark:text-gray-400">Seans, sınav ve akademik hedef kayıtlarını tek ekrandan izleyin.</p>
        </div>
        @if (canManage()) {
          <div class="flex flex-wrap gap-2">
            <a routerLink="/dashboard/coaching/operations/new/session" class="rounded-lg bg-indigo-600 px-3 py-2 text-sm font-medium text-white">Yeni seans</a>
            <a routerLink="/dashboard/coaching/operations/new/exam" class="rounded-lg bg-indigo-600 px-3 py-2 text-sm font-medium text-white">Yeni sınav</a>
            <a routerLink="/dashboard/coaching/operations/new/goal" class="rounded-lg bg-indigo-600 px-3 py-2 text-sm font-medium text-white">Yeni hedef</a>
          </div>
        }
      </div>

      <div class="flex flex-wrap gap-2">
        <button type="button" (click)="select('sessions')" [class.bg-indigo-600]="resource() === 'sessions'" [class.text-white]="resource() === 'sessions'" class="rounded-lg border px-4 py-2 text-sm">Seanslar</button>
        <button type="button" (click)="select('exams')" [class.bg-indigo-600]="resource() === 'exams'" [class.text-white]="resource() === 'exams'" class="rounded-lg border px-4 py-2 text-sm">Sınavlar</button>
        <button type="button" (click)="select('goals')" [class.bg-indigo-600]="resource() === 'goals'" [class.text-white]="resource() === 'goals'" class="rounded-lg border px-4 py-2 text-sm">Hedefler</button>
      </div>

      <div class="flex flex-wrap items-end gap-3 rounded-xl border border-gray-200 bg-white p-4 shadow-sm dark:border-gray-700 dark:bg-gray-800">
        <label class="min-w-64 text-sm text-gray-600 dark:text-gray-300">Arama
          <input [(ngModel)]="search" (keyup.enter)="applyFilters()" maxlength="200" class="mt-1 w-full rounded-lg border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-900" placeholder="Başlık, konu veya açıklama" />
        </label>
        @if (resource() === 'sessions') {
          <label class="text-sm text-gray-600 dark:text-gray-300">Durum
            <select [(ngModel)]="status" (change)="applyFilters()" class="mt-1 rounded-lg border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-900"><option value="">Tümü</option><option value="Scheduled">Planlandı</option><option value="Completed">Tamamlandı</option><option value="Cancelled">İptal</option></select>
          </label>
        }
        @if (resource() === 'goals') {
          <label class="text-sm text-gray-600 dark:text-gray-300">Tamamlanma
            <select [(ngModel)]="completed" (change)="applyFilters()" class="mt-1 rounded-lg border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-900"><option value="">Tümü</option><option value="false">Devam ediyor</option><option value="true">Tamamlandı</option></select>
          </label>
        }
        <button type="button" (click)="applyFilters()" [disabled]="loading()" class="rounded-lg bg-indigo-600 px-4 py-2 text-sm font-medium text-white disabled:opacity-50">Filtrele</button>
      </div>

      @if (error()) { <div class="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700">{{ error() }}</div> }
      @if (loading()) { <div class="rounded-xl border border-gray-200 bg-white p-8 text-center text-gray-500 dark:border-gray-700 dark:bg-gray-800">Yükleniyor…</div> }

      @if (resource() === 'sessions') {
        <div class="overflow-x-auto rounded-xl border border-gray-200 bg-white shadow-sm dark:border-gray-700 dark:bg-gray-800"><table class="min-w-full divide-y divide-gray-200 text-sm dark:divide-gray-700"><thead class="bg-gray-50 text-left text-xs uppercase text-gray-500 dark:bg-gray-900/40"><tr><th class="px-4 py-3">Başlık</th><th class="px-4 py-3">Tür</th><th class="px-4 py-3">Tarih</th><th class="px-4 py-3">Katılım</th><th class="px-4 py-3">Durum</th>@if (canManage()) { <th class="px-4 py-3">İşlem</th> }</tr></thead><tbody class="divide-y divide-gray-200 dark:divide-gray-700">@for (item of sessions(); track item.id) {<tr><td class="px-4 py-3 font-medium"><a [routerLink]="['/dashboard/coaching/operations/session', item.id]" class="text-indigo-700 hover:underline">{{ item.title }}</a></td><td class="px-4 py-3">{{ item.sessionType }}</td><td class="px-4 py-3">{{ item.scheduledDate | date:'dd.MM.yyyy HH:mm' }}</td><td class="px-4 py-3">{{ item.presentCount }}/{{ item.studentCount }}</td><td class="px-4 py-3">{{ item.status }}</td>@if (canManage()) { <td class="px-4 py-3"><div class="flex gap-2"><a [routerLink]="['/dashboard/coaching/operations/session', item.id, 'edit']" class="text-xs text-indigo-700 hover:underline">Düzenle</a>@if (item.status === 'Scheduled') { <button type="button" (click)="cancelSession(item.id)" [disabled]="actionLoading()" class="text-xs text-amber-700 hover:underline">İptal</button> }<button type="button" (click)="deleteSession(item.id)" [disabled]="actionLoading()" class="text-xs text-red-700 hover:underline">Sil</button></div></td> }</tr>} @empty {<tr><td [attr.colspan]="canManage() ? 6 : 5" class="px-4 py-10 text-center text-gray-500">Kayıt bulunamadı.</td></tr>}</tbody></table></div>
      } @else if (resource() === 'exams') {
        <div class="overflow-x-auto rounded-xl border border-gray-200 bg-white shadow-sm dark:border-gray-700 dark:bg-gray-800"><table class="min-w-full divide-y divide-gray-200 text-sm dark:divide-gray-700"><thead class="bg-gray-50 text-left text-xs uppercase text-gray-500 dark:bg-gray-900/40"><tr><th class="px-4 py-3">Başlık</th><th class="px-4 py-3">Tür</th><th class="px-4 py-3">Tarih</th><th class="px-4 py-3">Maksimum puan</th><th class="px-4 py-3">Sonuç</th>@if (canManage()) { <th class="px-4 py-3">İşlem</th> }</tr></thead><tbody class="divide-y divide-gray-200 dark:divide-gray-700">@for (item of exams(); track item.id) {<tr><td class="px-4 py-3 font-medium"><a [routerLink]="['/dashboard/coaching/operations/exam', item.id]" class="text-indigo-700 hover:underline">{{ item.title }}</a></td><td class="px-4 py-3">{{ item.examType }}</td><td class="px-4 py-3">{{ item.examDate | date:'dd.MM.yyyy HH:mm' }}</td><td class="px-4 py-3">{{ item.maxScore }}</td><td class="px-4 py-3">{{ item.resultCount }}</td>@if (canManage()) { <td class="px-4 py-3"><div class="flex gap-2"><a [routerLink]="['/dashboard/coaching/operations/exam', item.id, 'edit']" class="text-xs text-indigo-700 hover:underline">Düzenle</a><button type="button" (click)="deleteExam(item.id)" [disabled]="actionLoading()" class="text-xs text-red-700 hover:underline">Sil</button></div></td> }</tr>} @empty {<tr><td [attr.colspan]="canManage() ? 6 : 5" class="px-4 py-10 text-center text-gray-500">Kayıt bulunamadı.</td></tr>}</tbody></table></div>
      } @else {
        <div class="overflow-x-auto rounded-xl border border-gray-200 bg-white shadow-sm dark:border-gray-700 dark:bg-gray-800"><table class="min-w-full divide-y divide-gray-200 text-sm dark:divide-gray-700"><thead class="bg-gray-50 text-left text-xs uppercase text-gray-500 dark:bg-gray-900/40"><tr><th class="px-4 py-3">Başlık</th><th class="px-4 py-3">Kategori</th><th class="px-4 py-3">İlerleme</th><th class="px-4 py-3">Hedef tarih</th><th class="px-4 py-3">Durum</th>@if (canManage()) { <th class="px-4 py-3">İşlem</th> }</tr></thead><tbody class="divide-y divide-gray-200 dark:divide-gray-700">@for (item of goals(); track item.id) {<tr><td class="px-4 py-3 font-medium"><a [routerLink]="['/dashboard/coaching/operations/goal', item.id, 'edit']" class="text-indigo-700 hover:underline">{{ item.title }}</a></td><td class="px-4 py-3">{{ item.category }}</td><td class="px-4 py-3"><div class="flex items-center gap-2"><input type="number" min="0" max="100" [ngModel]="progressFor(item.id, item.currentProgress)" (ngModelChange)="setProgress(item.id, $event)" class="w-20 rounded border border-gray-300 px-2 py-1 text-sm dark:border-gray-600 dark:bg-gray-900" /><button type="button" (click)="updateGoalProgress(item.id)" [disabled]="actionLoading()" class="text-xs text-indigo-700 hover:underline">Kaydet</button></div></td><td class="px-4 py-3">{{ item.targetDate ? (item.targetDate | date:'dd.MM.yyyy') : '—' }}</td><td class="px-4 py-3">{{ item.isCompleted ? 'Tamamlandı' : 'Devam ediyor' }}</td>@if (canManage()) { <td class="px-4 py-3"><div class="flex gap-2"><a [routerLink]="['/dashboard/coaching/operations/goal', item.id, 'edit']" class="text-xs text-indigo-700 hover:underline">Düzenle</a><button type="button" (click)="deleteGoal(item.id)" [disabled]="actionLoading()" class="text-xs text-red-700 hover:underline">Sil</button></div></td> }</tr>} @empty {<tr><td [attr.colspan]="canManage() ? 6 : 5" class="px-4 py-10 text-center text-gray-500">Kayıt bulunamadı.</td></tr>}</tbody></table></div>
      }

      <div class="flex items-center justify-between text-sm text-gray-600 dark:text-gray-300"><span>{{ totalCount() }} kayıt · Sayfa {{ page() }} / {{ totalPages() }}</span><div class="flex gap-2"><button type="button" (click)="goTo(page() - 1)" [disabled]="page() <= 1 || loading()" class="rounded border px-3 py-1 disabled:opacity-40">Önceki</button><button type="button" (click)="goTo(page() + 1)" [disabled]="page() >= totalPages() || loading()" class="rounded border px-3 py-1 disabled:opacity-40">Sonraki</button></div></div>
    </section>
  `
})
export class CoachingOperationalComponent implements OnInit {
  private readonly service = inject(CoachingAdminService);
  private readonly auth = inject(AuthService);
  private readonly platformId = inject(PLATFORM_ID);
  readonly resource = signal<Resource>('sessions');
  readonly sessions = signal<CoachingAdminSessionListItem[]>([]);
  readonly exams = signal<CoachingAdminExamListItem[]>([]);
  readonly goals = signal<CoachingAdminGoalListItem[]>([]);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly page = signal(1);
  readonly totalCount = signal(0);
  readonly totalPages = signal(1);
  readonly actionLoading = signal(false);
  readonly pageSize = 25;
  search = '';
  status = '';
  completed = '';
  private readonly progressDraft: Record<string, number> = {};

  canManage() { return this.auth.hasPermission(ADMIN_PERMISSIONS.coachingManage); }

  ngOnInit() { if (isPlatformBrowser(this.platformId)) this.load(); }
  select(resource: Resource) { this.resource.set(resource); this.page.set(1); this.load(); }
  applyFilters() { this.page.set(1); this.load(); }
  goTo(page: number) { if (page >= 1 && page <= this.totalPages()) { this.page.set(page); this.load(); } }

  load() {
    this.loading.set(true);
    this.error.set(null);
    const options = { pageNumber: this.page(), pageSize: this.pageSize, search: this.search };
    const request: Observable<OperationalPage> = this.resource() === 'sessions'
      ? this.service.getSessions({ ...options, status: this.status }) as Observable<OperationalPage>
      : this.resource() === 'exams'
        ? this.service.getExams(options) as Observable<OperationalPage>
        : this.service.getGoals({ ...options, completed: this.completed === '' ? undefined : this.completed === 'true' }) as Observable<OperationalPage>;
    request.pipe(finalize(() => this.loading.set(false))).subscribe({
      next: response => {
        this.totalCount.set(response.totalCount);
        this.totalPages.set(Math.max(1, response.totalPages));
        if (this.resource() === 'sessions') this.sessions.set(response.items as CoachingAdminSessionListItem[]);
        if (this.resource() === 'exams') this.exams.set(response.items as CoachingAdminExamListItem[]);
        if (this.resource() === 'goals') this.goals.set(response.items as CoachingAdminGoalListItem[]);
      },
      error: () => this.error.set('Koçluk kayıtları yüklenemedi.')
    });
  }

  progressFor(id: string, fallback: number) { return this.progressDraft[id] ?? fallback; }
  setProgress(id: string, value: number) { this.progressDraft[id] = Number(value); }

  cancelSession(id: string) {
    if (typeof window !== 'undefined' && !window.confirm('Bu seans iptal edilsin mi?')) return;
    this.actionLoading.set(true);
    this.service.cancelSession(id).pipe(finalize(() => this.actionLoading.set(false))).subscribe({
      next: () => this.load(),
      error: () => this.error.set('Seans iptal edilemedi.')
    });
  }

  deleteSession(id: string) {
    if (typeof window !== 'undefined' && !window.confirm('Bu seans kalıcı olarak silinsin mi?')) return;
    this.actionLoading.set(true);
    this.service.deleteSession(id).pipe(finalize(() => this.actionLoading.set(false))).subscribe({
      next: () => this.load(),
      error: () => this.error.set('Seans silinemedi.')
    });
  }

  deleteExam(id: string) {
    if (typeof window !== 'undefined' && !window.confirm('Bu sınav kalıcı olarak silinsin mi?')) return;
    this.actionLoading.set(true);
    this.service.deleteExam(id).pipe(finalize(() => this.actionLoading.set(false))).subscribe({
      next: () => this.load(),
      error: () => this.error.set('Sınav silinemedi.')
    });
  }

  updateGoalProgress(id: string) {
    const progress = this.progressDraft[id];
    if (progress === undefined || !Number.isInteger(progress) || progress < 0 || progress > 100) {
      this.error.set('İlerleme 0-100 arasında tam sayı olmalıdır.');
      return;
    }
    this.actionLoading.set(true);
    this.service.updateGoalProgress(id, progress).pipe(finalize(() => this.actionLoading.set(false))).subscribe({
      next: () => this.load(),
      error: () => this.error.set('Hedef ilerlemesi güncellenemedi.')
    });
  }

  deleteGoal(id: string) {
    if (typeof window !== 'undefined' && !window.confirm('Bu hedef kalıcı olarak silinsin mi?')) return;
    this.actionLoading.set(true);
    this.service.deleteGoal(id).pipe(finalize(() => this.actionLoading.set(false))).subscribe({
      next: () => this.load(),
      error: () => this.error.set('Hedef silinemedi.')
    });
  }
}
