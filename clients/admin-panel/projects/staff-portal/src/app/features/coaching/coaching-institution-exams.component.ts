import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { coachingFeedback } from '../../../../../../src/app/features/coaching-portal/coaching-feedback';
import { CoachingInstitutionExamsService, InstitutionExam, InstitutionExamPage, InstitutionExamPreview, InstitutionExamReport } from './coaching-institution-exams.service';

const scopes = [
  { code: 'LGS', name: 'LGS' }, { code: 'TYT', name: 'TYT' }, { code: 'AYT_SAY', name: 'AYT · Sayısal' },
  { code: 'AYT_EA', name: 'AYT · Eşit Ağırlık' }, { code: 'AYT_SOZ', name: 'AYT · Sözel' }, { code: 'YDT_EN', name: 'YDT · İngilizce' }
];

/** Roadmap O4: the institution's own practice exams — create, import a CSV, read ranking and branches. */
@Component({
  selector: 'staff-coaching-institution-exams',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './coaching-institution-exams.component.html',
  styleUrl: './coaching-institution-exams.component.scss'
})
export class CoachingInstitutionExamsComponent implements OnInit {
  private readonly api = inject(CoachingInstitutionExamsService);
  readonly scopes = scopes;
  readonly grades = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12];
  readonly exams = signal<InstitutionExamPage>({ items: [], totalCount: 0, pageNumber: 1, pageSize: 20 });
  readonly selected = signal<InstitutionExam | null>(null);
  readonly report = signal<InstitutionExamReport | null>(null);
  readonly branch = signal<string | null>(null);
  readonly preview = signal<InstitutionExamPreview | null>(null);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly message = signal<string | null>(null);
  readonly canApply = computed(() => !!this.preview() && this.preview()!.errors.length === 0 && this.preview()!.accepted.length > 0 && !this.busy());
  form = { title: '', examCode: 'LGS', examDate: '', gradeLevel: null as number | null, maxScore: 500 };
  private csv = '';

  ngOnInit(): void { this.load(); }

  load(page = 1): void {
    this.error.set(null);
    this.api.list(page).subscribe({ next: value => this.exams.set(value), error: () => this.error.set('Denemeler yüklenemedi. Lütfen tekrar deneyin.') });
  }

  create(): void {
    if (this.busy() || !this.form.title.trim() || !this.form.examDate) { this.error.set('Deneme adı, kapsamı ve tarihi gereklidir.'); return; }
    this.busy.set(true); this.error.set(null); this.message.set(null);
    this.api.create({ title: this.form.title.trim(), examCode: this.form.examCode, examDate: this.form.examDate, gradeLevel: this.form.gradeLevel, maxScore: this.form.maxScore }).subscribe({
      next: exam => { this.busy.set(false); this.message.set(`${exam.title} oluşturuldu. Şimdi sonuç dosyasını yükleyebilirsiniz.`); this.form.title = ''; this.load(); void this.open(exam); },
      error: response => { this.busy.set(false); this.error.set(response?.error?.message ?? 'Deneme oluşturulamadı. Bilgileri kontrol edin.'); }
    });
  }

  async open(exam: InstitutionExam): Promise<void> {
    this.selected.set(exam); this.branch.set(null); this.preview.set(null); this.csv = '';
    await this.loadReport();
  }

  async filterBranch(branch: string | null): Promise<void> { this.branch.set(branch || null); await this.loadReport(); }

  async downloadTemplate(): Promise<void> {
    const exam = this.selected(); if (!exam) return;
    try {
      const blob = await firstValueFrom(this.api.template(exam.examCode));
      const url = URL.createObjectURL(blob); const link = document.createElement('a');
      link.href = url; link.download = `deneme-sablonu-${exam.examCode.toLowerCase()}.csv`; link.click();
      setTimeout(() => URL.revokeObjectURL(url), 0);
    } catch { this.error.set('Şablon indirilemedi.'); }
  }

  async chooseFile(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement; const file = input.files?.[0]; input.value = '';
    if (file) await this.previewFile(file);
  }

  async previewFile(file: File): Promise<void> {
    const exam = this.selected(); if (!exam) return;
    if (file.size > 2 * 1024 * 1024) { this.error.set('Dosya en fazla 2 MB olabilir.'); return; }
    this.busy.set(true); this.error.set(null); this.message.set(null); this.preview.set(null);
    try {
      this.csv = await readText(file);
      this.preview.set(await firstValueFrom(this.api.preview(exam.id, this.csv)));
    } catch (response) { this.error.set((response as { error?: { message?: string } })?.error?.message ?? 'Dosya okunamadı.'); }
    finally { this.busy.set(false); }
  }

  async applyImport(): Promise<void> {
    const exam = this.selected(); const preview = this.preview();
    if (!exam || !preview || !this.canApply()) return;
    this.busy.set(true); this.error.set(null);
    try {
      const result = await firstValueFrom(this.api.apply(exam.id, this.csv, preview.fingerprint));
      this.message.set(`${result.applied} sonuç kaydedildi.`); this.preview.set(null); this.csv = '';
      await this.loadReport(); this.load(this.exams().pageNumber);
    } catch (response) { this.error.set((response as { error?: { message?: string } })?.error?.message ?? 'Sonuçlar kaydedilemedi.'); }
    finally { this.busy.set(false); }
  }

  replacesAny(preview: InstitutionExamPreview): boolean { return preview.accepted.some(row => row.replaces); }
  percent(value: number): string { return '%' + value.toLocaleString('tr-TR', { maximumFractionDigits: 1 }); }
  net(value: number): string { return value.toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 }); }

  private async loadReport(): Promise<void> {
    const exam = this.selected(); if (!exam) return;
    try { this.report.set(await firstValueFrom(this.api.report(exam.id, this.branch()))); }
    catch { this.error.set('Deneme sonuçları yüklenemedi.'); }
  }

  private readonly feedback = coachingFeedback(this.message, this.error);
}

function readText(file: File): Promise<string> {
  if (typeof file.text === 'function') return file.text();
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(String(reader.result ?? '')); reader.onerror = () => reject(reader.error);
    reader.readAsText(file);
  });
}
