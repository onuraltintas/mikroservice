import { CommonModule } from '@angular/common';
import { Component, DestroyRef, Input, OnChanges, inject, signal } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize, Subscription } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { ADMIN_PERMISSIONS } from '../../../core/auth/permissions';
import { StudyAvailability, StudyPlan, StudyPlanStatus, StudyPlanSummary } from '../../../core/services/coaching-study-planning.service';
import { environment } from '../../../../environments/environment';
import { StudentStudyReportComponent } from '../../coaching-portal/pages/student-study-report.component';
import { CoachingStudyCorrectionsComponent } from './coaching-study-corrections';

interface RevisionSummary extends StudyPlanSummary { planId: string; revisionNumber: number; createdAt: string; automaticAvailabilityVersion: number | null; }
interface RevisionPage { items: RevisionSummary[]; totalCount: number; pageNumber: number; pageSize: number; }

@Component({ selector: 'app-coaching-admin-study', standalone: true, imports: [CommonModule, FormsModule, StudentStudyReportComponent, CoachingStudyCorrectionsComponent], template: `
  @if(permitted()) {
    <section class="space-y-4 rounded-xl border p-4" aria-labelledby="admin-study-title">
      <h3 id="admin-study-title" class="text-lg font-semibold">Kişisel çalışma planı ve hedef incelemesi</h3>
      <p class="text-sm text-gray-500">Salt okunur global yönetici incelemesi. Bu ekrandan öğrenci beyanı, plan geçmişi veya hedef sonuçları değiştirilemez.</p>
      <h4 class="font-medium">Haftalık müsaitlik</h4>
      @if(availabilityLoading()) { <p role="status">Müsaitlik yükleniyor…</p> }
      @if(availabilityError()) { <p role="alert" class="text-red-700">{{ availabilityError() }}</p> }
      @else if(availability(); as value) {
        <p class="text-sm">Saat dilimi: {{ value.timeZoneId }} · Sürüm: {{ value.version }}</p>
        <ul class="text-sm">@for(window of value.windows; track $index) { <li>{{ dayLabel(window.day) }} · {{ time(window.startMinute) }}–{{ time(window.endMinute) }}</li> }</ul>
        @if(!value.windows.length) { <p class="text-sm">Haftalık zaman aralığı belirlenmemiş.</p> }
      } @else if(!availabilityLoading()) { <p class="text-sm">Müsaitlik kaydı yok.</p> }
      <button type="button" (click)="loadAvailability()" [disabled]="availabilityLoading()" class="rounded border px-3 py-2 text-sm">Müsaitliği yenile</button>
      <h4 class="font-medium">Plan revizyonları ve geçmiş</h4>
      <div class="flex flex-wrap gap-3"><label class="text-sm">Başlık ara<input [(ngModel)]="search" maxlength="100" class="ml-2 rounded border p-2 dark:bg-gray-800" /></label><label class="text-sm">Durum<select [(ngModel)]="status" class="ml-2 rounded border p-2 dark:bg-gray-800"><option value="">Tümü</option><option value="Draft">Taslak</option><option value="Active">Aktif</option><option value="Archived">Arşiv</option></select></label><button type="button" (click)="loadPlans()" class="rounded border px-3 py-2">Filtrele / yeniden dene</button></div>
      @if(loading()) { <p role="status">Plan geçmişi yükleniyor…</p> }
      @if(error()) { <p role="alert" class="text-red-700">{{ error() }}</p> }
      @if(plans(); as page) {
        <p class="text-sm">{{ page.totalCount }} revizyon · {{ page.pageNumber }}. sayfa</p>
        @if(!page.items.length) { <p class="text-sm">Bu filtrelerle plan revizyonu bulunamadı.</p> }
        <div class="overflow-x-auto"><table class="w-full text-left text-sm"><thead><tr><th>Başlık</th><th>Revizyon</th><th>Durum / yöntem</th><th>Oluşturulma</th><th>İncele</th></tr></thead><tbody>@for(row of page.items; track row.id) { <tr class="border-t"><td class="p-2">{{ row.title }}<span class="block break-all text-xs text-gray-500">Plan: {{ row.planId }}</span></td><td>{{ row.revisionNumber }} (sürüm {{ row.version }})</td><td>{{ statusLabel(row.status) }} · {{ row.automaticAvailabilityVersion == null ? 'Manuel' : 'Otomatik' }}</td><td>{{ row.createdAt | date:'dd.MM.yyyy HH:mm' }}</td><td><button type="button" (click)="inspect(row.id)" class="rounded border px-2 py-1" [attr.aria-label]="row.title + ' revizyon ' + row.revisionNumber + ' görevlerini incele'">Görevler</button></td></tr> }</tbody></table></div>
        <div class="flex gap-3"><button type="button" (click)="loadPlans(page.pageNumber - 1)" [disabled]="loading() || page.pageNumber <= 1" class="rounded border px-3 py-1">Önceki</button><button type="button" (click)="loadPlans(page.pageNumber + 1)" [disabled]="loading() || page.pageNumber * 25 >= page.totalCount" class="rounded border px-3 py-1">Sonraki</button></div>
      }
      @if(detailLoading()) { <p role="status">Revizyon görevleri yükleniyor…</p> }
      @if(selected(); as plan) {
        <h4 class="font-medium">{{ plan.title }} — {{ statusLabel(plan.status) }} revizyon görevleri</h4>
        <div class="overflow-x-auto"><table class="w-full text-left text-sm"><thead><tr><th>Tarih / görev</th><th>Konu kimliği</th><th>Planlanan / kaydedilen dakika</th><th>Durum</th><th>Tamamlanma</th></tr></thead><tbody>@for(task of plan.tasks; track task.id) { <tr class="border-t"><td class="p-2">{{ task.plannedDate | date:'dd.MM.yyyy':'UTC' }} · {{ task.title }}</td><td class="break-all">{{ task.topicId || 'Serbest görev' }}</td><td>{{ task.plannedMinutes }} / {{ task.actualMinutes ?? 'Kayıt yok' }}</td><td>{{ task.isCompleted ? 'Tamamlandı' : 'Bekliyor' }} {{ task.isPinned ? '· Sabit' : '' }}</td><td>{{ task.completedAt ? (task.completedAt | date:'dd.MM.yyyy HH:mm') : '—' }}</td></tr> }</tbody></table></div>
        @if(!plan.tasks.length) { <p class="text-sm">Bu revizyonda görev yok.</p> }
      }
      <app-student-study-report [adminStudentId]="studentId" />
      @if(canCorrect()) {
        <button type="button" (click)="correctionOpen.set(!correctionOpen())" class="rounded border px-3 py-2">{{correctionOpen() ? 'Düzeltmeyi kapat' : 'Düzeltme ve işlem geçmişini aç'}}</button>
        @if(correctionOpen()) {<app-coaching-study-corrections [studentId]="studentId" [plan]="selected()" (saved)="refreshAfterCorrection()" />}
      }
    </section>
  }` })
