import { CommonModule, isPlatformBrowser } from '@angular/common';
import { Component, inject, OnInit, PLATFORM_ID, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { finalize, Observable } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { ADMIN_PERMISSIONS } from '../../../core/auth/permissions';
import {
  CoachingAdminExamListItem,
  CoachingAdminGoalListItem,
  CoachingAdminSessionListItem,
  CoachingAdminService
} from '../../../core/services/coaching-admin.service';
import { ToasterService } from '../../../core/services/toaster.service';

import { CoachingLabelPipe } from '../../coaching-portal/coaching-labels';
type Resource = 'sessions' | 'exams' | 'goals';
type OperationalPage = { items: unknown[]; totalCount: number; totalPages: number };

@Component({
  selector: 'app-coaching-operational',
  standalone: true,
  imports: [CoachingLabelPipe, CommonModule, FormsModule, RouterModule],
  template: `
    <section class="space-y-6">
      <div class="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 class="text-2xl font-bold text-(--ui-text)">Koçluk operasyonları</h1>
          <p class="text-sm text-(--ui-text-muted)">Seans, sınav ve akademik hedef kayıtlarını tek ekrandan izleyin.</p>
        </div>
        @if (canManage()) {
          <div class="flex flex-wrap gap-2">
            <a routerLink="/dashboard/coaching/operations/new/session" class="rounded-full border border-(--ui-border-strong) bg-(--ui-surface) px-4 py-2 text-sm font-semibold text-(--ui-text) hover:bg-(--ui-surface-muted)">Yeni seans</a>
            <a routerLink="/dashboard/coaching/operations/new/exam" class="rounded-full border border-(--ui-border-strong) bg-(--ui-surface) px-4 py-2 text-sm font-semibold text-(--ui-text) hover:bg-(--ui-surface-muted)">Yeni sınav</a>
            <a routerLink="/dashboard/coaching/operations/new/goal" class="rounded-full border border-(--ui-border-strong) bg-(--ui-surface) px-4 py-2 text-sm font-semibold text-(--ui-text) hover:bg-(--ui-surface-muted)">Yeni hedef</a>
          </div>
        }
      </div>

      <div class="ui-tab-list" role="group" aria-label="Koçluk kaynakları">
        <button type="button" (click)="select('sessions')" [attr.aria-pressed]="resource() === 'sessions'" class="ui-tab px-4 py-2 text-sm">Seanslar</button>
        <button type="button" (click)="select('exams')" [attr.aria-pressed]="resource() === 'exams'" class="ui-tab px-4 py-2 text-sm">Sınavlar</button>
        <button type="button" (click)="select('goals')" [attr.aria-pressed]="resource() === 'goals'" class="ui-tab px-4 py-2 text-sm">Hedefler</button>
      </div>

      <div class="flex flex-wrap items-end gap-3 rounded-xl border border-(--ui-border) bg-(--ui-surface) p-4 shadow-sm">
        <label class="min-w-0 flex-1 basis-64 text-sm text-(--ui-text-muted)">Arama
          <input [(ngModel)]="search" (keyup.enter)="applyFilters()" maxlength="200" class="mt-1 w-full min-w-0 rounded-lg border border-(--ui-border) px-3 py-2" placeholder="Başlık, konu veya açıklama" />
        </label>
        @if (resource() === 'sessions') {
          <label class="text-sm text-(--ui-text-muted)">Durum
            <select [(ngModel)]="status" (change)="applyFilters()" class="mt-1 rounded-lg border border-(--ui-border) px-3 py-2"><option value="">Tümü</option><option value="Scheduled">Planlandı</option><option value="Completed">Tamamlandı</option><option value="Cancelled">İptal</option></select>
          </label>
        }
        @if (resource() === 'goals') {
          <label class="text-sm text-(--ui-text-muted)">Tamamlanma
            <select [(ngModel)]="completed" (change)="applyFilters()" class="mt-1 rounded-lg border border-(--ui-border) px-3 py-2"><option value="">Tümü</option><option value="false">Devam ediyor</option><option value="true">Tamamlandı</option></select>
          </label>
        }
        <button type="button" (click)="applyFilters()" [disabled]="loading()" class="rounded-lg bg-(--ui-brand) px-4 py-2 text-sm font-medium text-(--ui-brand-contrast) disabled:opacity-50">Filtrele</button>
      </div>

      @if (error()) { <div class="rounded-lg border border-(--ui-danger-soft) bg-(--ui-danger-soft) p-4 text-sm text-(--ui-danger)">{{ error() }}</div> }
      @if (loading()) { <div class="rounded-xl border border-(--ui-border) bg-(--ui-surface) p-8 text-center text-(--ui-text-muted)">Yükleniyor…</div> }

      @if (resource() === 'sessions') {
        <div class="overflow-x-auto rounded-xl border border-(--ui-border) bg-(--ui-surface) shadow-sm"><table class="min-w-full divide-y divide-(--ui-border) text-sm"><thead class="bg-(--ui-surface-muted) text-left text-xs text-(--ui-text-muted)"><tr><th class="px-4 py-3">Başlık</th><th class="px-4 py-3">Tür</th><th class="px-4 py-3">Tarih</th><th class="px-4 py-3">Katılım</th><th class="px-4 py-3">Durum</th>@if (canManage()) { <th class="px-4 py-3">İşlem</th> }</tr></thead><tbody class="divide-y divide-(--ui-border)">@for (item of sessions(); track item.id) {<tr><td class="px-4 py-3 font-medium"><a [routerLink]="['/dashboard/coaching/operations/session', item.id]" class="text-(--ui-brand-text) hover:underline">{{ item.title }}</a></td><td class="px-4 py-3">{{ item.sessionType | coachingLabel }}</td><td class="px-4 py-3">{{ item.scheduledDate | date:'dd.MM.yyyy HH:mm' }}</td><td class="px-4 py-3">{{ item.presentCount }}/{{ item.studentCount }}</td><td class="px-4 py-3">{{ item.status | coachingLabel }}</td>@if (canManage()) { <td class="px-4 py-3"><div class="flex gap-2"><a [routerLink]="['/dashboard/coaching/operations/session', item.id, 'edit']" class="text-xs text-(--ui-brand-text) hover:underline">Düzenle</a>@if (item.status === 'Scheduled') { <button type="button" (click)="cancelSession(item.id)" [disabled]="actionLoading()" class="text-xs text-(--ui-warning) hover:underline">İptal</button> }<button type="button" (click)="deleteSession(item.id)" [disabled]="actionLoading()" class="text-xs text-(--ui-danger) hover:underline">Sil</button></div></td> }</tr>} @empty {<tr><td [attr.colspan]="canManage() ? 6 : 5" class="px-4 py-10 text-center text-(--ui-text-muted)">Kayıt bulunamadı.</td></tr>}</tbody></table></div>
      } @else if (resource() === 'exams') {
        <div class="overflow-x-auto rounded-xl border border-(--ui-border) bg-(--ui-surface) shadow-sm"><table class="min-w-full divide-y divide-(--ui-border) text-sm"><thead class="bg-(--ui-surface-muted) text-left text-xs text-(--ui-text-muted)"><tr><th class="px-4 py-3">Başlık</th><th class="px-4 py-3">Tür</th><th class="px-4 py-3">Tarih</th><th class="px-4 py-3">Maksimum puan</th><th class="px-4 py-3">Sonuç</th>@if (canManage()) { <th class="px-4 py-3">İşlem</th> }</tr></thead><tbody class="divide-y divide-(--ui-border)">@for (item of exams(); track item.id) {<tr><td class="px-4 py-3 font-medium"><a [routerLink]="['/dashboard/coaching/operations/exam', item.id]" class="text-(--ui-brand-text) hover:underline">{{ item.title }}</a></td><td class="px-4 py-3">{{ item.examType }}</td><td class="px-4 py-3">{{ item.examDate | date:'dd.MM.yyyy HH:mm' }}</td><td class="px-4 py-3">{{ item.maxScore }}</td><td class="px-4 py-3">{{ item.resultCount }}</td>@if (canManage()) { <td class="px-4 py-3"><div class="flex gap-2"><a [routerLink]="['/dashboard/coaching/operations/exam', item.id, 'edit']" class="text-xs text-(--ui-brand-text) hover:underline">Düzenle</a><button type="button" (click)="deleteExam(item.id)" [disabled]="actionLoading()" class="text-xs text-(--ui-danger) hover:underline">Sil</button></div></td> }</tr>} @empty {<tr><td [attr.colspan]="canManage() ? 6 : 5" class="px-4 py-10 text-center text-(--ui-text-muted)">Kayıt bulunamadı.</td></tr>}</tbody></table></div>
      } @else {
        <div class="overflow-x-auto rounded-xl border border-(--ui-border) bg-(--ui-surface) shadow-sm"><table class="min-w-full divide-y divide-(--ui-border) text-sm"><thead class="bg-(--ui-surface-muted) text-left text-xs text-(--ui-text-muted)"><tr><th class="px-4 py-3">Başlık</th><th class="px-4 py-3">Kategori</th><th class="px-4 py-3">İlerleme</th><th class="px-4 py-3">Hedef tarih</th><th class="px-4 py-3">Durum</th>@if (canManage()) { <th class="px-4 py-3">İşlem</th> }</tr></thead><tbody class="divide-y divide-(--ui-border)">@for (item of goals(); track item.id) {<tr><td class="px-4 py-3 font-medium"><a [routerLink]="['/dashboard/coaching/operations/goal', item.id, 'edit']" class="text-(--ui-brand-text) hover:underline">{{ item.title }}</a></td><td class="px-4 py-3">{{ item.category | coachingLabel }}</td><td class="px-4 py-3"><div class="flex items-center gap-2"><input type="number" min="0" max="100" [ngModel]="progressFor(item.id, item.currentProgress)" (ngModelChange)="setProgress(item.id, $event)" class="w-20 rounded border border-(--ui-border) px-2 py-1 text-sm" /><button type="button" (click)="updateGoalProgress(item.id)" [disabled]="actionLoading()" class="text-xs text-(--ui-brand-text) hover:underline">Kaydet</button></div></td><td class="px-4 py-3">{{ item.targetDate ? (item.targetDate | date:'dd.MM.yyyy') : '—' }}</td><td class="px-4 py-3">{{ item.isCompleted ? 'Tamamlandı' : 'Devam ediyor' }}</td>@if (canManage()) { <td class="px-4 py-3"><div class="flex gap-2"><a [routerLink]="['/dashboard/coaching/operations/goal', item.id, 'edit']" class="text-xs text-(--ui-brand-text) hover:underline">Düzenle</a><button type="button" (click)="deleteGoal(item.id)" [disabled]="actionLoading()" class="text-xs text-(--ui-danger) hover:underline">Sil</button></div></td> }</tr>} @empty {<tr><td [attr.colspan]="canManage() ? 6 : 5" class="px-4 py-10 text-center text-(--ui-text-muted)">Kayıt bulunamadı.</td></tr>}</tbody></table></div>
      }

      <div class="flex items-center justify-between text-sm text-(--ui-text-muted)"><span>{{ totalCount() }} kayıt, sayfa {{ page() }} / {{ totalPages() }}</span><div class="flex gap-2"><button type="button" (click)="goTo(page() - 1)" [disabled]="page() <= 1 || loading()" class="rounded border px-3 py-1 disabled:opacity-40">Önceki</button><button type="button" (click)="goTo(page() + 1)" [disabled]="page() >= totalPages() || loading()" class="rounded border px-3 py-1 disabled:opacity-40">Sonraki</button></div></div>
    </section>
  `
})
export class CoachingOperationalComponent implements OnInit {
  private readonly service = inject(CoachingAdminService);
  private readonly toaster = inject(ToasterService);
  private readonly auth = inject(AuthService);
  private readonly platformId = inject(PLATFORM_ID);
  private readonly route = inject(ActivatedRoute, { optional: true });
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

  ngOnInit() {
    if (!isPlatformBrowser(this.platformId)) return;
    const requestedResource = this.route?.snapshot.queryParamMap.get('resource');
    if (requestedResource === 'sessions' || requestedResource === 'exams' || requestedResource === 'goals') {
      this.resource.set(requestedResource);
    }
    this.load();
  }
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
        this.totalPages.set(Math.max(1, response.totalPages || 1));
        if (this.resource() === 'sessions') this.sessions.set(response.items as CoachingAdminSessionListItem[]);
        if (this.resource() === 'exams') this.exams.set(response.items as CoachingAdminExamListItem[]);
        if (this.resource() === 'goals') this.goals.set(response.items as CoachingAdminGoalListItem[]);
      },
      error: () => this.error.set('Koçluk kayıtları yüklenemedi. Sayfayı yenileyip yeniden deneyin.')
    });
  }

  progressFor(id: string, fallback: number) { return this.progressDraft[id] ?? fallback; }
  setProgress(id: string, value: number) { this.progressDraft[id] = Number(value); }

  async cancelSession(id: string): Promise<void> {
    if (!await this.toaster.confirm('Bu seans iptal edilsin mi?', { title: 'Seansı iptal et' })) return;
    this.actionLoading.set(true);
    this.service.cancelSession(id).pipe(finalize(() => this.actionLoading.set(false))).subscribe({
      next: () => this.load(),
      error: () => this.error.set('Seans iptal edilemedi.')
    });
  }

  async deleteSession(id: string): Promise<void> {
    if (!await this.toaster.confirm('Bu seans kalıcı olarak silinsin mi?', { title: 'Seansı kalıcı olarak sil' })) return;
    this.actionLoading.set(true);
    this.service.deleteSession(id).pipe(finalize(() => this.actionLoading.set(false))).subscribe({
      next: () => this.load(),
      error: (e: { status?: number; error?: { error?: string } }) => this.error.set(e?.status === 409 && e.error?.error ? e.error.error : 'Seans silinemedi.')
    });
  }

  async deleteExam(id: string): Promise<void> {
    if (!await this.toaster.confirm('Bu sınav kalıcı olarak silinsin mi?', { title: 'Sınavı kalıcı olarak sil' })) return;
    this.actionLoading.set(true);
    this.service.deleteExam(id).pipe(finalize(() => this.actionLoading.set(false))).subscribe({
      next: () => this.load(),
      error: (e: { status?: number; error?: { error?: string } }) => this.error.set(e?.status === 409 && e.error?.error ? e.error.error : 'Sınav silinemedi.')
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

  async deleteGoal(id: string): Promise<void> {
    if (!await this.toaster.confirm('Bu hedef kalıcı olarak silinsin mi?', { title: 'Hedefi kalıcı olarak sil' })) return;
    this.actionLoading.set(true);
    this.service.deleteGoal(id).pipe(finalize(() => this.actionLoading.set(false))).subscribe({
      next: () => this.load(),
      error: () => this.error.set('Hedef silinemedi.')
    });
  }
}
