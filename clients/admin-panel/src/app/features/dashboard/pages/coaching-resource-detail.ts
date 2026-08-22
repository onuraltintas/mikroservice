import { CommonModule, isPlatformBrowser } from '@angular/common';
import { Component, inject, OnInit, PLATFORM_ID, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { finalize, firstValueFrom, Observable } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { ADMIN_PERMISSIONS } from '../../../core/auth/permissions';
import {
  CoachingAdminExamResult,
  CoachingAdminExamDetail,
  CoachingAdminService,
  CoachingAdminSessionDetail
} from '../../../core/services/coaching-admin.service';
import { IdentityService, UserDto } from '../../../core/services/identity.service';

type Resource = 'session' | 'exam';

@Component({
  selector: 'app-coaching-resource-detail',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  template: `
    <section class="mx-auto max-w-5xl space-y-6">
      <div>
        <a routerLink="/dashboard/coaching/operations" class="text-sm font-medium text-indigo-600 hover:underline">← Operasyonlara dön</a>
        @if (session(); as value) {
          <h1 class="mt-2 text-2xl font-bold text-gray-900 dark:text-white">{{ value.title }}</h1>
          <p class="text-sm text-gray-500 dark:text-gray-400">{{ value.scheduledDate | date:'dd.MM.yyyy HH:mm' }} · {{ value.status }}</p>
          @if (canManage()) { <a [routerLink]="['/dashboard/coaching/operations/session', value.id, 'edit']" class="mt-2 inline-flex text-sm font-medium text-indigo-600 hover:underline">Seansı düzenle →</a> }
          @if (value.meetingLink) { <a [href]="value.meetingLink" target="_blank" rel="noopener noreferrer" class="mt-2 inline-flex text-sm font-medium text-indigo-600 hover:underline">Online görüşme bağlantısını aç →</a> }
        } @else if (exam(); as value) {
          <h1 class="mt-2 text-2xl font-bold text-gray-900 dark:text-white">{{ value.title }}</h1>
          <p class="text-sm text-gray-500 dark:text-gray-400">{{ value.examDate | date:'dd.MM.yyyy HH:mm' }} · {{ value.examType }} · Maksimum {{ value.maxScore }}</p>
          @if (canManage()) { <a [routerLink]="['/dashboard/coaching/operations/exam', value.id, 'edit']" class="mt-2 inline-flex text-sm font-medium text-indigo-600 hover:underline">Sınavı düzenle →</a> }
        }
      </div>

      @if (error()) { <div class="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700">{{ error() }}</div> }
      @if (loading()) { <div class="rounded-xl border bg-white p-8 text-center text-gray-500 dark:border-gray-700 dark:bg-gray-800">Yükleniyor…</div> }
      <div class="flex flex-wrap items-end gap-3 rounded-lg border border-gray-200 bg-gray-50 p-3 dark:border-gray-700 dark:bg-gray-900/40">
        <label class="min-w-56 text-sm text-gray-700 dark:text-gray-200">Öğrenci ara
          <input [(ngModel)]="studentSearch" name="studentSearch" maxlength="100" placeholder="Ad veya e-posta" class="mt-1 w-full rounded border px-3 py-2 dark:border-gray-600 dark:bg-gray-900" />
        </label>
        <button type="button" (click)="searchStudents()" [disabled]="loading()" class="rounded-lg border border-indigo-300 px-4 py-2 text-sm text-indigo-700 disabled:opacity-50">Ara</button>
      </div>

      @if (session(); as value) {
        <div class="rounded-xl border border-gray-200 bg-white p-5 shadow-sm dark:border-gray-700 dark:bg-gray-800">
          <h2 class="mb-4 font-semibold text-gray-900 dark:text-white">Katılım kayıtları</h2>
          <div class="overflow-x-auto"><table class="min-w-full divide-y divide-gray-200 text-sm dark:divide-gray-700"><thead class="text-left text-xs uppercase text-gray-500"><tr><th class="px-3 py-2">Öğrenci</th><th class="px-3 py-2">Mevcut durum</th><th class="px-3 py-2">Not</th><th class="px-3 py-2"></th></tr></thead><tbody class="divide-y divide-gray-200 dark:divide-gray-700">@for (attendance of value.attendances; track attendance.studentId) {<tr><td class="px-3 py-3">{{ userName(attendance.studentId) }}</td><td class="px-3 py-3">{{ attendance.status }}</td><td class="px-3 py-3"><input [(ngModel)]="attendanceDraft[attendance.studentId].notes" [name]="'note-' + attendance.studentId" maxlength="1000" class="w-full rounded border px-2 py-1 dark:border-gray-600 dark:bg-gray-900" /></td><td class="px-3 py-3">@if (canManage()) {<div class="flex items-center gap-2"><select [(ngModel)]="attendanceDraft[attendance.studentId].attended" [name]="'attended-' + attendance.studentId" class="rounded border px-2 py-1 dark:border-gray-600 dark:bg-gray-900"><option [ngValue]="true">Katıldı</option><option [ngValue]="false">Katılmadı</option></select><button type="button" (click)="saveAttendance(attendance.studentId)" [disabled]="actionLoading()" class="text-xs text-indigo-700 hover:underline">Kaydet</button></div>}</td></tr>} @empty {<tr><td colspan="4" class="px-3 py-8 text-center text-gray-500">Katılım kaydı bulunamadı.</td></tr>}</tbody></table></div>
        </div>
      } @else if (exam(); as value) {
        <div class="rounded-xl border border-gray-200 bg-white p-5 shadow-sm dark:border-gray-700 dark:bg-gray-800">
          <div class="mb-4 flex items-center justify-between gap-3"><h2 class="font-semibold text-gray-900 dark:text-white">Sınav sonuçları</h2><span class="text-sm text-gray-500">{{ value.results.length }} sonuç</span></div>
          <div class="overflow-x-auto"><table class="min-w-full divide-y divide-gray-200 text-sm dark:divide-gray-700"><thead class="text-left text-xs uppercase text-gray-500"><tr><th class="px-3 py-2">Öğrenci</th><th class="px-3 py-2">Puan</th><th class="px-3 py-2">Doğru / Yanlış / Boş</th><th class="px-3 py-2">Not</th>@if (canManage()) { <th class="px-3 py-2">İşlem</th> }</tr></thead><tbody class="divide-y divide-gray-200 dark:divide-gray-700">@for (result of value.results; track result.id) {<tr><td class="px-3 py-3">{{ userName(result.studentId) }}</td><td class="px-3 py-3">{{ result.score }}</td><td class="px-3 py-3">{{ result.correctAnswers ?? '—' }} / {{ result.wrongAnswers ?? '—' }} / {{ result.emptyAnswers ?? '—' }}</td><td class="px-3 py-3">{{ result.teacherNotes || '—' }}</td>@if (canManage()) { <td class="px-3 py-3"><button type="button" (click)="editResult(result)" class="text-xs text-indigo-700 hover:underline">Düzenle</button></td> }</tr>} @empty {<tr><td [attr.colspan]="canManage() ? 5 : 4" class="px-3 py-8 text-center text-gray-500">Sonuç bulunamadı.</td></tr>}</tbody></table></div>
          @if (editingResult(); as result) { <form (ngSubmit)="saveResult()" class="mt-4 space-y-3 rounded-lg border border-indigo-100 bg-indigo-50 p-4 dark:border-indigo-900 dark:bg-indigo-950/30"><p class="text-sm font-medium">{{ userName(result.studentId) }} sonucunu düzelt</p><div class="grid gap-3 md:grid-cols-4"><label class="text-sm">Puan<input [(ngModel)]="resultEditForm.score" name="editScore" type="number" min="0" [max]="value.maxScore" step="0.01" class="mt-1 w-full rounded border px-2 py-1 dark:border-gray-600 dark:bg-gray-900" /></label><label class="text-sm">Doğru<input [(ngModel)]="resultEditForm.correctAnswers" name="editCorrect" type="number" min="0" class="mt-1 w-full rounded border px-2 py-1 dark:border-gray-600 dark:bg-gray-900" /></label><label class="text-sm">Yanlış<input [(ngModel)]="resultEditForm.wrongAnswers" name="editWrong" type="number" min="0" class="mt-1 w-full rounded border px-2 py-1 dark:border-gray-600 dark:bg-gray-900" /></label><label class="text-sm">Boş<input [(ngModel)]="resultEditForm.emptyAnswers" name="editEmpty" type="number" min="0" class="mt-1 w-full rounded border px-2 py-1 dark:border-gray-600 dark:bg-gray-900" /></label></div><label class="block text-sm">Öğretmen notu<textarea [(ngModel)]="resultEditForm.notes" name="editNotes" maxlength="2000" rows="2" class="mt-1 w-full rounded border px-2 py-1 dark:border-gray-600 dark:bg-gray-900"></textarea></label><div class="flex justify-end gap-2"><button type="button" (click)="editingResult.set(null)" class="rounded border px-3 py-1 text-sm">Vazgeç</button><button type="submit" [disabled]="actionLoading()" class="rounded bg-indigo-600 px-3 py-1 text-sm text-white">Kaydet</button></div></form> }
        </div>
        @if (canManage()) {
          <form (ngSubmit)="addResult()" class="space-y-4 rounded-xl border border-gray-200 bg-white p-5 shadow-sm dark:border-gray-700 dark:bg-gray-800"><h2 class="font-semibold text-gray-900 dark:text-white">Sonuç ekle</h2><div class="grid gap-4 md:grid-cols-2"><label class="text-sm text-gray-700 dark:text-gray-200">Öğrenci<select [(ngModel)]="resultForm.studentId" name="studentId" required class="mt-1 w-full rounded border px-3 py-2 dark:border-gray-600 dark:bg-gray-900"><option value="">Öğrenci seçin</option>@for (student of students(); track student.userId) {<option [value]="student.userId">{{ student.fullName }} · {{ student.email }}</option>}</select></label><label class="text-sm text-gray-700 dark:text-gray-200">Puan<input [(ngModel)]="resultForm.score" name="score" type="number" min="0" [max]="value.maxScore" step="0.01" required class="mt-1 w-full rounded border px-3 py-2 dark:border-gray-600 dark:bg-gray-900" /></label></div><div class="grid gap-4 md:grid-cols-3"><label class="text-sm text-gray-700 dark:text-gray-200">Doğru<input [(ngModel)]="resultForm.correctAnswers" name="correctAnswers" type="number" min="0" class="mt-1 w-full rounded border px-3 py-2 dark:border-gray-600 dark:bg-gray-900" /></label><label class="text-sm text-gray-700 dark:text-gray-200">Yanlış<input [(ngModel)]="resultForm.wrongAnswers" name="wrongAnswers" type="number" min="0" class="mt-1 w-full rounded border px-3 py-2 dark:border-gray-600 dark:bg-gray-900" /></label><label class="text-sm text-gray-700 dark:text-gray-200">Boş<input [(ngModel)]="resultForm.emptyAnswers" name="emptyAnswers" type="number" min="0" class="mt-1 w-full rounded border px-3 py-2 dark:border-gray-600 dark:bg-gray-900" /></label></div><label class="block text-sm text-gray-700 dark:text-gray-200">Öğretmen notu<textarea [(ngModel)]="resultForm.notes" name="notes" maxlength="2000" rows="3" class="mt-1 w-full rounded border px-3 py-2 dark:border-gray-600 dark:bg-gray-900"></textarea></label><div class="flex justify-end"><button type="submit" [disabled]="actionLoading()" class="rounded-lg bg-indigo-600 px-5 py-2 text-sm font-medium text-white disabled:opacity-50">Sonucu kaydet</button></div></form>
        }
      }
    </section>
  `
})
export class CoachingResourceDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly coaching = inject(CoachingAdminService);
  private readonly identity = inject(IdentityService);
  private readonly auth = inject(AuthService);
  private readonly platformId = inject(PLATFORM_ID);

  readonly session = signal<CoachingAdminSessionDetail | null>(null);
  readonly exam = signal<CoachingAdminExamDetail | null>(null);
  readonly students = signal<UserDto[]>([]);
  readonly loading = signal(false);
  readonly actionLoading = signal(false);
  readonly error = signal<string | null>(null);
  readonly editingResult = signal<CoachingAdminExamResult | null>(null);
  readonly attendanceDraft: Record<string, { attended: boolean; notes: string }> = {};
  resultForm = { studentId: '', score: 0, correctAnswers: 0, wrongAnswers: 0, emptyAnswers: 0, notes: '' };
  resultEditForm = { score: 0, correctAnswers: 0, wrongAnswers: 0, emptyAnswers: 0, notes: '', subjectScores: undefined as Record<string, number> | undefined, ranking: undefined as number | undefined };
  private resource: Resource = 'session';
  private id = '';
  studentSearch = '';

  canManage() { return this.auth.hasPermission(ADMIN_PERMISSIONS.coachingManage); }

  ngOnInit() {
    this.resource = this.route.snapshot.data['resource'] as Resource;
    this.id = this.route.snapshot.paramMap.get('id') ?? '';
    if (!isPlatformBrowser(this.platformId) || !this.id || !['session', 'exam'].includes(this.resource)) return;
    this.load();
  }

  load() {
    this.loading.set(true); this.error.set(null);
    const detail: Observable<CoachingAdminSessionDetail | CoachingAdminExamDetail> = this.resource === 'session'
      ? this.coaching.getSession(this.id)
      : this.coaching.getExam(this.id);
    Promise.allSettled([
      firstValueFrom(detail),
      firstValueFrom(this.identity.getAllUsers(1, 100, '', 'Student', true))
    ]).then(([resource, students]) => {
      if (resource.status === 'rejected') { this.error.set('Kayıt ayrıntısı yüklenemedi.'); return; }
      if (students.status === 'fulfilled') this.students.set(students.value.items ?? []);
      if (this.resource === 'session') {
        const value = resource.value as CoachingAdminSessionDetail;
        this.session.set(value);
        value.attendances.forEach(attendance => {
          this.attendanceDraft[attendance.studentId] = { attended: attendance.status === 'Present', notes: attendance.teacherNote ?? '' };
        });
      } else {
        this.exam.set(resource.value as CoachingAdminExamDetail);
      }
    }).finally(() => this.loading.set(false));
  }

  userName(id: string) { return this.students().find(student => student.userId === id)?.fullName ?? id; }

  async searchStudents() {
    try {
      const result = await firstValueFrom(this.identity.getAllUsers(1, 100, this.studentSearch.trim(), 'Student', true));
      this.students.set(result.items ?? []);
    } catch {
      this.error.set('Öğrenci araması başarısız oldu.');
    }
  }

  saveAttendance(studentId: string) {
    const draft = this.attendanceDraft[studentId];
    if (!draft) return;
    this.actionLoading.set(true); this.error.set(null);
    this.coaching.updateSessionAttendance(this.id, { sessionId: this.id, studentId, attended: draft.attended, notes: draft.notes || undefined })
      .pipe(finalize(() => this.actionLoading.set(false)))
      .subscribe({ next: () => this.load(), error: () => this.error.set('Katılım kaydedilemedi.') });
  }

  addResult() {
    const exam = this.exam();
    if (!exam || !this.resultForm.studentId || this.resultForm.score < 0 || this.resultForm.score > exam.maxScore) {
      this.error.set('Öğrenci seçin ve puanı sınav aralığında girin.');
      return;
    }
    this.actionLoading.set(true); this.error.set(null);
    this.coaching.addExamResult(exam.id, {
      examId: exam.id,
      studentId: this.resultForm.studentId,
      score: this.resultForm.score,
      correctAnswers: this.resultForm.correctAnswers,
      wrongAnswers: this.resultForm.wrongAnswers,
      emptyAnswers: this.resultForm.emptyAnswers,
      notes: this.resultForm.notes || undefined
    }, this.idempotencyKey()).pipe(finalize(() => this.actionLoading.set(false))).subscribe({
      next: () => { this.resultForm = { studentId: '', score: 0, correctAnswers: 0, wrongAnswers: 0, emptyAnswers: 0, notes: '' }; this.load(); },
      error: () => this.error.set('Sınav sonucu kaydedilemedi; aynı öğrenci için daha önce sonuç olabilir.')
    });
  }

  editResult(result: CoachingAdminExamResult) {
    this.editingResult.set(result);
    this.resultEditForm = {
      score: result.score,
      correctAnswers: result.correctAnswers ?? 0,
      wrongAnswers: result.wrongAnswers ?? 0,
      emptyAnswers: result.emptyAnswers ?? 0,
      notes: result.teacherNotes ?? '',
      subjectScores: result.subjectScores,
      ranking: result.ranking
    };
  }

  saveResult() {
    const exam = this.exam();
    const result = this.editingResult();
    if (!exam || !result || this.resultEditForm.score < 0 || this.resultEditForm.score > exam.maxScore) {
      this.error.set('Puan sınavın maksimum puanı içinde olmalıdır.');
      return;
    }
    this.actionLoading.set(true); this.error.set(null);
    this.coaching.updateExamResult(exam.id, result.id, {
      examId: exam.id,
      resultId: result.id,
      score: this.resultEditForm.score,
      correctAnswers: this.resultEditForm.correctAnswers,
      wrongAnswers: this.resultEditForm.wrongAnswers,
      emptyAnswers: this.resultEditForm.emptyAnswers,
      subjectScores: this.resultEditForm.subjectScores ?? null,
      ranking: this.resultEditForm.ranking ?? null,
      notes: this.resultEditForm.notes.trim() || null
    }).pipe(finalize(() => this.actionLoading.set(false))).subscribe({
      next: () => { this.editingResult.set(null); this.load(); },
      error: () => this.error.set('Sınav sonucu güncellenemedi.')
    });
  }

  private idempotencyKey() {
    return globalThis.crypto?.randomUUID?.() ?? `admin-${Date.now()}-${Math.random().toString(36).slice(2)}`;
  }
}
