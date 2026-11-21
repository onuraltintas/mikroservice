import { Component, Input, OnChanges, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CourseAssignment, CourseAssignmentAttachment, CourseAssignmentSubmission, CourseManagementService } from '../../course-management.service';
import { CoursePanelState, formatDate, formatDateTime, problemText } from '../panel-state';

const MAX_BYTES = 10 * 1024 * 1024;

/**
 * A homework's files and the students' answers (moved from the earlier single-page screen, 21.11.2026): add a sheet
 * to the homework, open what students handed in, give a score and a note. PDF, JPG and PNG up to 10 MB; files are
 * deleted 180 days after the due date.
 */
@Component({
  selector: 'course-homework-files',
  standalone: true,
  imports: [FormsModule],
  styleUrl: '../panel.scss',
  template: `
    <div class="files stack" style="gap: 12px">
      @if (error()) { <p class="alert" role="alert">{{ error() }}</p> }
      @if (notice()) { <p class="notice" role="status">{{ notice() }}</p> }
      <div>
        <h3>Ödev dosyaları</h3>
        <ul class="plain">
          @for (file of materials(); track file.id) {
            <li><span>{{ file.fileName }} <span class="muted">{{ size(file.sizeBytes) }}</span></span>
              <button type="button" class="btn-quiet btn-small" [disabled]="busy()" (click)="download(file)" [attr.aria-label]="file.fileName + ' dosyasını indir'">İndir</button></li>
          } @empty { <li class="muted">Bu ödeve dosya eklenmedi.</li> }
        </ul>
        @if (!state.readOnly()) {
          <div class="file-pick">
            <input [id]="'hw-files-' + assignment.id" class="file-pick__input" type="file" accept=".pdf,.jpg,.jpeg,.png" multiple (change)="pick($event)">
            <label [for]="'hw-files-' + assignment.id" class="btn-secondary file-pick__button">Dosya ekle</label>
            <span class="file-pick__name" aria-live="polite">{{ picked() || 'PDF, JPG ya da PNG; her biri en çok 10 MB.' }}</span>
          </div>
        }
      </div>
      <div>
        <h3>Öğrenci teslimleri <span class="muted" style="font-weight: 400">{{ handedIn() }} / {{ submissions().length }}</span></h3>
        <ul class="plain">
          @for (row of submissions(); track row.studentId) {
            <li class="answer">
              <div class="who"><strong>{{ row.studentName }}</strong>
                <span class="badge" [class.ok]="row.status === 'Reviewed'" [class.warn]="row.isLate">{{ statusText(row) }}</span>
                @if (row.submittedAt) { <span class="muted num">{{ dateTime(row.submittedAt) }}</span> }</div>
              @if (row.submissionId) {
                @if (row.text) { <p class="text">{{ row.text }}</p> }
                @for (file of row.attachments; track file.id) {
                  <button type="button" class="btn-quiet btn-small" [disabled]="busy()" (click)="download(file)">{{ file.fileName }} dosyasını indir</button>
                }
                @if (!state.readOnly()) {
                  <form class="review" (ngSubmit)="review(row)">
                    <label class="field">Puan (0–100)<input type="number" min="0" max="100" step="1" [name]="'s' + row.studentId" [(ngModel)]="scores[row.studentId]"></label>
                    <label class="field grow">Geri bildirim<input [name]="'f' + row.studentId" [(ngModel)]="notes[row.studentId]" maxlength="3000"></label>
                    <button type="submit" class="btn-secondary btn-small" [disabled]="busy() || (scores[row.studentId] == null && !notes[row.studentId]?.trim())">Kaydet</button>
                  </form>
                }
              }
            </li>
          } @empty { <li class="muted">Bu ödevin alıcısı yok.</li> }
        </ul>
      </div>
      <p class="muted" style="margin: 0; font-size: 14px">Eklenen ve teslim edilen dosyalar, son teslim tarihinden 180 gün sonra ({{ date(purgeOn()) }}) silinir.</p>
    </div>
  `,
  styles: `
    .files h3 { margin: 0 0 6px; font-size: 15px; }
    .plain { list-style: none; margin: 0; padding: 0; }
    .plain li { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 8px; min-height: 44px; border-top: 1px solid var(--ds-line); }
    .answer { padding: 8px 0; }
    .who { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; flex: 1 1 100%; }
    .text { margin: 0; flex-basis: 100%; white-space: pre-wrap; }
    .review { display: flex; flex-wrap: wrap; align-items: flex-end; gap: 8px; flex-basis: 100%; }
    .review .field { min-width: 120px; } .review .grow { flex: 1 1 220px; }
  `
})
export class HomeworkFilesComponent implements OnChanges {
  @Input({ required: true }) assignment!: CourseAssignment;
  readonly state = inject(CoursePanelState);
  private readonly service = inject(CourseManagementService);
  readonly materials = signal<CourseAssignmentAttachment[]>([]);
  readonly submissions = signal<CourseAssignmentSubmission[]>([]);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly notice = signal('');
  readonly picked = signal('');
  readonly date = formatDate;
  readonly dateTime = formatDateTime;
  scores: Record<string, number | null> = {};
  notes: Record<string, string> = {};

