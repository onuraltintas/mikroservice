import { CommonModule, isPlatformBrowser } from '@angular/common';
import { Component, inject, OnInit, PLATFORM_ID, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { finalize } from 'rxjs';
import { ADMIN_PERMISSIONS } from '../../../core/auth/permissions';
import {
  CoachingAdminAssignmentDetail,
  CoachingAdminAssignmentUpdateRequest,
  CoachingAdminService
} from '../../../core/services/coaching-admin.service';

@Component({
  selector: 'app-coaching-assignment-edit',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  template: `
    <section class="mx-auto max-w-4xl space-y-6">
      <a [routerLink]="['/dashboard/coaching/assignments', id]" class="text-sm font-medium text-indigo-600 hover:underline">← Ödev detayına dön</a>
      <div>
        <h1 class="text-2xl font-bold text-gray-900 dark:text-white">Ödevi düzenle</h1>
        <p class="text-sm text-gray-500 dark:text-gray-400">Düzenlenen tüm alanlar tek işlemde kaydedilir. Öğrenci değişikliği teslim edilmiş çalışmaları silemez.</p>
      </div>
      @if (loading()) { <div class="rounded-xl border bg-white p-8 text-center text-gray-500 dark:border-gray-700 dark:bg-gray-800">Yükleniyor…</div> }
      @if (error()) { <div class="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700">{{ error() }}</div> }
      @if (!loading()) {
        <form (ngSubmit)="save()" class="space-y-5 rounded-xl border border-gray-200 bg-white p-6 shadow-sm dark:border-gray-700 dark:bg-gray-800">
          <div class="grid gap-4 md:grid-cols-2">
            <label class="text-sm text-gray-700 dark:text-gray-200">Başlık<input [(ngModel)]="form.title" name="title" required maxlength="200" class="mt-1 w-full rounded border px-3 py-2 dark:border-gray-600 dark:bg-gray-900" /></label>
            <label class="text-sm text-gray-700 dark:text-gray-200">Konu<input [(ngModel)]="form.subject" name="subject" maxlength="200" class="mt-1 w-full rounded border px-3 py-2 dark:border-gray-600 dark:bg-gray-900" /></label>
          </div>
          <label class="block text-sm text-gray-700 dark:text-gray-200">Açıklama<textarea [(ngModel)]="form.description" name="description" maxlength="2000" rows="3" class="mt-1 w-full rounded border px-3 py-2 dark:border-gray-600 dark:bg-gray-900"></textarea></label>
          <div class="grid gap-4 md:grid-cols-3">
            <label class="text-sm text-gray-700 dark:text-gray-200">Kaynak<select [(ngModel)]="form.assignmentSource" name="source" class="mt-1 w-full rounded border px-3 py-2 dark:border-gray-600 dark:bg-gray-900"><option value="Digital">Dijital</option><option value="Book">Kitap</option><option value="Mixed">Dijital + kitap</option></select></label>
            <label class="text-sm text-gray-700 dark:text-gray-200">Son tarih<input [(ngModel)]="form.dueDate" name="dueDate" type="datetime-local" required class="mt-1 w-full rounded border px-3 py-2 dark:border-gray-600 dark:bg-gray-900" /></label>
            <label class="text-sm text-gray-700 dark:text-gray-200">Sınıf seviyesi<input [(ngModel)]="form.targetGradeLevel" name="grade" type="number" min="1" max="12" class="mt-1 w-full rounded border px-3 py-2 dark:border-gray-600 dark:bg-gray-900" /></label>
          </div>
          @if (form.assignmentSource !== 'Digital') {
            <div class="space-y-4 rounded-lg border border-indigo-100 bg-indigo-50 p-4 dark:border-indigo-900 dark:bg-indigo-950/30">
              <h2 class="font-semibold text-indigo-900 dark:text-indigo-200">Kitap ödevi</h2>
              <div class="grid gap-4 md:grid-cols-2"><label class="text-sm">Kitap adı<input [(ngModel)]="form.bookTitle" name="bookTitle" maxlength="200" required class="mt-1 w-full rounded border px-3 py-2 dark:border-gray-600 dark:bg-gray-900" /></label><label class="text-sm">ISBN<input [(ngModel)]="form.bookIsbn" name="isbn" class="mt-1 w-full rounded border px-3 py-2 dark:border-gray-600 dark:bg-gray-900" /></label></div>
              <div class="grid gap-4 md:grid-cols-4"><label class="text-sm">İlk sayfa<input [(ngModel)]="form.bookStartPage" name="startPage" type="number" min="1" required class="mt-1 w-full rounded border px-2 py-2 dark:border-gray-600 dark:bg-gray-900" /></label><label class="text-sm">Son sayfa<input [(ngModel)]="form.bookEndPage" name="endPage" type="number" min="1" required class="mt-1 w-full rounded border px-2 py-2 dark:border-gray-600 dark:bg-gray-900" /></label><label class="text-sm">İlk soru<input [(ngModel)]="form.bookStartQuestion" name="startQuestion" type="number" min="1" class="mt-1 w-full rounded border px-2 py-2 dark:border-gray-600 dark:bg-gray-900" /></label><label class="text-sm">Son soru<input [(ngModel)]="form.bookEndQuestion" name="endQuestion" type="number" min="1" class="mt-1 w-full rounded border px-2 py-2 dark:border-gray-600 dark:bg-gray-900" /></label></div>
              <div class="grid gap-4 md:grid-cols-2"><label class="text-sm">Baskı<input [(ngModel)]="form.bookEdition" name="edition" class="mt-1 w-full rounded border px-3 py-2 dark:border-gray-600 dark:bg-gray-900" /></label><label class="text-sm">Bölüm<input [(ngModel)]="form.bookChapter" name="chapter" class="mt-1 w-full rounded border px-3 py-2 dark:border-gray-600 dark:bg-gray-900" /></label></div>
            </div>
          }
          <div class="grid gap-4 md:grid-cols-3"><label class="text-sm">Tahmini süre (dk)<input [(ngModel)]="form.estimatedDurationMinutes" name="duration" type="number" min="1" max="240" class="mt-1 w-full rounded border px-3 py-2 dark:border-gray-600 dark:bg-gray-900" /></label><label class="text-sm">Maksimum puan<input [(ngModel)]="form.maxScore" name="maxScore" type="number" min="0.01" max="999.99" step="0.01" class="mt-1 w-full rounded border px-3 py-2 dark:border-gray-600 dark:bg-gray-900" /></label><label class="text-sm">Geçme puanı<input [(ngModel)]="form.passingScore" name="passingScore" type="number" min="0" max="999.99" step="0.01" class="mt-1 w-full rounded border px-3 py-2 dark:border-gray-600 dark:bg-gray-900" /></label></div>
          <label class="block text-sm text-gray-700 dark:text-gray-200">Öğrenci kimlikleri (virgülle ayırın; boş bırakılırsa mevcut atamalar korunur)<textarea [(ngModel)]="studentIdsText" name="studentIds" rows="2" class="mt-1 w-full rounded border px-3 py-2 font-mono text-xs dark:border-gray-600 dark:bg-gray-900"></textarea></label>
          <div class="flex justify-end gap-3"><a [routerLink]="['/dashboard/coaching/assignments', id]" class="rounded border px-4 py-2 text-sm">Vazgeç</a><button type="submit" [disabled]="saving()" class="rounded bg-indigo-600 px-5 py-2 text-sm font-medium text-white disabled:opacity-50">{{ saving() ? 'Kaydediliyor…' : 'Değişiklikleri kaydet' }}</button></div>
        </form>
      }
    </section>
  `
})
export class CoachingAssignmentEditComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly service = inject(CoachingAdminService);
  private readonly router = inject(Router);
  private readonly platformId = inject(PLATFORM_ID);
  readonly id = this.route.snapshot.paramMap.get('id') ?? '';
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  studentIdsText = '';
  form = {
    title: '', description: '', subject: '', assignmentSource: 'Digital', targetGradeLevel: undefined as number | undefined,
    bookTitle: '', bookIsbn: '', bookEdition: '', bookChapter: '', bookStartPage: undefined as number | undefined,
    bookEndPage: undefined as number | undefined, bookStartQuestion: undefined as number | undefined, bookEndQuestion: undefined as number | undefined,
    dueDate: '', estimatedDurationMinutes: undefined as number | undefined, maxScore: undefined as number | undefined, passingScore: undefined as number | undefined
  };

  ngOnInit() { if (isPlatformBrowser(this.platformId) && this.id) this.load(); }

  private load() {
    this.loading.set(true);
    this.service.getAssignment(this.id).pipe(finalize(() => this.loading.set(false))).subscribe({
      next: value => this.fill(value),
      error: () => this.error.set('Ödev detayı yüklenemedi.')
    });
  }

  private fill(value: CoachingAdminAssignmentDetail) {
    this.form = {
      title: value.title, description: value.description ?? '', subject: value.subject ?? '', assignmentSource: value.source,
      targetGradeLevel: value.targetGradeLevel, bookTitle: value.bookTitle ?? '', bookIsbn: value.bookIsbn ?? '', bookEdition: value.bookEdition ?? '', bookChapter: value.bookChapter ?? '',
      bookStartPage: value.bookStartPage, bookEndPage: value.bookEndPage, bookStartQuestion: value.bookStartQuestion, bookEndQuestion: value.bookEndQuestion,
      dueDate: this.toLocalDateTime(value.dueDate), estimatedDurationMinutes: value.estimatedDurationMinutes, maxScore: value.maxScore, passingScore: value.passingScore
    };
    this.studentIdsText = value.assignedStudents.map(student => student.studentId).join(', ');
  }

  save() {
    const dueDate = new Date(this.form.dueDate);
    if (!this.form.title.trim() || Number.isNaN(dueDate.getTime())) { this.error.set('Başlık ve geçerli bir son tarih zorunludur.'); return; }
    const request: CoachingAdminAssignmentUpdateRequest = {
      assignmentId: this.id, title: this.form.title.trim(), description: this.form.description.trim() || null, subject: this.form.subject.trim() || null,
      assignmentSource: this.form.assignmentSource, targetGradeLevel: this.form.targetGradeLevel ?? null,
      bookTitle: this.form.assignmentSource === 'Digital' ? null : this.form.bookTitle.trim() || null,
      bookIsbn: this.form.assignmentSource === 'Digital' ? null : this.form.bookIsbn.trim() || null,
      bookEdition: this.form.assignmentSource === 'Digital' ? null : this.form.bookEdition.trim() || null,
      bookChapter: this.form.assignmentSource === 'Digital' ? null : this.form.bookChapter.trim() || null,
      bookStartPage: this.form.assignmentSource === 'Digital' ? null : this.form.bookStartPage ?? null,
      bookEndPage: this.form.assignmentSource === 'Digital' ? null : this.form.bookEndPage ?? null,
      bookStartQuestion: this.form.assignmentSource === 'Digital' ? null : this.form.bookStartQuestion ?? null,
      bookEndQuestion: this.form.assignmentSource === 'Digital' ? null : this.form.bookEndQuestion ?? null,
      dueDate: dueDate.toISOString(), estimatedDurationMinutes: this.form.estimatedDurationMinutes ?? null,
      maxScore: this.form.maxScore ?? null, passingScore: this.form.passingScore ?? null,
      studentIds: this.studentIdsText.trim() ? this.studentIdsText.split(',').map(id => id.trim()).filter(Boolean) : null
    };
    this.saving.set(true); this.error.set(null);
    this.service.updateAssignment(this.id, request).pipe(finalize(() => this.saving.set(false))).subscribe({
      next: () => this.router.navigate(['/dashboard/coaching/assignments', this.id]),
      error: () => this.error.set('Ödev güncellenemedi; alanları ve teslim edilmiş atamaları kontrol edin.')
    });
  }

  private toLocalDateTime(value: string) { const date = new Date(value); date.setMinutes(date.getMinutes() - date.getTimezoneOffset()); return date.toISOString().slice(0, 16); }
}
