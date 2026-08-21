import { CommonModule, isPlatformBrowser } from '@angular/common';
import { Component, inject, OnInit, PLATFORM_ID, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { finalize, firstValueFrom } from 'rxjs';
import {
  CoachingAdminExamCreateRequest,
  CoachingAdminGoalCreateRequest,
  CoachingAdminService,
  CoachingAdminSessionCreateRequest
} from '../../../core/services/coaching-admin.service';
import { IdentityService, UserDto } from '../../../core/services/identity.service';
import { InstitutionDto, InstitutionService } from '../../../core/services/institution.service';

type Resource = 'session' | 'exam' | 'goal';

@Component({
  selector: 'app-coaching-resource-create',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  template: `
    <section class="mx-auto max-w-4xl space-y-6">
      <div>
        <a routerLink="/dashboard/coaching/operations" class="text-sm font-medium text-indigo-600 hover:underline">← Operasyonlara dön</a>
        <h1 class="mt-2 text-2xl font-bold text-gray-900 dark:text-white">{{ title() }}</h1>
        <p class="text-sm text-gray-500 dark:text-gray-400">Domain doğrulamalarına tabi, idempotent yönetici kaydı oluşturun.</p>
      </div>

      @if (error()) { <div class="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700">{{ error() }}</div> }
      @if (loadingUsers()) { <div class="rounded-lg border border-blue-200 bg-blue-50 p-4 text-sm text-blue-700">Kullanıcı listeleri yükleniyor…</div> }
      <div class="flex flex-wrap items-end gap-3 rounded-lg border border-gray-200 bg-gray-50 p-3 dark:border-gray-700 dark:bg-gray-900/40">
        <label class="min-w-56 text-sm text-gray-700 dark:text-gray-200">Öğretmen ara
          <input [(ngModel)]="teacherSearch" name="teacherSearch" maxlength="100" placeholder="Ad veya e-posta" class="mt-1 w-full rounded border px-3 py-2 dark:border-gray-600 dark:bg-gray-900" />
        </label>
        <label class="min-w-56 text-sm text-gray-700 dark:text-gray-200">Öğrenci ara
          <input [(ngModel)]="studentSearch" name="studentSearch" maxlength="100" placeholder="Ad veya e-posta" class="mt-1 w-full rounded border px-3 py-2 dark:border-gray-600 dark:bg-gray-900" />
        </label>
        <button type="button" (click)="searchUsers()" [disabled]="loadingUsers()" class="rounded-lg border border-indigo-300 px-4 py-2 text-sm text-indigo-700 disabled:opacity-50">Ara</button>
      </div>

      @if (resource() === 'session') {
        <form (ngSubmit)="submitSession()" class="space-y-5 rounded-xl border border-gray-200 bg-white p-6 shadow-sm dark:border-gray-700 dark:bg-gray-800">
          <div class="grid gap-4 md:grid-cols-2">
            <label class="text-sm text-gray-700 dark:text-gray-200">Öğretmen
              <select [(ngModel)]="session.teacherId" name="teacherId" required class="mt-1 w-full rounded-lg border px-3 py-2 dark:border-gray-600 dark:bg-gray-900"><option value="">Öğretmen seçin</option>@for (teacher of teachers(); track teacher.userId) {<option [value]="teacher.userId">{{ teacher.fullName }} · {{ teacher.email }}</option>}</select>
            </label>
            <label class="text-sm text-gray-700 dark:text-gray-200">Seans tipi
              <select [(ngModel)]="session.type" name="type" class="mt-1 w-full rounded-lg border px-3 py-2 dark:border-gray-600 dark:bg-gray-900"><option value="OneOnOne">Birebir</option><option value="Group">Grup</option></select>
            </label>
          </div>
          @if (session.type === 'OneOnOne') {
            <label class="block text-sm text-gray-700 dark:text-gray-200">Öğrenci
              <select [(ngModel)]="session.studentId" name="studentId" required class="mt-1 w-full rounded-lg border px-3 py-2 dark:border-gray-600 dark:bg-gray-900"><option value="">Öğrenci seçin</option>@for (student of students(); track student.userId) {<option [value]="student.userId">{{ student.fullName }} · {{ student.email }}</option>}</select>
            </label>
          } @else {
            <fieldset>
              <legend class="mb-2 text-sm font-medium text-gray-700 dark:text-gray-200">Grup öğrencileri (en az 2)</legend>
              <div class="grid max-h-56 gap-2 overflow-y-auto rounded-lg border p-3 md:grid-cols-2 dark:border-gray-700">@for (student of students(); track student.userId) {<label class="flex items-center gap-2 text-sm"><input type="checkbox" [checked]="selectedStudentIds.has(student.userId)" (change)="toggleStudent(student.userId)" /><span>{{ student.fullName }} · {{ student.email }}</span></label>} @empty {<span class="text-sm text-gray-500">Aktif öğrenci bulunamadı.</span>}</div>
              <p class="mt-1 text-xs text-gray-500">Seçili: {{ selectedStudentIds.size }}/100</p>
            </fieldset>
          }
          <div class="grid gap-4 md:grid-cols-2">
            <label class="text-sm text-gray-700 dark:text-gray-200">Başlangıç
              <input [(ngModel)]="session.startTime" name="startTime" type="datetime-local" required class="mt-1 w-full rounded-lg border px-3 py-2 dark:border-gray-600 dark:bg-gray-900" />
            </label>
            <label class="text-sm text-gray-700 dark:text-gray-200">Süre (dakika)
              <input [(ngModel)]="session.durationMinutes" name="durationMinutes" type="number" min="1" max="240" required class="mt-1 w-full rounded-lg border px-3 py-2 dark:border-gray-600 dark:bg-gray-900" />
            </label>
          </div>
          <label class="block text-sm text-gray-700 dark:text-gray-200">Konu
            <input [(ngModel)]="session.subject" name="subject" maxlength="200" class="mt-1 w-full rounded-lg border px-3 py-2 dark:border-gray-600 dark:bg-gray-900" />
          </label>
          <label class="block text-sm text-gray-700 dark:text-gray-200">Notlar
            <textarea [(ngModel)]="session.notes" name="notes" maxlength="2000" rows="4" class="mt-1 w-full rounded-lg border px-3 py-2 dark:border-gray-600 dark:bg-gray-900"></textarea>
          </label>
          <div class="flex justify-end gap-3"><a routerLink="/dashboard/coaching/operations" class="rounded-lg border px-4 py-2 text-sm dark:border-gray-600">Vazgeç</a><button type="submit" [disabled]="submitting()" class="rounded-lg bg-indigo-600 px-5 py-2 text-sm font-medium text-white disabled:opacity-50">{{ submitting() ? 'Kaydediliyor…' : 'Seans oluştur' }}</button></div>
        </form>
      } @else if (resource() === 'exam') {
        <form (ngSubmit)="submitExam()" class="space-y-5 rounded-xl border border-gray-200 bg-white p-6 shadow-sm dark:border-gray-700 dark:bg-gray-800">
          <div class="grid gap-4 md:grid-cols-2">
            <label class="text-sm text-gray-700 dark:text-gray-200">Öğretmen
              <select [(ngModel)]="exam.teacherId" name="teacherId" required class="mt-1 w-full rounded-lg border px-3 py-2 dark:border-gray-600 dark:bg-gray-900"><option value="">Öğretmen seçin</option>@for (teacher of teachers(); track teacher.userId) {<option [value]="teacher.userId">{{ teacher.fullName }} · {{ teacher.email }}</option>}</select>
            </label>
            <label class="text-sm text-gray-700 dark:text-gray-200">Sınav tipi
              <select [(ngModel)]="exam.type" name="type" class="mt-1 w-full rounded-lg border px-3 py-2 dark:border-gray-600 dark:bg-gray-900"><option value="Mock">Deneme</option><option value="Weekly">Haftalık</option><option value="Monthly">Aylık</option><option value="LGS">LGS</option><option value="YKS">YKS</option><option value="MidTerm">Ara sınav</option><option value="Final">Final</option><option value="Quiz">Kısa sınav</option></select>
            </label>
          </div>
          <div class="grid gap-4 md:grid-cols-2">
            <label class="text-sm text-gray-700 dark:text-gray-200">Başlık
              <input [(ngModel)]="exam.title" name="title" maxlength="200" required class="mt-1 w-full rounded-lg border px-3 py-2 dark:border-gray-600 dark:bg-gray-900" />
            </label>
            <label class="text-sm text-gray-700 dark:text-gray-200">Sınav tarihi
              <input [(ngModel)]="exam.examDate" name="examDate" type="datetime-local" required class="mt-1 w-full rounded-lg border px-3 py-2 dark:border-gray-600 dark:bg-gray-900" />
            </label>
          </div>
          <div class="grid gap-4 md:grid-cols-2">
            <label class="text-sm text-gray-700 dark:text-gray-200">Maksimum puan
              <input [(ngModel)]="exam.maxScore" name="maxScore" type="number" min="0.01" max="999.99" step="0.01" required class="mt-1 w-full rounded-lg border px-3 py-2 dark:border-gray-600 dark:bg-gray-900" />
            </label>
            <label class="text-sm text-gray-700 dark:text-gray-200">Kurum (isteğe bağlı)
              <select [(ngModel)]="exam.institutionId" name="institutionId" class="mt-1 w-full rounded-lg border px-3 py-2 dark:border-gray-600 dark:bg-gray-900"><option value="">Öğretmen kurumundan türet</option>@for (institution of institutions(); track institution.id) {<option [value]="institution.id">{{ institution.name }}</option>}</select>
            </label>
          </div>
          <label class="block text-sm text-gray-700 dark:text-gray-200">Açıklama
            <textarea [(ngModel)]="exam.description" name="description" maxlength="2000" rows="4" class="mt-1 w-full rounded-lg border px-3 py-2 dark:border-gray-600 dark:bg-gray-900"></textarea>
          </label>
          <div class="flex justify-end gap-3"><a routerLink="/dashboard/coaching/operations" class="rounded-lg border px-4 py-2 text-sm dark:border-gray-600">Vazgeç</a><button type="submit" [disabled]="submitting()" class="rounded-lg bg-indigo-600 px-5 py-2 text-sm font-medium text-white disabled:opacity-50">{{ submitting() ? 'Kaydediliyor…' : 'Sınav oluştur' }}</button></div>
        </form>
      } @else {
        <form (ngSubmit)="submitGoal()" class="space-y-5 rounded-xl border border-gray-200 bg-white p-6 shadow-sm dark:border-gray-700 dark:bg-gray-800">
          <div class="grid gap-4 md:grid-cols-2">
            <label class="text-sm text-gray-700 dark:text-gray-200">Öğrenci
              <select [(ngModel)]="goal.studentId" name="studentId" required class="mt-1 w-full rounded-lg border px-3 py-2 dark:border-gray-600 dark:bg-gray-900"><option value="">Öğrenci seçin</option>@for (student of students(); track student.userId) {<option [value]="student.userId">{{ student.fullName }} · {{ student.email }}</option>}</select>
            </label>
            <label class="text-sm text-gray-700 dark:text-gray-200">Koç (isteğe bağlı)
              <select [(ngModel)]="goal.teacherId" name="teacherId" class="mt-1 w-full rounded-lg border px-3 py-2 dark:border-gray-600 dark:bg-gray-900"><option value="">Öğrencinin hedefi</option>@for (teacher of teachers(); track teacher.userId) {<option [value]="teacher.userId">{{ teacher.fullName }} · {{ teacher.email }}</option>}</select>
            </label>
          </div>
          <div class="grid gap-4 md:grid-cols-2">
            <label class="text-sm text-gray-700 dark:text-gray-200">Başlık
              <input [(ngModel)]="goal.title" name="title" maxlength="200" required class="mt-1 w-full rounded-lg border px-3 py-2 dark:border-gray-600 dark:bg-gray-900" />
            </label>
            <label class="text-sm text-gray-700 dark:text-gray-200">Kategori
              <select [(ngModel)]="goal.category" name="category" class="mt-1 w-full rounded-lg border px-3 py-2 dark:border-gray-600 dark:bg-gray-900"><option value="ExamPreparation">Sınav hazırlığı</option><option value="SubjectMastery">Ders hâkimiyeti</option><option value="GradeImprovement">Not yükseltme</option><option value="StudyHabits">Çalışma alışkanlığı</option><option value="TimeManagement">Zaman yönetimi</option><option value="Other">Diğer</option></select>
            </label>
          </div>
          <div class="grid gap-4 md:grid-cols-2">
            <label class="text-sm text-gray-700 dark:text-gray-200">Hedef tarihi (isteğe bağlı)
              <input [(ngModel)]="goal.targetDate" name="targetDate" type="date" class="mt-1 w-full rounded-lg border px-3 py-2 dark:border-gray-600 dark:bg-gray-900" />
            </label>
            <label class="text-sm text-gray-700 dark:text-gray-200">Hedef puan (isteğe bağlı)
              <input [(ngModel)]="goal.targetScore" name="targetScore" type="number" min="0" max="999.99" step="0.01" class="mt-1 w-full rounded-lg border px-3 py-2 dark:border-gray-600 dark:bg-gray-900" />
            </label>
          </div>
          <label class="block text-sm text-gray-700 dark:text-gray-200">Açıklama
            <textarea [(ngModel)]="goal.description" name="description" maxlength="2000" rows="4" class="mt-1 w-full rounded-lg border px-3 py-2 dark:border-gray-600 dark:bg-gray-900"></textarea>
          </label>
          <div class="flex justify-end gap-3"><a routerLink="/dashboard/coaching/operations" class="rounded-lg border px-4 py-2 text-sm dark:border-gray-600">Vazgeç</a><button type="submit" [disabled]="submitting()" class="rounded-lg bg-indigo-600 px-5 py-2 text-sm font-medium text-white disabled:opacity-50">{{ submitting() ? 'Kaydediliyor…' : 'Hedef oluştur' }}</button></div>
        </form>
      }
    </section>
  `
})
export class CoachingResourceCreateComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly coaching = inject(CoachingAdminService);
  private readonly identity = inject(IdentityService);
  private readonly institutionsService = inject(InstitutionService);
  private readonly router = inject(Router);
  private readonly platformId = inject(PLATFORM_ID);

  readonly resource = signal<Resource>('session');
  readonly title = signal('Yeni seans');
  readonly teachers = signal<UserDto[]>([]);
  readonly students = signal<UserDto[]>([]);
  readonly institutions = signal<InstitutionDto[]>([]);
  readonly loadingUsers = signal(false);
  readonly submitting = signal(false);
  readonly error = signal<string | null>(null);
  readonly selectedStudentIds = new Set<string>();
  teacherSearch = '';
  studentSearch = '';

  session: CoachingAdminSessionCreateRequest & { startTime: string } = {
    teacherId: '', studentId: '', startTime: this.defaultDateTime(), durationMinutes: 60,
    subject: '', notes: '', type: 'OneOnOne'
  };
  exam: CoachingAdminExamCreateRequest & { examDate: string } = {
    teacherId: '', title: '', type: 'Mock', examDate: this.defaultDateTime(), maxScore: 100,
    institutionId: '', description: ''
  };
  goal: CoachingAdminGoalCreateRequest = {
    studentId: '', teacherId: '', title: '', category: 'ExamPreparation', description: '', targetDate: '', targetScore: undefined
  };

  ngOnInit() {
    const requested = this.route.snapshot.data['resource'] as Resource | undefined;
    if (requested && ['session', 'exam', 'goal'].includes(requested)) {
      this.resource.set(requested);
      this.title.set(requested === 'session' ? 'Yeni koçluk seansı' : requested === 'exam' ? 'Yeni sınav' : 'Yeni akademik hedef');
    }
    if (isPlatformBrowser(this.platformId)) this.loadUsers();
  }

  private async loadUsers() {
    this.loadingUsers.set(true);
    const results = await Promise.allSettled([
      firstValueFrom(this.identity.getAllUsers(1, 100, '', 'Teacher', true)),
      firstValueFrom(this.identity.getAllUsers(1, 100, '', 'Student', true)),
      firstValueFrom(this.institutionsService.getAll(1, 100, '', true))
    ]);
    const [teachers, students, institutions] = results;
    if (teachers.status === 'fulfilled') this.teachers.set(teachers.value.items ?? []);
    if (students.status === 'fulfilled') this.students.set(students.value.items ?? []);
    if (institutions.status === 'fulfilled') this.institutions.set(institutions.value.items ?? []);
    if (results.every(result => result.status === 'rejected')) this.error.set('Kullanıcı listeleri yüklenemedi.');
    this.loadingUsers.set(false);
  }

  async searchUsers() {
    this.loadingUsers.set(true);
    const [teachers, students] = await Promise.allSettled([
      firstValueFrom(this.identity.getAllUsers(1, 100, this.teacherSearch.trim(), 'Teacher', true)),
      firstValueFrom(this.identity.getAllUsers(1, 100, this.studentSearch.trim(), 'Student', true))
    ]);
    if (teachers.status === 'fulfilled') this.teachers.set(teachers.value.items ?? []);
    if (students.status === 'fulfilled') this.students.set(students.value.items ?? []);
    if (teachers.status === 'rejected' && students.status === 'rejected') this.error.set('Kullanıcı araması başarısız oldu.');
    this.loadingUsers.set(false);
  }

  toggleStudent(id: string) {
    if (this.selectedStudentIds.has(id)) this.selectedStudentIds.delete(id);
    else if (this.selectedStudentIds.size < 100) this.selectedStudentIds.add(id);
  }

  submitSession() {
    const start = new Date(this.session.startTime);
    if (!this.session.teacherId || Number.isNaN(start.getTime()) || start <= new Date()) {
      this.error.set('Öğretmen ve gelecekte bir başlangıç zamanı seçilmelidir.');
      return;
    }
    if (this.session.type === 'OneOnOne' && !this.session.studentId) {
      this.error.set('Birebir seans için öğrenci seçilmelidir.');
      return;
    }
    if (this.session.type === 'Group' && this.selectedStudentIds.size < 2) {
      this.error.set('Grup seansı için en az iki öğrenci seçilmelidir.');
      return;
    }
    this.submitting.set(true); this.error.set(null);
    const request: CoachingAdminSessionCreateRequest = {
      ...this.session,
      startTime: start.toISOString(),
      studentIds: this.session.type === 'Group' ? [...this.selectedStudentIds] : undefined
    };
    this.coaching.createSession(request, this.idempotencyKey()).pipe(finalize(() => this.submitting.set(false))).subscribe({
      next: () => this.router.navigate(['/dashboard/coaching/operations']),
      error: () => this.error.set('Seans oluşturulamadı; alanları ve yetkinizi kontrol edin.')
    });
  }

  submitExam() {
    const date = new Date(this.exam.examDate);
    if (!this.exam.teacherId || !this.exam.title.trim() || Number.isNaN(date.getTime()) || date <= new Date()) {
      this.error.set('Öğretmen, başlık ve gelecekte bir sınav tarihi zorunludur.');
      return;
    }
    this.submitting.set(true); this.error.set(null);
    const request: CoachingAdminExamCreateRequest = { ...this.exam, examDate: date.toISOString(), institutionId: this.exam.institutionId || undefined };
    this.coaching.createExam(request, this.idempotencyKey()).pipe(finalize(() => this.submitting.set(false))).subscribe({
      next: () => this.router.navigate(['/dashboard/coaching/operations']),
      error: () => this.error.set('Sınav oluşturulamadı; alanları ve yetkinizi kontrol edin.')
    });
  }

  submitGoal() {
    if (!this.goal.studentId || !this.goal.title.trim()) {
      this.error.set('Öğrenci ve hedef başlığı zorunludur.');
      return;
    }
    this.submitting.set(true); this.error.set(null);
    const request: CoachingAdminGoalCreateRequest = {
      ...this.goal,
      teacherId: this.goal.teacherId || undefined,
      targetDate: this.goal.targetDate ? new Date(`${this.goal.targetDate}T23:59:59`).toISOString() : undefined
    };
    this.coaching.createGoal(request, this.idempotencyKey()).pipe(finalize(() => this.submitting.set(false))).subscribe({
      next: () => this.router.navigate(['/dashboard/coaching/operations']),
      error: () => this.error.set('Hedef oluşturulamadı; alanları ve yetkinizi kontrol edin.')
    });
  }

  private idempotencyKey() {
    return globalThis.crypto?.randomUUID?.() ?? `admin-${Date.now()}-${Math.random().toString(36).slice(2)}`;
  }

  private defaultDateTime() {
    const date = new Date(Date.now() + 60 * 60 * 1000);
    date.setMinutes(date.getMinutes() - date.getTimezoneOffset());
    return date.toISOString().slice(0, 16);
  }
}
