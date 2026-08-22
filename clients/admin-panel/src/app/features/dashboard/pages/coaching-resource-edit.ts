import { CommonModule, isPlatformBrowser } from '@angular/common';
import { Component, inject, OnInit, PLATFORM_ID, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { finalize, Observable } from 'rxjs';
import {
  CoachingAdminExamDetail,
  CoachingAdminGoalDetail,
  CoachingAdminService,
  CoachingAdminSessionDetail
} from '../../../core/services/coaching-admin.service';

type Resource = 'session' | 'exam' | 'goal';

@Component({
  selector: 'app-coaching-resource-edit',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  template: `
    <section class="mx-auto max-w-4xl space-y-6">
      <a routerLink="/dashboard/coaching/operations" class="text-sm font-medium text-indigo-600 hover:underline">← Operasyonlara dön</a>
      <h1 class="text-2xl font-bold text-gray-900 dark:text-white">{{ resource === 'session' ? 'Seansı düzenle' : resource === 'exam' ? 'Sınavı düzenle' : 'Hedefi düzenle' }}</h1>
      @if (loading()) { <div class="rounded-xl border bg-white p-8 text-center text-gray-500 dark:border-gray-700 dark:bg-gray-800">Yükleniyor…</div> }
      @if (error()) { <div class="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700">{{ error() }}</div> }
      @if (!loading() && resource === 'session') {
        <form (ngSubmit)="saveSession()" class="space-y-4 rounded-xl border bg-white p-6 shadow-sm dark:border-gray-700 dark:bg-gray-800"><div class="grid gap-4 md:grid-cols-2"><label class="text-sm">Başlık<input [(ngModel)]="session.title" name="title" required maxlength="200" class="mt-1 w-full rounded border px-3 py-2 dark:border-gray-600 dark:bg-gray-900" /></label><label class="text-sm">Tarih<input [(ngModel)]="session.scheduledDate" name="scheduledDate" type="datetime-local" required class="mt-1 w-full rounded border px-3 py-2 dark:border-gray-600 dark:bg-gray-900" /></label></div><label class="block text-sm">Açıklama<textarea [(ngModel)]="session.description" name="description" maxlength="2000" rows="3" class="mt-1 w-full rounded border px-3 py-2 dark:border-gray-600 dark:bg-gray-900"></textarea></label><div class="grid gap-4 md:grid-cols-2"><label class="text-sm">Süre (dk)<input [(ngModel)]="session.durationMinutes" name="duration" type="number" min="1" max="240" required class="mt-1 w-full rounded border px-3 py-2 dark:border-gray-600 dark:bg-gray-900" /></label><label class="text-sm">Görüşme bağlantısı<input [(ngModel)]="session.meetingLink" name="meetingLink" type="url" maxlength="500" placeholder="https://..." class="mt-1 w-full rounded border px-3 py-2 dark:border-gray-600 dark:bg-gray-900" /></label></div><label class="block text-sm">Koç notu<textarea [(ngModel)]="session.teacherNotes" name="teacherNotes" maxlength="2000" rows="3" class="mt-1 w-full rounded border px-3 py-2 dark:border-gray-600 dark:bg-gray-900"></textarea></label><div class="flex justify-end"><button type="submit" [disabled]="saving()" class="rounded bg-indigo-600 px-5 py-2 text-sm font-medium text-white">Kaydet</button></div></form>
      } @else if (!loading() && resource === 'exam') {
        <form (ngSubmit)="saveExam()" class="space-y-4 rounded-xl border bg-white p-6 shadow-sm dark:border-gray-700 dark:bg-gray-800"><div class="grid gap-4 md:grid-cols-2"><label class="text-sm">Başlık<input [(ngModel)]="exam.title" name="title" required maxlength="200" class="mt-1 w-full rounded border px-3 py-2 dark:border-gray-600 dark:bg-gray-900" /></label><label class="text-sm">Sınav tipi<select [(ngModel)]="exam.type" name="type" class="mt-1 w-full rounded border px-3 py-2 dark:border-gray-600 dark:bg-gray-900"><option value="Mock">Deneme</option><option value="Weekly">Haftalık</option><option value="Monthly">Aylık</option><option value="LGS">LGS</option><option value="YKS">YKS</option><option value="MidTerm">Ara sınav</option><option value="Final">Final</option><option value="Quiz">Kısa sınav</option></select></label></div><div class="grid gap-4 md:grid-cols-2"><label class="text-sm">Tarih<input [(ngModel)]="exam.examDate" name="examDate" type="datetime-local" required class="mt-1 w-full rounded border px-3 py-2 dark:border-gray-600 dark:bg-gray-900" /></label><label class="text-sm">Ders<input [(ngModel)]="exam.subject" name="subject" maxlength="200" class="mt-1 w-full rounded border px-3 py-2 dark:border-gray-600 dark:bg-gray-900" /></label></div><div class="grid gap-4 md:grid-cols-3"><label class="text-sm">Maksimum puan<input [(ngModel)]="exam.maxScore" name="maxScore" type="number" min="0.01" max="999.99" step="0.01" required class="mt-1 w-full rounded border px-3 py-2 dark:border-gray-600 dark:bg-gray-900" /></label><label class="text-sm">Süre (dk)<input [(ngModel)]="exam.durationMinutes" name="duration" type="number" min="1" max="480" class="mt-1 w-full rounded border px-3 py-2 dark:border-gray-600 dark:bg-gray-900" /></label><label class="text-sm">Sınıf seviyesi<input [(ngModel)]="exam.targetGradeLevel" name="grade" type="number" min="1" max="12" class="mt-1 w-full rounded border px-3 py-2 dark:border-gray-600 dark:bg-gray-900" /></label></div><label class="block text-sm">Açıklama<textarea [(ngModel)]="exam.description" name="description" maxlength="2000" rows="3" class="mt-1 w-full rounded border px-3 py-2 dark:border-gray-600 dark:bg-gray-900"></textarea></label><div class="flex justify-end"><button type="submit" [disabled]="saving()" class="rounded bg-indigo-600 px-5 py-2 text-sm font-medium text-white">Kaydet</button></div></form>
      } @else if (!loading() && resource === 'goal') {
        <form (ngSubmit)="saveGoal()" class="space-y-4 rounded-xl border bg-white p-6 shadow-sm dark:border-gray-700 dark:bg-gray-800"><div class="grid gap-4 md:grid-cols-2"><label class="text-sm">Başlık<input [(ngModel)]="goal.title" name="title" required maxlength="200" class="mt-1 w-full rounded border px-3 py-2 dark:border-gray-600 dark:bg-gray-900" /></label><label class="text-sm">Kategori<select [(ngModel)]="goal.category" name="category" class="mt-1 w-full rounded border px-3 py-2 dark:border-gray-600 dark:bg-gray-900"><option value="ExamPreparation">Sınav hazırlığı</option><option value="SubjectMastery">Ders hâkimiyeti</option><option value="GradeImprovement">Not yükseltme</option><option value="StudyHabits">Çalışma alışkanlığı</option><option value="TimeManagement">Zaman yönetimi</option><option value="Other">Diğer</option></select></label></div><label class="block text-sm">Açıklama<textarea [(ngModel)]="goal.description" name="description" maxlength="2000" rows="3" class="mt-1 w-full rounded border px-3 py-2 dark:border-gray-600 dark:bg-gray-900"></textarea></label><div class="grid gap-4 md:grid-cols-2"><label class="text-sm">Hedef tarihi<input [(ngModel)]="goal.targetDate" name="targetDate" type="date" class="mt-1 w-full rounded border px-3 py-2 dark:border-gray-600 dark:bg-gray-900" /></label><label class="text-sm">Hedef puan<input [(ngModel)]="goal.targetScore" name="targetScore" type="number" min="0" max="999.99" step="0.01" class="mt-1 w-full rounded border px-3 py-2 dark:border-gray-600 dark:bg-gray-900" /></label></div><div class="grid gap-4 md:grid-cols-2"><label class="text-sm">Hedef sınavı<select [(ngModel)]="goal.targetExamType" name="targetExamType" class="mt-1 w-full rounded border px-3 py-2 dark:border-gray-600 dark:bg-gray-900"><option value="">Seçilmedi</option><option value="LGS">LGS</option><option value="YKS">YKS</option><option value="Mock">Deneme</option></select></label><label class="text-sm">Hedef dersi<input [(ngModel)]="goal.targetSubject" name="targetSubject" maxlength="200" class="mt-1 w-full rounded border px-3 py-2 dark:border-gray-600 dark:bg-gray-900" /></label></div><div class="flex justify-end"><button type="submit" [disabled]="saving()" class="rounded bg-indigo-600 px-5 py-2 text-sm font-medium text-white">Kaydet</button></div></form>
      }
    </section>
  `
})
export class CoachingResourceEditComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly service = inject(CoachingAdminService);
  private readonly router = inject(Router);
  private readonly platformId = inject(PLATFORM_ID);
  readonly resource = this.route.snapshot.data['resource'] as Resource;
  readonly id = this.route.snapshot.paramMap.get('id') ?? '';
  readonly loading = signal(false); readonly saving = signal(false); readonly error = signal<string | null>(null);
  session = { title: '', description: '', scheduledDate: '', durationMinutes: 60, meetingLink: '', teacherNotes: '' };
  exam = { title: '', type: 'Mock', subject: '', description: '', examDate: '', durationMinutes: undefined as number | undefined, maxScore: 100, targetGradeLevel: undefined as number | undefined };
  goal = { title: '', description: '', category: 'ExamPreparation', targetDate: '', targetScore: undefined as number | undefined, targetExamType: '', targetSubject: '' };

  ngOnInit() { if (isPlatformBrowser(this.platformId) && this.id) this.load(); }

  private load() {
    this.loading.set(true); this.error.set(null);
    const request: Observable<unknown> = this.resource === 'session'
      ? this.service.getSession(this.id)
      : this.resource === 'exam'
        ? this.service.getExam(this.id)
        : this.service.getGoal(this.id);
    request.pipe(finalize(() => this.loading.set(false))).subscribe({
      next: (value: unknown) => {
        if (this.resource === 'session') { const item = value as CoachingAdminSessionDetail; this.session = { title: item.title, description: item.description ?? '', scheduledDate: this.toLocalDateTime(item.scheduledDate), durationMinutes: item.durationMinutes, meetingLink: item.meetingLink ?? '', teacherNotes: item.teacherNotes ?? '' }; }
        else if (this.resource === 'exam') { const item = value as CoachingAdminExamDetail; this.exam = { title: item.title, type: item.examType, subject: item.subject ?? '', description: item.description ?? '', examDate: this.toLocalDateTime(item.examDate), durationMinutes: item.durationMinutes, maxScore: item.maxScore, targetGradeLevel: item.targetGradeLevel }; }
        else { const item = value as CoachingAdminGoalDetail; this.goal = { title: item.title, description: item.description ?? '', category: item.category, targetDate: item.targetDate ? new Date(item.targetDate).toISOString().slice(0, 10) : '', targetScore: item.targetScore, targetExamType: item.targetExamType ?? '', targetSubject: item.targetSubject ?? '' }; }
      },
      error: () => this.error.set('Kayıt detayı yüklenemedi.')
    });
  }

  saveSession() { const date = new Date(this.session.scheduledDate); if (!this.session.title.trim() || Number.isNaN(date.getTime())) { this.error.set('Başlık ve geçerli bir tarih zorunludur.'); return; } this.run(this.service.updateSession(this.id, { sessionId: this.id, title: this.session.title.trim(), description: this.session.description.trim() || null, scheduledDate: date.toISOString(), durationMinutes: Number(this.session.durationMinutes), meetingLink: this.session.meetingLink.trim() || null, teacherNotes: this.session.teacherNotes.trim() || null })); }
  saveExam() { const date = new Date(this.exam.examDate); if (!this.exam.title.trim() || Number.isNaN(date.getTime())) { this.error.set('Başlık ve geçerli bir tarih zorunludur.'); return; } this.run(this.service.updateExam(this.id, { examId: this.id, title: this.exam.title.trim(), type: this.exam.type, subject: this.exam.subject.trim() || null, description: this.exam.description.trim() || null, examDate: date.toISOString(), durationMinutes: this.exam.durationMinutes ?? null, maxScore: Number(this.exam.maxScore), targetGradeLevel: this.exam.targetGradeLevel ?? null })); }
  saveGoal() { if (!this.goal.title.trim()) { this.error.set('Hedef başlığı zorunludur.'); return; } this.run(this.service.updateGoal(this.id, { goalId: this.id, title: this.goal.title.trim(), description: this.goal.description.trim() || null, category: this.goal.category, targetDate: this.goal.targetDate ? new Date(`${this.goal.targetDate}T23:59:59`).toISOString() : null, targetScore: this.goal.targetScore ?? null, targetExamType: this.goal.targetExamType || null, targetSubject: this.goal.targetSubject.trim() || null })); }
  private run(request: Observable<unknown>) { this.saving.set(true); this.error.set(null); request.pipe(finalize(() => this.saving.set(false))).subscribe({ next: () => this.router.navigate(['/dashboard/coaching/operations']), error: () => this.error.set('Değişiklikler kaydedilemedi; alanları kontrol edin.') }); }
  private toLocalDateTime(value: string) { const date = new Date(value); date.setMinutes(date.getMinutes() - date.getTimezoneOffset()); return date.toISOString().slice(0, 16); }
}