  ngOnChanges(): void { void this.load(); }

  async load(): Promise<void> {
    try {
      const [materials, submissions] = await Promise.all([
        this.service.getAssignmentAttachments(this.state.id(), this.assignment.id),
        this.service.getAssignmentSubmissions(this.state.id(), this.assignment.id)
      ]);
      this.materials.set(materials.filter(file => file.purpose === 'TeacherMaterial'));
      this.submissions.set(submissions);
      for (const row of submissions) { this.scores[row.studentId] = row.score; this.notes[row.studentId] = row.feedback ?? ''; }
    } catch (error) { this.error.set(problemText(error, 'Ödev dosyaları yüklenemedi.')); }
  }

  handedIn(): number { return this.submissions().filter(row => !!row.submissionId).length; }
  purgeOn(): string { return new Date(Date.parse(this.assignment.dueAt) + 180 * 86_400_000).toISOString(); }
  size(bytes: number): string { return bytes >= 1_048_576 ? `${(bytes / 1_048_576).toFixed(1).replace('.', ',')} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`; }
  statusText(row: CourseAssignmentSubmission): string {
    if (row.status === 'Pending' || !row.submissionId) return 'Teslim etmedi';
    if (row.status === 'Reviewed') return row.isLate ? 'Geç teslim, değerlendirildi' : 'Değerlendirildi';
    return row.isLate ? 'Geç teslim' : 'Teslim etti';
  }

  async pick(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const files = Array.from(input.files ?? []);
    input.value = '';
    if (!files.length) return;
    const wrong = files.find(file => file.size === 0 || file.size > MAX_BYTES || !/\.(pdf|jpe?g|png)$/i.test(file.name));
    if (wrong) { this.error.set(`${wrong.name} eklenemez: yalnız PDF, JPG ya da PNG, en çok 10 MB.`); return; }
    this.picked.set(files.map(file => file.name).join(', '));
    await this.act(async () => {
      for (const file of files) await this.service.uploadAssignmentAttachment(this.state.id(), this.assignment.id, file);
      this.picked.set('');
    }, files.length === 1 ? 'Dosya eklendi.' : `${files.length} dosya eklendi.`);
  }

  async download(file: CourseAssignmentAttachment): Promise<void> {
    try {
      const blob = await this.service.downloadAssignmentAttachment(this.state.id(), this.assignment.id, file.id);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url; link.download = file.fileName; link.click();
      setTimeout(() => URL.revokeObjectURL(url), 1000);
    } catch (error) { this.error.set(problemText(error, 'Dosya indirilemedi.')); }
  }

  review(row: CourseAssignmentSubmission): Promise<void> {
    const score = this.scores[row.studentId];
    return this.act(() => this.service.reviewAssignment(this.state.id(), this.assignment.id, row.studentId,
      score === null || score === undefined || (score as unknown) === '' ? null : Number(score), this.notes[row.studentId]?.trim() ?? ''),
      `${row.studentName} için değerlendirme kaydedildi.`);
  }

  private async act(work: () => Promise<unknown>, done: string): Promise<void> {
    this.busy.set(true); this.error.set(''); this.notice.set('');
    try { await work(); this.notice.set(done); await this.load(); }
    catch (error) { this.error.set(problemText(error)); }
    finally { this.busy.set(false); }
  }
}
