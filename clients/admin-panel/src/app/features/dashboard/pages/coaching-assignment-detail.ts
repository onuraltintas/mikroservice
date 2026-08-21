import { CommonModule, isPlatformBrowser } from '@angular/common';
import { Component, inject, OnInit, PLATFORM_ID, signal } from '@angular/core';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { finalize } from 'rxjs';
import {
  CoachingAdminAssignmentDetail,
  CoachingAdminService
} from '../../../core/services/coaching-admin.service';

@Component({
  selector: 'app-coaching-assignment-detail',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <section class="space-y-6">
      <a routerLink="/coaching/assignments" class="inline-flex text-sm font-medium text-indigo-600 hover:underline">← Ödev listesine dön</a>

      @if (loading()) {
        <div class="rounded-xl border border-gray-200 bg-white p-8 text-center text-gray-500 dark:border-gray-700 dark:bg-gray-800">Yükleniyor…</div>
      } @else if (error()) {
        <div class="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700">{{ error() }}</div>
      } @else if (assignment(); as item) {
        <div class="rounded-xl border border-gray-200 bg-white p-6 shadow-sm dark:border-gray-700 dark:bg-gray-800">
          <div class="flex flex-wrap items-start justify-between gap-3">
            <div>
              <h1 class="text-2xl font-bold text-gray-900 dark:text-white">{{ item.title }}</h1>
              <p class="mt-1 text-sm text-gray-500 dark:text-gray-400">{{ item.source }} · {{ item.status }} · Son tarih {{ item.dueDate | date:'dd.MM.yyyy HH:mm' }}</p>
            </div>
            <button type="button" (click)="load()" [disabled]="loading()" class="rounded-lg border border-gray-300 px-4 py-2 text-sm dark:border-gray-600">Yenile</button>
          </div>
          @if (item.bookTitle) {
            <p class="mt-4 rounded-lg bg-indigo-50 p-3 text-sm text-indigo-900 dark:bg-indigo-950/40 dark:text-indigo-200">
              Kitap: {{ item.bookTitle }} · sayfa {{ item.bookStartPage }}-{{ item.bookEndPage }}
              @if (item.bookStartQuestion) { · soru {{ item.bookStartQuestion }}-{{ item.bookEndQuestion }} }
            </p>
          }
        </div>

        <div class="overflow-x-auto rounded-xl border border-gray-200 bg-white shadow-sm dark:border-gray-700 dark:bg-gray-800">
          <table class="min-w-full divide-y divide-gray-200 text-sm dark:divide-gray-700">
            <thead class="bg-gray-50 text-left text-xs uppercase text-gray-500 dark:bg-gray-900/40 dark:text-gray-400">
              <tr><th class="px-4 py-3">Öğrenci</th><th class="px-4 py-3">Durum</th><th class="px-4 py-3">Teslim</th><th class="px-4 py-3">Puan</th><th class="px-4 py-3">Geri bildirim</th><th class="px-4 py-3">Fotoğraflar</th></tr>
            </thead>
            <tbody class="divide-y divide-gray-200 dark:divide-gray-700">
              @for (student of item.assignedStudents; track student.studentId) {
                <tr>
                  <td class="px-4 py-3 font-mono text-xs">{{ student.studentId }}</td>
                  <td class="px-4 py-3">{{ student.status }}</td>
                  <td class="px-4 py-3">{{ student.submittedAt ? (student.submittedAt | date:'dd.MM.yyyy HH:mm') : '—' }}</td>
                  <td class="px-4 py-3">{{ student.score ?? '—' }}</td>
                  <td class="max-w-xs px-4 py-3">{{ student.teacherFeedback || '—' }}</td>
                  <td class="px-4 py-3">
                    @for (attachment of student.attachments ?? []; track attachment.id) {
                      @if (attachment.status === 'Clean') {
                        <a class="mr-2 inline-block text-indigo-600 hover:underline" href="#" (click)="openAttachment($event, item.id, student.studentId, attachment.id)">{{ attachment.originalFileName }}</a>
                      } @else { <span class="mr-2 text-amber-600">{{ attachment.originalFileName }} ({{ attachment.status }})</span> }
                    } @empty { <span class="text-gray-400">—</span> }
                  </td>
                </tr>
              } @empty {
                <tr><td colspan="6" class="px-4 py-10 text-center text-gray-500">Atanmış öğrenci bulunamadı.</td></tr>
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
  private readonly route = inject(ActivatedRoute);
  private readonly platformId = inject(PLATFORM_ID);
  readonly assignment = signal<CoachingAdminAssignmentDetail | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

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
        error: () => this.error.set('Ödev detayı yüklenemedi.')
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
}
