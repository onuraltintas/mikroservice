import { CommonModule, isPlatformBrowser } from '@angular/common';
import { Component, inject, OnInit, PLATFORM_ID, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { finalize, firstValueFrom } from 'rxjs';
import {
  CoachingAdminAssignmentCreateRequest,
  CoachingAdminService
} from '../../../core/services/coaching-admin.service';
import { IdentityService, UserDto } from '../../../core/services/identity.service';

@Component({
  selector: 'app-coaching-assignment-create',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  template: `
    <section class="mx-auto max-w-5xl space-y-6">
      <div class="flex items-center justify-between gap-3">
        <div>
          <a routerLink="/dashboard/coaching/assignments" class="text-sm font-medium text-indigo-600 hover:underline">← Ödevlere dön</a>
          <h1 class="mt-2 text-2xl font-bold text-gray-900 dark:text-white">Yeni koçluk ödevi</h1>
          <p class="text-sm text-gray-500 dark:text-gray-400">Admin adına kitap, dijital veya karma ödev oluşturun.</p>
        </div>
      </div>

      @if (error()) { <div class="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700">{{ error() }}</div> }
      @if (loadingUsers()) { <div class="rounded-lg border border-blue-200 bg-blue-50 p-4 text-sm text-blue-700">Öğretmen ve öğrenci listeleri yükleniyor…</div> }
      <div class="flex flex-wrap items-end gap-3 rounded-lg border border-gray-200 bg-gray-50 p-3 dark:border-gray-700 dark:bg-gray-900/40">
        <label class="min-w-56 text-sm text-gray-700 dark:text-gray-200">Öğretmen ara
          <input [(ngModel)]="teacherSearch" name="teacherSearch" maxlength="100" placeholder="Ad veya e-posta" class="mt-1 w-full rounded border px-3 py-2 dark:border-gray-600 dark:bg-gray-900" />
        </label>
        <label class="min-w-56 text-sm text-gray-700 dark:text-gray-200">Öğrenci ara
          <input [(ngModel)]="studentSearch" name="studentSearch" maxlength="100" placeholder="Ad veya e-posta" class="mt-1 w-full rounded border px-3 py-2 dark:border-gray-600 dark:bg-gray-900" />
        </label>
        <button type="button" (click)="searchUsers()" [disabled]="loadingUsers()" class="rounded-lg border border-indigo-300 px-4 py-2 text-sm text-indigo-700 disabled:opacity-50">Ara</button>
      </div>

      <form (ngSubmit)="submit()" class="space-y-6 rounded-xl border border-gray-200 bg-white p-6 shadow-sm dark:border-gray-700 dark:bg-gray-800">
        <div class="grid gap-4 md:grid-cols-2">
          <label class="text-sm text-gray-700 dark:text-gray-200">Öğretmen
            <select [(ngModel)]="form.teacherId" name="teacherId" required class="mt-1 w-full rounded-lg border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-900">
              <option value="">Öğretmen seçin</option>
              @for (teacher of teachers(); track teacher.userId) { <option [value]="teacher.userId">{{ teacher.fullName }} · {{ teacher.email }}</option> }
            </select>
          </label>
          <label class="text-sm text-gray-700 dark:text-gray-200">Son tarih
            <input [(ngModel)]="form.dueDate" name="dueDate" type="datetime-local" required class="mt-1 w-full rounded-lg border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-900" />
          </label>
        </div>

        <div class="grid gap-4 md:grid-cols-2">
          <label class="text-sm text-gray-700 dark:text-gray-200">Başlık
            <input [(ngModel)]="form.title" name="title" maxlength="200" required class="mt-1 w-full rounded-lg border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-900" />
          </label>
          <label class="text-sm text-gray-700 dark:text-gray-200">Ders
            <input [(ngModel)]="form.subject" name="subject" maxlength="120" class="mt-1 w-full rounded-lg border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-900" />
          </label>
        </div>

        <div class="grid gap-4 md:grid-cols-3">
          <label class="text-sm text-gray-700 dark:text-gray-200">Tür
            <select [(ngModel)]="form.assignmentType" name="assignmentType" class="mt-1 w-full rounded-lg border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-900"><option value="Individual">Bireysel</option><option value="Group">Grup</option></select>
          </label>
          <label class="text-sm text-gray-700 dark:text-gray-200">Kaynak
            <select [(ngModel)]="form.assignmentSource" name="assignmentSource" class="mt-1 w-full rounded-lg border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-900"><option value="Digital">Dijital</option><option value="Book">Kitap</option><option value="Mixed">Karma</option></select>
          </label>
          <label class="text-sm text-gray-700 dark:text-gray-200">Maksimum puan
            <input [(ngModel)]="form.maxScore" name="maxScore" type="number" min="1" class="mt-1 w-full rounded-lg border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-900" />
          </label>
        </div>

        <label class="block text-sm text-gray-700 dark:text-gray-200">Açıklama
          <textarea [(ngModel)]="form.description" name="description" maxlength="2000" rows="3" class="mt-1 w-full rounded-lg border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-900"></textarea>
        </label>

        @if (form.assignmentSource === 'Book' || form.assignmentSource === 'Mixed') {
          <div class="grid gap-4 rounded-lg border border-indigo-100 bg-indigo-50 p-4 md:grid-cols-2 dark:border-indigo-900 dark:bg-indigo-950/30">
            <label class="text-sm text-gray-700 dark:text-gray-200">Kitap adı
              <input [(ngModel)]="form.bookTitle" name="bookTitle" required maxlength="200" class="mt-1 w-full rounded-lg border border-gray-300 px-3 py-2 dark:border-gray-900 dark:bg-gray-900" />
            </label>
            <label class="text-sm text-gray-700 dark:text-gray-200">Bölüm
              <input [(ngModel)]="form.bookChapter" name="bookChapter" maxlength="200" class="mt-1 w-full rounded-lg border border-gray-300 px-3 py-2 dark:border-gray-900 dark:bg-gray-900" />
            </label>
            <label class="text-sm text-gray-700 dark:text-gray-200">Başlangıç sayfası
              <input [(ngModel)]="form.bookStartPage" name="bookStartPage" type="number" min="1" required class="mt-1 w-full rounded-lg border border-gray-300 px-3 py-2 dark:border-gray-900 dark:bg-gray-900" />
            </label>
            <label class="text-sm text-gray-700 dark:text-gray-200">Bitiş sayfası
              <input [(ngModel)]="form.bookEndPage" name="bookEndPage" type="number" min="1" required class="mt-1 w-full rounded-lg border border-gray-300 px-3 py-2 dark:border-gray-900 dark:bg-gray-900" />
            </label>
            <label class="text-sm text-gray-700 dark:text-gray-200">Başlangıç sorusu
              <input [(ngModel)]="form.bookStartQuestion" name="bookStartQuestion" type="number" min="1" class="mt-1 w-full rounded-lg border border-gray-300 px-3 py-2 dark:border-gray-900 dark:bg-gray-900" />
            </label>
            <label class="text-sm text-gray-700 dark:text-gray-200">Bitiş sorusu
              <input [(ngModel)]="form.bookEndQuestion" name="bookEndQuestion" type="number" min="1" class="mt-1 w-full rounded-lg border border-gray-300 px-3 py-2 dark:border-gray-900 dark:bg-gray-900" />
            </label>
          </div>
        }

        <fieldset>
          <legend class="mb-2 text-sm font-medium text-gray-700 dark:text-gray-200">Öğrenciler</legend>
          <div class="grid max-h-72 gap-2 overflow-y-auto rounded-lg border border-gray-200 p-3 md:grid-cols-2 dark:border-gray-700">
            @for (student of students(); track student.userId) {
              <label class="flex items-center gap-2 rounded px-2 py-1 text-sm hover:bg-gray-50 dark:hover:bg-gray-700">
                <input type="checkbox" [checked]="isSelected(student.userId)" (change)="toggleStudent(student.userId)" />
                <span>{{ student.fullName }} · {{ student.email }}</span>
              </label>
            } @empty { <p class="text-sm text-gray-500">Aktif öğrenci bulunamadı.</p> }
          </div>
          <p class="mt-1 text-xs text-gray-500">Seçili öğrenci: {{ selectedStudentIds.size }}</p>
        </fieldset>

        <div class="flex justify-end gap-3">
          <a routerLink="/dashboard/coaching/assignments" class="rounded-lg border border-gray-300 px-4 py-2 text-sm dark:border-gray-600">Vazgeç</a>
          <button type="submit" [disabled]="submitting() || selectedStudentIds.size === 0" class="rounded-lg bg-indigo-600 px-5 py-2 text-sm font-medium text-white disabled:opacity-50">{{ submitting() ? 'Kaydediliyor…' : 'Ödevi oluştur' }}</button>
        </div>
      </form>
    </section>
  `
})
export class CoachingAssignmentCreateComponent implements OnInit {
  private readonly coaching = inject(CoachingAdminService);
  private readonly identity = inject(IdentityService);
  private readonly router = inject(Router);
  private readonly platformId = inject(PLATFORM_ID);

  readonly teachers = signal<UserDto[]>([]);
  readonly students = signal<UserDto[]>([]);
  readonly loadingUsers = signal(false);
  readonly submitting = signal(false);
  readonly error = signal<string | null>(null);
  readonly selectedStudentIds = new Set<string>();
  teacherSearch = '';
  studentSearch = '';

  form: CoachingAdminAssignmentCreateRequest = {
    teacherId: '',
    title: '',
    description: '',
    subject: '',
    assignmentType: 'Individual',
    assignmentSource: 'Digital',
    dueDate: this.defaultDueDate(),
    studentIds: []
  };

  ngOnInit() {
    if (!isPlatformBrowser(this.platformId)) return;
    this.searchUsers();
  }

  searchUsers() {
    this.loadingUsers.set(true);
    Promise.all([
      firstValueFrom(this.identity.getAllUsers(1, 100, this.teacherSearch.trim(), 'Teacher', true)),
      firstValueFrom(this.identity.getAllUsers(1, 100, this.studentSearch.trim(), 'Student', true))
    ]).then(([teachers, students]) => {
      this.teachers.set(teachers?.items ?? []);
      this.students.set(students?.items ?? []);
    }).catch(() => this.error.set('Öğretmen ve öğrenci listeleri yüklenemedi.'))
      .finally(() => this.loadingUsers.set(false));
  }

  isSelected(id: string) { return this.selectedStudentIds.has(id); }

  toggleStudent(id: string) {
    if (this.selectedStudentIds.has(id)) this.selectedStudentIds.delete(id);
    else if (this.selectedStudentIds.size < 100) this.selectedStudentIds.add(id);
  }

  submit() {
    if (!this.form.teacherId || !this.form.title.trim() || this.selectedStudentIds.size === 0) {
      this.error.set('Öğretmen, başlık ve en az bir öğrenci seçilmelidir.');
      return;
    }
    const dueDate = new Date(this.form.dueDate);
    if (Number.isNaN(dueDate.getTime()) || dueDate <= new Date()) {
      this.error.set('Son tarih gelecekte olmalıdır.');
      return;
    }

    this.submitting.set(true);
    this.error.set(null);
    const request = { ...this.form, dueDate: dueDate.toISOString(), studentIds: [...this.selectedStudentIds] };
    const key = globalThis.crypto?.randomUUID?.() ?? `admin-${Date.now()}-${Math.random().toString(36).slice(2)}`;
    this.coaching.createAssignment(request, key)
      .pipe(finalize(() => this.submitting.set(false)))
      .subscribe({
        next: response => this.router.navigate(['/dashboard/coaching/assignments', response.assignmentId]),
        error: () => this.error.set('Ödev oluşturulamadı; alanları ve yetkinizi kontrol edin.')
      });
  }

  private defaultDueDate() {
    const date = new Date(Date.now() + 24 * 60 * 60 * 1000);
    date.setMinutes(date.getMinutes() - date.getTimezoneOffset());
    return date.toISOString().slice(0, 16);
  }
}
