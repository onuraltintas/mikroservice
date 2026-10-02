import { CommonModule } from '@angular/common';
import { Component, DestroyRef, HostListener, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { CoachingStudyPlanningService, StudyAvailability, StudyDay, StudyWindow } from '../../../core/services/coaching-study-planning.service';

interface AvailabilityRow { day: StudyDay; start: string; end: string; endOfDay: boolean }

@Component({
  selector: 'app-student-study-availability',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './student-study-availability.component.html',
  styleUrl: './student-study-plans.component.scss'
})
export class StudentStudyAvailabilityComponent implements OnInit {
  private readonly service = inject(CoachingStudyPlanningService);
  private readonly destroyRef = inject(DestroyRef);
  private lastSaved: StudyAvailability | null = null;
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly ready = signal(false);
  readonly error = signal<string | null>(null);
  readonly success = signal<string | null>(null);
  readonly days: { value: StudyDay; label: string }[] = [
    { value: 'Monday', label: 'Pazartesi' }, { value: 'Tuesday', label: 'Salı' },
    { value: 'Wednesday', label: 'Çarşamba' }, { value: 'Thursday', label: 'Perşembe' },
    { value: 'Friday', label: 'Cuma' }, { value: 'Saturday', label: 'Cumartesi' }, { value: 'Sunday', label: 'Pazar' }
  ];
  version: number | null = null;
  timeZoneId = 'Europe/Istanbul';
  rows: AvailabilityRow[] = [];
  dirty = false;

  ngOnInit() { this.load(); }

  load() {
    if (this.loading() || this.saving()) return;
    if (this.dirty) { this.error.set('Önce değişiklikleri kaydedin veya değişikliklerden vazgeçin.'); return; }
    this.loading.set(true);
    this.ready.set(false);
    this.error.set(null);
    this.success.set(null);
    this.service.getAvailability().pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.loading.set(false)))
      .subscribe({
        next: preferences => { this.apply(preferences); this.ready.set(true); },
        error: error => {
          if (error?.status === 404) { this.apply(null); this.ready.set(true); }
          else this.showError(error, 'Çalışma saatleri yüklenemedi. Kaydetmeden önce yeniden yükleyin.');
        }
      });
  }

  addRow() {
    if (!this.ready() || this.saving() || this.rows.length >= 42) return;
    this.rows = [...this.rows, { day: 'Monday', start: '17:00', end: '18:00', endOfDay: false }];
    this.dirty = true;
  }

  removeRow(index: number) {
    if (this.saving() || !this.ready()) return;
    this.rows = this.rows.filter((_, i) => i !== index);
    this.dirty = true;
  }

  discardChanges() {
    if (this.saving() || this.loading()) return;
    this.apply(this.lastSaved);
    this.error.set(null);
    this.success.set(null);
  }

  weeklyMinutes() {
    return this.days.reduce((total, day) => {
      const ranges = this.rows.filter(row => row.day === day.value)
        .map(row => ({ start: this.minute(row.start), end: row.endOfDay ? 1440 : this.minute(row.end) }))
        .filter(row => Number.isFinite(row.start) && Number.isFinite(row.end) && row.end > row.start)
        .sort((a, b) => a.start - b.start);
      let lastEnd = 0;
      for (const range of ranges) {
        total += Math.max(0, range.end - Math.max(lastEnd, range.start));
        lastEnd = Math.max(lastEnd, range.end);
      }
      return total;
    }, 0);
  }

  save() {
    if (!this.ready() || this.saving() || this.loading()) return;
    let windows: StudyWindow[];
    try { windows = this.validatedWindows(); }
    catch (error) { this.error.set(error instanceof Error ? error.message : 'Çalışma saatlerini kontrol edin.'); return; }
    this.saving.set(true);
    this.error.set(null);
    this.success.set(null);
    this.service.saveAvailability({ expectedVersion: this.version, timeZoneId: this.timeZoneId.trim(), windows })
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.saving.set(false)))
      .subscribe({ next: preferences => { this.apply(preferences); this.success.set('Çalışma saatlerin kaydedildi. Aktif planın değişmedi.'); },
        error: error => this.showError(error, 'Çalışma saatleri kaydedilemedi. Değişikliklerin ekranda korundu.') });
  }

  canLeavePage() {
    if (this.saving()) { this.error.set('Kaydetme işleminin tamamlanmasını bekleyin.'); return false; }
    return !this.dirty || (typeof window !== 'undefined' && window.confirm('Kaydedilmemiş çalışma saatleriniz var. Değişikliklerden vazgeçerek ayrılmak istiyor musunuz?'));
  }

  @HostListener('window:beforeunload', ['$event'])
  beforeUnload(event: BeforeUnloadEvent) {
    if (this.dirty || this.saving()) { event.preventDefault(); event.returnValue = ''; }
  }

  private validatedWindows(): StudyWindow[] {
    if (!this.timeZoneId.trim() || this.timeZoneId.length > 100) throw new Error('Geçerli bir saat dilimi girin.');
    if (this.rows.length > 42) throw new Error('En fazla 42 saat aralığı eklenebilir.');
    const windows = this.rows.map(row => ({ day: row.day, startMinute: this.minute(row.start), endMinute: row.endOfDay ? 1440 : this.minute(row.end) }));
    if (windows.some(row => !this.days.some(day => day.value === row.day) || !Number.isFinite(row.startMinute)
      || !Number.isFinite(row.endMinute) || row.endMinute <= row.startMinute))
      throw new Error('Gün ve saatleri kontrol edin. Bitiş saati başlangıçtan sonra olmalıdır.');
    for (const day of this.days) {
      const sorted = windows.filter(row => row.day === day.value).sort((a, b) => a.startMinute - b.startMinute);
      if (sorted.some((row, index) => index > 0 && row.startMinute < sorted[index - 1].endMinute))
        throw new Error(`${day.label} günündeki saat aralıkları çakışıyor.`);
    }
    return windows;
  }

  private minute(time: string): number {
    if (!/^([01]\d|2[0-3]):[0-5]\d$/.test(time)) return NaN;
    const [hour, minute] = time.split(':').map(Number);
    return hour * 60 + minute;
  }

  private clock(minute: number) { return `${String(Math.floor(minute / 60)).padStart(2, '0')}:${String(minute % 60).padStart(2, '0')}`; }

  private apply(preferences: StudyAvailability | null) {
    this.lastSaved = preferences;
    this.version = preferences?.version ?? null;
    this.timeZoneId = preferences?.timeZoneId ?? 'Europe/Istanbul';
    this.rows = (preferences?.windows ?? []).map(row => ({ day: row.day, start: this.clock(row.startMinute),
      end: row.endMinute === 1440 ? '00:00' : this.clock(row.endMinute), endOfDay: row.endMinute === 1440 }));
    this.dirty = false;
  }

  private showError(error: unknown, fallback: string) {
    const response = error as { status?: number; error?: { message?: unknown } };
    const message = response?.error?.message;
    this.error.set(typeof message === 'string' ? message : fallback);
    if (response?.status === 409) this.error.update(value => `${value} Değişikliklerin korundu; güncel kaydı yüklemek için önce değişikliklerden vazgeçip yeniden yükle.`);
  }
}
