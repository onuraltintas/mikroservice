import { ChangeDetectionStrategy, Component, computed, effect, inject, model, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ToasterService } from '../../../core/services/toaster.service';

/** The form shape of an exam session (KM1): dates as yyyy-MM-dd, empty string for no application window. */
export interface ExamSessionDraft { id: string | null; date: string; label: string; applicationStart: string; applicationEnd: string }

/** KM7: the same rules the API applies (StudyCatalogExamSession), as Turkish messages per row; empty = valid. */
export function examSessionErrors(sessions: ExamSessionDraft[]): string[][] {
  return sessions.map((s, i) => {
    const errors: string[] = [];
    if (!s.label.trim()) errors.push('Oturum adını girin.');
    else if (s.label.trim().length > 100) errors.push('Oturum adı en fazla 100 karakter olabilir.');
    if (!s.date) errors.push('Sınav tarihini girin.');
    if (!!s.applicationStart !== !!s.applicationEnd) errors.push('Başvuru başlangıcını ve bitişini birlikte girin ya da ikisini de boş bırakın.');
    if (s.applicationStart && s.applicationEnd && s.applicationStart > s.applicationEnd) errors.push('Başvuru başlangıcı bitişten sonra olamaz.');
    if (s.applicationEnd && s.date && s.applicationEnd >= s.date) errors.push('Başvuru bitişi sınav tarihinden önce olmalıdır.');
    if (s.date && sessions.some((o, j) => j !== i && o.date === s.date)) errors.push('Aynı tarihte iki oturum olamaz.');
    if (s.label.trim() && sessions.some((o, j) => j !== i && o.label.trim().toLocaleLowerCase('tr-TR') === s.label.trim().toLocaleLowerCase('tr-TR'))) errors.push('Oturum adları farklı olmalıdır.');
    return errors;
  });
}

/**
 * KM7 (01.11.2026): an exam's dated sessions with client-side date checks and a confirmation before removing a saved
 * session. Embedded in the exam form of Müfredat Yönetimi:
 * `<app-exam-sessions-editor [(sessions)]="form.sessions" (validChange)="sessionsValid = $event" />`.
 */
@Component({
  selector: 'app-exam-sessions-editor',
  standalone: true,
  imports: [FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <fieldset class="grid gap-3 rounded-xl border border-gray-200 p-4 dark:border-gray-700">
      <legend class="px-1 font-semibold">Sınav oturumları</legend>
      @if (!sessions().length) { <p class="text-sm text-gray-600 dark:text-gray-300">Henüz oturum yok. Tarih belli olunca “Oturum ekle” ile ekleyin.</p> }
      @for (session of sessions(); track $index; let i = $index) {
        <div class="grid gap-2 rounded-lg border border-gray-200 p-3 dark:border-gray-700 sm:grid-cols-2 lg:grid-cols-4" role="group" [attr.aria-label]="(session.label || 'Yeni oturum') + ' oturumu'">
          <label class="grid min-w-0 gap-1 text-sm">Oturum adı<input [name]="'sessionLabel' + i" [attr.name]="'sessionLabel' + i" [ngModel]="session.label" (ngModelChange)="update(i, 'label', $event)" required maxlength="100" placeholder="2027/1" class="w-full min-w-0 rounded-lg border p-2 dark:bg-gray-800" [attr.aria-invalid]="errors()[i].length > 0"></label>
          <label class="grid min-w-0 gap-1 text-sm">Sınav tarihi<input [name]="'sessionDate' + i" [attr.name]="'sessionDate' + i" type="date" [ngModel]="session.date" (ngModelChange)="update(i, 'date', $event)" required class="w-full min-w-0 rounded-lg border p-2 dark:bg-gray-800"></label>
          <label class="grid min-w-0 gap-1 text-sm">Başvuru başlangıcı<input [name]="'sessionApplicationStart' + i" [attr.name]="'sessionApplicationStart' + i" type="date" [ngModel]="session.applicationStart" (ngModelChange)="update(i, 'applicationStart', $event)" class="w-full min-w-0 rounded-lg border p-2 dark:bg-gray-800"></label>
          <label class="grid min-w-0 gap-1 text-sm">Başvuru bitişi<input [name]="'sessionApplicationEnd' + i" [attr.name]="'sessionApplicationEnd' + i" type="date" [ngModel]="session.applicationEnd" (ngModelChange)="update(i, 'applicationEnd', $event)" class="w-full min-w-0 rounded-lg border p-2 dark:bg-gray-800"></label>
          @if (errors()[i].length) { <ul class="list-disc pl-5 text-sm text-red-700 dark:text-red-300 sm:col-span-2 lg:col-span-4" [attr.aria-label]="(session.label || 'Yeni oturum') + ' hataları'">@for (e of errors()[i]; track e) { <li>{{ e }}</li> }</ul> }
          <button type="button" (click)="remove(i)" class="justify-self-start rounded-lg border px-3 py-1 text-sm" [attr.aria-label]="(session.label || 'Yeni oturum') + ' oturumunu kaldır'">Oturumu kaldır</button>
        </div>
      }
      <button type="button" (click)="add()" class="justify-self-start rounded-lg border px-3 py-2 text-sm">Oturum ekle</button>
    </fieldset>`
})
export class CoachingExamSessionsEditorComponent {
  private readonly toaster = inject(ToasterService);
  readonly sessions = model<ExamSessionDraft[]>([]);
  readonly validChange = output<boolean>();
  readonly errors = computed(() => examSessionErrors(this.sessions()));
  readonly valid = computed(() => this.errors().every(x => x.length === 0));

  constructor() { effect(() => this.validChange.emit(this.valid())); }

  update(index: number, field: keyof ExamSessionDraft, value: string): void {
    this.sessions.set(this.sessions().map((s, i) => i === index ? { ...s, [field]: value ?? '' } : s));
  }
  add(): void { this.sessions.set([...this.sessions(), { id: null, date: '', label: '', applicationStart: '', applicationEnd: '' }]); }

  /** A saved session is removed only after a confirmation; an unsaved empty row goes at once. */
  async remove(index: number): Promise<void> {
    const session = this.sessions()[index];
    if (session.id || session.label || session.date) {
      const ok = await this.toaster.confirm(`“${session.label || 'Adsız'}” oturumu kaldırılacak. Değişiklik sınav kaydedilince uygulanır; bu oturumu hedef seçen öğrenci varsa kayıt reddedilir. Devam edilsin mi?`,
        { title: 'Oturumu kaldır', confirmText: 'Evet, kaldır', cancelText: 'Vazgeç' });
      if (!ok) return;
    }
    this.sessions.set(this.sessions().filter((_, i) => i !== index));
  }
}
