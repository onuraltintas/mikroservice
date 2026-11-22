import { CommonModule, isPlatformBrowser } from '@angular/common';
import { Component, inject, OnInit, PLATFORM_ID, signal } from '@angular/core';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { ADMIN_PERMISSIONS } from '../../../core/auth/permissions';
import {
  CoachingAdminAssignmentDetail,
  CoachingAdminService
} from '../../../core/services/coaching-admin.service';
import { ToasterService } from '../../../core/services/toaster.service';

import { CoachingLabelPipe } from '../../coaching-portal/coaching-labels';
@Component({
  selector: 'app-coaching-assignment-detail',
  standalone: true,
  imports: [CoachingLabelPipe, CommonModule, FormsModule, RouterModule],
  template: `
    <section class="space-y-6">
      <a routerLink="/dashboard/coaching/assignments" class="inline-flex text-sm font-medium text-(--ui-brand-text) hover:underline">← Ödev listesine dön</a>

      @if (loading()) {
        <div class="rounded-xl border border-(--ui-border) bg-(--ui-surface) p-8 text-center text-(--ui-text-muted)">Yükleniyor…</div>
      } @else if (error()) {
        <div class="rounded-lg border border-(--ui-danger-soft) bg-(--ui-danger-soft) p-4 text-sm text-(--ui-danger)">{{ error() }}</div>
      } @else if (assignment(); as item) {
        <div class="rounded-xl border border-(--ui-border) bg-(--ui-surface) p-6 shadow-sm">
          <div class="flex flex-wrap items-start justify-between gap-3">
            <div>
              <h1 class="text-2xl font-bold text-(--ui-text)">{{ item.title }}</h1>
              <p class="mt-1 text-sm text-(--ui-text-muted)">{{ item.source | coachingLabel }}, {{ item.status | coachingLabel }} · Son tarih {{ item.dueDate | date:'dd.MM.yyyy HH:mm' }}</p>
            </div>
            <div class="flex flex-wrap gap-2">
              <button type="button" (click)="load()" [disabled]="loading() || actionLoading()" class="rounded-lg border border-(--ui-border) px-4 py-2 text-sm">Yenile</button>
              @if (canManage()) {
                <a [routerLink]="['/dashboard/coaching/assignments', item.id, 'edit']" class="rounded-lg border border-(--ui-brand) px-4 py-2 text-sm text-(--ui-brand-text)">Düzenle</a>
              }
              @if (canManage() && item.status === 'Active') {
                <button type="button" (click)="cancelAssignment(item.id)" [disabled]="actionLoading()" class="rounded-lg border border-(--ui-warning) px-4 py-2 text-sm text-(--ui-warning)">İptal et</button>
              }
              @if (canManage()) {
                <button type="button" (click)="deleteAssignment(item.id)" [disabled]="actionLoading()" class="rounded-lg border border-(--ui-danger) px-4 py-2 text-sm text-(--ui-danger)">Sil</button>
              }
            </div>
          </div>
          @if (item.bookTitle) {
            <p class="mt-4 rounded-lg bg-(--ui-brand-soft) p-3 text-sm text-(--ui-brand-text)">
              Kitap: {{ item.bookTitle }} · sayfa {{ item.bookStartPage }}-{{ item.bookEndPage }}
              @if (item.bookStartQuestion) { · soru {{ item.bookStartQuestion }}-{{ item.bookEndQuestion }} }
            </p>
          }
        </div>

        <div class="overflow-x-auto rounded-xl border border-(--ui-border) bg-(--ui-surface) shadow-sm">
          <table class="min-w-full divide-y divide-(--ui-border) text-sm">
            <thead class="bg-(--ui-surface-muted) text-left text-xs text-(--ui-text-muted)">
              <tr><th class="px-4 py-3">Öğrenci</th><th class="px-4 py-3">Durum</th><th class="px-4 py-3">Teslim</th><th class="px-4 py-3">Puan</th><th class="px-4 py-3">Geri bildirim</th><th class="px-4 py-3">Fotoğraflar</th>@if (canManage()) { <th class="px-4 py-3">İşlem</th> }</tr>
            </thead>
            <tbody class="divide-y divide-(--ui-border)">
              @for (student of item.assignedStudents; track student.studentId) {
                <tr>
                  <td class="px-4 py-3 font-mono text-xs">{{ student.studentId }}</td>
                  <td class="px-4 py-3">{{ student.status | coachingLabel }}</td>
                  <td class="px-4 py-3">{{ student.submittedAt ? (student.submittedAt | date:'dd.MM.yyyy HH:mm') : '—' }}</td>
                  <td class="px-4 py-3">{{ student.score ?? '—' }}</td>
                  <td class="max-w-xs px-4 py-3">{{ student.teacherFeedback || '—' }}</td>
                  <td class="px-4 py-3">
                    @for (attachment of student.attachments ?? []; track attachment.id) {
                      @if (attachment.status === 'Clean') {
                        <a class="mr-2 inline-block text-(--ui-brand-text) hover:underline" href="#" (click)="openAttachment($event, item.id, student.studentId, attachment.id)">{{ attachment.originalFileName }}</a>
                      } @else { <span class="mr-2 text-(--ui-warning)">{{ attachment.originalFileName }} ({{ attachment.status | coachingLabel }})</span> }
                    } @empty { <span class="text-(--ui-text-muted)">—</span> }
                  </td>
                  @if (canManage()) { <td class="min-w-64 px-4 py-3">
                    <div class="space-y-2">
                      <input type="number" min="0" [max]="item.maxScore ?? 100" [ngModel]="scoreFor(student.studentId)" (ngModelChange)="setScore(student.studentId, $event)" class="w-24 rounded border border-(--ui-border) px-2 py-1 text-sm" placeholder="Puan" />
                      <input type="text" maxlength="1000" [ngModel]="feedbackFor(student.studentId)" (ngModelChange)="setFeedback(student.studentId, $event)" class="w-full rounded border border-(--ui-border) px-2 py-1 text-sm" placeholder="Geri bildirim" />
                      <button type="button" (click)="gradeAssignment(item.id, student.studentId)" [disabled]="actionLoading() || scoreFor(student.studentId) === ''" class="rounded bg-(--ui-brand) px-3 py-1 text-xs font-medium text-(--ui-brand-contrast) disabled:opacity-50">Notu kaydet</button>
                    </div>
                  </td> }
                </tr>
              } @empty {
                <tr><td [attr.colspan]="canManage() ? 7 : 6" class="px-4 py-10 text-center text-(--ui-text-muted)">Atanmış öğrenci bulunamadı.</td></tr>
              }
            </tbody>
          </table>
        </div>
      }
    </section>
  `
})
export class CoachingAssignmentDetailComponent implements OnInit {
  private readonly service = inject(CoachingAdminService);
  private readonly toaster = inject(ToasterService);
  private readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly platformId = inject(PLATFORM_ID);
  readonly assignment = signal<CoachingAdminAssignmentDetail | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly actionLoading = signal(false);
  private readonly scoreDraft: Record<string, string> = {};
  private readonly feedbackDraft: Record<string, string> = {};

