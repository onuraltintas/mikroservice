import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ToasterService } from '../../../core/services/toaster.service';
import { CoachingStudentTeacher, CoachingStudentTeachersService } from '../../../core/services/coaching-student-teachers.service';
import { CoachingDatePipe } from '../coaching-labels';
import { apiMessage } from '../parent/parent-home.component';

/** Roadmap O12: the student's teachers; an independent teacher can be left, institution teachers are changed by the institution. */
@Component({
  selector: 'app-student-teachers',
  standalone: true,
  imports: [CommonModule, CoachingDatePipe],
  template: `
    <section class="rounded-2xl border border-slate-200 bg-white p-5 dark:border-slate-800 dark:bg-slate-900" aria-labelledby="my-teachers-heading">
      <h2 id="my-teachers-heading" class="font-semibold">Öğretmenlerim</h2>
      @if (error()) { <p role="alert" class="mt-3 text-sm text-red-700 dark:text-red-300">{{ error() }}</p> }
      @if (message()) { <p role="status" class="mt-3 text-sm text-emerald-700 dark:text-emerald-300">{{ message() }}</p> }
      @if (loading()) { <p role="status" class="mt-3 text-sm">Öğretmenlerin yükleniyor…</p> }
      @for (teacher of teachers(); track teacher.teacherUserId + (teacher.institutionId ?? '')) {
        <div class="mt-3 flex flex-wrap items-center justify-between gap-2 border-t border-slate-200 pt-3 text-sm dark:border-slate-800">
          <span><strong>{{ teacher.teacherName }}</strong> · {{ teacher.institutionId ? 'Kurum öğretmeni' : 'Bağımsız öğretmen' }} · {{ teacher.since | coachingDate }} tarihinden beri</span>
          @if (teacher.canLeave) {
            <button type="button" class="rounded-lg border border-slate-300 px-3 py-1 disabled:opacity-50 dark:border-slate-700" [attr.data-leave-teacher]="teacher.teacherUserId" [disabled]="busy()" (click)="leave(teacher)">Öğretmenden ayrıl</button>
          } @else { <span class="text-slate-500 dark:text-slate-400">Bu bağlantıyı kurum yönetimi değiştirir.</span> }
        </div>
      } @empty { @if (!loading() && !error()) { <p class="mt-3 text-sm text-slate-600 dark:text-slate-300">Şu anda bağlı olduğun bir öğretmen yok.</p> } }
    </section>
  `
})
export class StudentTeachersComponent implements OnInit {
  private readonly service = inject(CoachingStudentTeachersService);
  private readonly toaster = inject(ToasterService);
  readonly teachers = signal<CoachingStudentTeacher[]>([]);
  readonly loading = signal(false);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly message = signal<string | null>(null);

  ngOnInit(): void { void this.load(); }

  async load(): Promise<void> {
    this.loading.set(true); this.error.set(null);
    try { this.teachers.set(await firstValueFrom(this.service.list())); }
    catch { this.error.set('Öğretmenlerin yüklenemedi. Yeniden dene.'); }
    finally { this.loading.set(false); }
  }

  async leave(teacher: CoachingStudentTeacher): Promise<void> {
    if (!teacher.canLeave || this.busy()) return;
    if (!await this.toaster.confirm(`${teacher.teacherName} ile koçluk bağlantını sonlandırmak istiyor musun? Öğretmenin artık çalışmanı göremez; geçmiş kayıtların korunur.`)) return;
    this.busy.set(true); this.error.set(null); this.message.set(null);
    try {
      await firstValueFrom(this.service.leave(teacher.teacherUserId));
      this.message.set(`${teacher.teacherName} ile bağlantın sona erdi.`);
      await this.load();
    } catch (error) { this.error.set(apiMessage(error, 'Bağlantı sonlandırılamadı. Yeniden dene.')); }
    finally { this.busy.set(false); }
  }
}
