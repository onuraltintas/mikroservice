import { Component, inject, input, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { CoachingDataExportService, saveCoachingExport } from '../../../core/services/coaching-data-export.service';

/** Roadmap O12: the student (coaching audit K1: or the parent) downloads their own Coaching data (KVKK) as a file. */
@Component({
  selector: 'app-student-data-export',
  standalone: true,
  template: `
    <section class="rounded-2xl border border-(--ui-border) bg-(--ui-surface) p-5" aria-labelledby="data-export-heading">
      <h2 id="data-export-heading" class="font-semibold">Kişisel verilerim</h2>
      @if (audience() === 'parent') {
        <p class="mt-1 text-sm text-(--ui-text-muted)">Koçlukta kendi oluşturduğun verilerin bir kopyasını dosya olarak indirebilirsin: öğretmenlerle yazışmaların ve çocuğun için gönderdiğin ödeme bildirimleri. Çocuğunun kendi verileri onun hesabından indirilir.</p>
      } @else {
        <p class="mt-1 text-sm text-(--ui-text-muted)">Koçluk hesabındaki kişisel verilerinin bir kopyasını dosya olarak indirebilirsin: profilin, planların, ödevlerin, seansların, sınav sonuçların, abonelik ve ödeme kayıtların.</p>
        <p class="mt-2 text-sm text-(--ui-text-muted)">Yeni bir kuruma ya da öğretmene bağlandığında önceki çalışma geçmişin (ödevler, denemeler, seanslar, hedefler) onlar tarafından da görülebilir.</p>
      }
      @if (error()) { <p role="alert" class="mt-3 text-sm text-(--ui-danger)">{{ error() }}</p> }
      @if (done()) { <p role="status" class="mt-3 text-sm text-(--ui-success)">Dosyan indirildi.</p> }
      <button type="button" class="mt-4 rounded-lg border border-(--ui-border) px-4 py-2 text-sm font-medium text-(--ui-brand-text) disabled:opacity-50 " [disabled]="busy()" (click)="download()">{{ busy() ? 'Hazırlanıyor…' : 'Verilerimi indir' }}</button>
    </section>
  `
})
export class StudentDataExportComponent {
  private readonly service = inject(CoachingDataExportService);
  /** Whose data: the student's own, or what a parent created themselves. */
  readonly audience = input<'student' | 'parent'>('student');
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