  canManage() { return this.auth.hasPermission(ADMIN_PERMISSIONS.coachingManage); }

  ngOnInit() { if (isPlatformBrowser(this.platformId)) this.load(); }

  load() {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) { this.error.set('Ödev kimliği bulunamadı.'); return; }
    this.loading.set(true);
    this.error.set(null);
    this.service.getAssignment(id)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: value => this.assignment.set(value),
        error: () => this.error.set('Ödev detayı yüklenemedi. Sayfayı yenileyip yeniden deneyin.')
      });
  }

  openAttachment(event: Event, assignmentId: string, studentId: string, attachmentId: string) {
    event.preventDefault();
    this.service.downloadAttachment(assignmentId, studentId, attachmentId).subscribe({
      next: content => {
        if (typeof window === 'undefined') return;
        const url = URL.createObjectURL(content);
        window.open(url, '_blank', 'noopener');
        window.setTimeout(() => URL.revokeObjectURL(url), 60_000);
      },
      error: () => this.error.set('Attachment indirilemedi.')
    });
  }

  scoreFor(studentId: string) { return this.scoreDraft[studentId] ?? ''; }
  feedbackFor(studentId: string) { return this.feedbackDraft[studentId] ?? ''; }
  setScore(studentId: string, value: string) { this.scoreDraft[studentId] = value; }
  setFeedback(studentId: string, value: string) { this.feedbackDraft[studentId] = value; }

  async cancelAssignment(id: string): Promise<void> {
    if (!await this.toaster.confirm('Bu ödev iptal edilsin mi?', { title: 'Ödevi iptal et' })) return;
    this.actionLoading.set(true);
    this.service.cancelAssignment(id).pipe(finalize(() => this.actionLoading.set(false))).subscribe({
      next: () => this.load(),
      error: () => this.error.set('Ödev iptal edilemedi.')
    });
  }

  async deleteAssignment(id: string): Promise<void> {
    if (!await this.toaster.confirm('Bu ödev kalıcı olarak silinsin mi?', { title: 'Ödevi kalıcı olarak sil' })) return;
    this.actionLoading.set(true);
    this.service.deleteAssignment(id).pipe(finalize(() => this.actionLoading.set(false))).subscribe({
      next: () => this.router.navigate(['/dashboard/coaching/assignments']),
      error: (e: { status?: number; error?: { error?: string } }) => this.error.set(e?.status === 409 && e.error?.error ? e.error.error : 'Ödev silinemedi.')
    });
  }

  gradeAssignment(assignmentId: string, studentId: string) {
    const score = Number(this.scoreFor(studentId));
    if (!Number.isFinite(score)) return;
    this.actionLoading.set(true);
    this.service.gradeAssignment(assignmentId, {
      assignmentId,
      studentId,
      score,
      teacherFeedback: this.feedbackFor(studentId) || undefined
    }).pipe(finalize(() => this.actionLoading.set(false))).subscribe({
      next: () => this.load(),
      error: () => this.error.set('Not kaydedilemedi.')
    });
  }
}
