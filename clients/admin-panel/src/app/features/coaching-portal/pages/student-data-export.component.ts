import { Component, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { CoachingDataExportService, saveCoachingExport } from '../../../core/services/coaching-data-export.service';

/** Roadmap O12: the student downloads their own Coaching data (KVKK) as a file. */
@Component({
  selector: 'app-student-data-export',
  standalone: true,
  template: `
    <section class="rounded-2xl border border-slate-200 bg-white p-5 dark:border-slate-800 dark:bg-slate-900" aria-labelledby="data-export-heading">
      <h2 id="data-export-heading" class="font-semibold">Kişisel verilerim</h2>
      <p class="mt-1 text-sm text-slate-600 dark:text-slate-300">Koçluk hesabındaki kişisel verilerinin bir kopyasını dosya olarak indirebilirsin: profilin, planların, ödevlerin, seansların, sınav sonuçların, abonelik ve ödeme kayıtların.</p>
      <p class="mt-2 text-sm text-slate-600 dark:text-slate-300">Yeni bir kuruma ya da öğretmene bağlandığında önceki çalışma geçmişin (ödevler, denemeler, seanslar, hedefler) onlar tarafından da görülebilir.</p>
      @if (error()) { <p role="alert" class="mt-3 text-sm text-red-700 dark:text-red-300">{{ error() }}</p> }
      @if (done()) { <p role="status" class="mt-3 text-sm text-emerald-700 dark:text-emerald-300">Dosyan indirildi.</p> }
      <button type="button" class="mt-4 rounded-lg border border-indigo-300 px-4 py-2 text-sm font-medium text-indigo-700 disabled:opacity-50 dark:border-indigo-800 dark:text-indigo-200" [disabled]="busy()" (click)="download()">{{ busy() ? 'Hazırlanıyor…' : 'Verilerimi indir' }}</button>
    </section>
  `
})
export class StudentDataExportComponent {
  private readonly service = inject(CoachingDataExportService);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly done = signal(false);

  async download(): Promise<void> {
    if (this.busy()) return;
    this.busy.set(true); this.error.set(null); this.done.set(false);
    try { saveCoachingExport(await firstValueFrom(this.service.download())); this.done.set(true); }
    catch { this.error.set('Verilerin hazırlanamadı. Biraz sonra yeniden dene.'); }
    finally { this.busy.set(false); }
  }
}