export class CoachingAdminStudyComponent implements OnChanges {
  @Input({ required: true }) studentId!: string;
  private readonly http = inject(HttpClient); private readonly auth = inject(AuthService); private readonly destroyRef = inject(DestroyRef);
  private requests: Partial<Record<'availability' | 'plans' | 'detail', Subscription>> = {};
  readonly availability = signal<StudyAvailability | null>(null); readonly availabilityLoading = signal(false); readonly availabilityError = signal('');
  readonly plans = signal<RevisionPage | null>(null); readonly selected = signal<StudyPlan | null>(null);
  readonly loading = signal(false); readonly detailLoading = signal(false); readonly error = signal('');
  readonly correctionOpen=signal(false);
  canCorrect(){return this.permitted()&&this.auth.hasPermission('Permissions.Coaching.Manage');}
  refreshAfterCorrection(){const id=this.selected()?.id;this.loadPlans();if(id)this.inspect(id);}
  search = ''; status: StudyPlanStatus | '' = '';
  permitted() { return !!this.auth.userProfile()?.roles?.includes('SystemAdmin') && this.auth.hasPermission(ADMIN_PERMISSIONS.coachingView); }
  private url() { return `${environment.apiUrl}/coaching-admin/students/${encodeURIComponent(this.studentId)}/study`; }
  ngOnChanges() {
    this.correctionOpen.set(false);
    Object.values(this.requests).forEach(request => request?.unsubscribe());
    this.availability.set(null); this.plans.set(null); this.selected.set(null); this.error.set(''); this.availabilityError.set(''); this.search = ''; this.status = '';
    if (this.permitted() && this.studentId) { this.loadAvailability(); this.loadPlans(); }
  }
  loadAvailability() {
    if (!this.permitted() || !this.studentId) return;
    this.requests.availability?.unsubscribe(); this.availability.set(null); this.availabilityError.set(''); this.availabilityLoading.set(true);
    this.requests.availability = this.http.get<{ data: StudyAvailability | null }>(`${this.url()}/availability`).pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.availabilityLoading.set(false))).subscribe({ next: value => this.availability.set(value.data), error: () => this.availabilityError.set('Müsaitlik yüklenemedi. Yeniden deneyin.') });
  }
  loadPlans(page = 1) {
    if (!this.permitted() || !this.studentId || page < 1) return;
    this.requests.plans?.unsubscribe(); this.requests.detail?.unsubscribe(); this.plans.set(null); this.selected.set(null); this.error.set(''); this.loading.set(true);
    let params = new HttpParams().set('pageNumber', page).set('pageSize', 25).set('search', this.search.trim()); if (this.status) params = params.set('status', this.status);
    this.requests.plans = this.http.get<{ data: RevisionPage }>(`${this.url()}/plans`, { params }).pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.loading.set(false))).subscribe({ next: value => this.plans.set(value.data), error: () => this.error.set('Plan revizyonları yüklenemedi. Filtreleri kontrol edip yeniden deneyin.') });
  }
  inspect(id: string) {
    if (!this.permitted() || !this.studentId) return;
    this.requests.detail?.unsubscribe(); this.selected.set(null); this.error.set(''); this.detailLoading.set(true);
    this.requests.detail = this.http.get<{ data: StudyPlan }>(`${this.url()}/plans/${encodeURIComponent(id)}`).pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.detailLoading.set(false))).subscribe({ next: value => this.selected.set(value.data), error: () => this.error.set('Revizyon ayrıntısı yüklenemedi. Görevleri yeniden seçin.') });
  }
  statusLabel(status: StudyPlanStatus) { return { Draft: 'Taslak', Active: 'Aktif', Archived: 'Arşiv' }[status]; }
  dayLabel(day: string) { return ({ Monday: 'Pazartesi', Tuesday: 'Salı', Wednesday: 'Çarşamba', Thursday: 'Perşembe', Friday: 'Cuma', Saturday: 'Cumartesi', Sunday: 'Pazar' } as Record<string, string>)[day] || day; }
  time(minute: number) { return `${String(Math.floor(minute / 60)).padStart(2, '0')}:${String(minute % 60).padStart(2, '0')}`; }
}
