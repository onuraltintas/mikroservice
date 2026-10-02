import { CommonModule } from '@angular/common';
import { Component, DestroyRef, HostListener, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AutomaticStudyPreview, CoachingStudyPlanningService, StudyAvailability, StudyTopic, TargetPage } from '../../../core/services/coaching-study-planning.service';

@Component({
  selector: 'app-student-automatic-plan', standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './student-automatic-plan.component.html',
  styleUrl: './student-study-plans.component.scss'
})
export class StudentAutomaticPlanComponent implements OnInit {
  private readonly service = inject(CoachingStudyPlanningService);
  private readonly destroyRef = inject(DestroyRef);
  readonly availability = signal<StudyAvailability | null>(null);
  readonly results = signal<TargetPage<StudyTopic> | null>(null);
  readonly preview = signal<AutomaticStudyPreview | null>(null);
  readonly loading = signal(false);
  readonly searching = signal(false);
  readonly generating = signal(false);
  readonly stale = signal(false);
  readonly error = signal<string | null>(null);
  selected: { topic: StudyTopic; minutes: number | null }[] = [];
  query = '';
  gradeNumber: number | null = null;
  examCode = '';
  startDate = '';
  days = 7;
  dirty = false;

  ngOnInit() { this.loadHours(); }
  busy() { return this.loading() || this.searching() || this.generating(); }
  loadHours() {
    if (this.busy()) return;
    this.loading.set(true); this.availability.set(null); this.preview.set(null); this.error.set(null);
    this.service.getAvailability().pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.loading.set(false)))
      .subscribe({ next: hours => { this.availability.set(hours); this.stale.set(false); },
        error: () => this.error.set('Çalışma saatleri yüklenemedi. Önce saatlerinizi belirleyin veya yeniden yükleyin.') });
  }
  searchTopics(pageNumber = 1) {
    if (this.busy()) return;
    this.searching.set(true); this.error.set(null); this.results.set(null);
    this.service.searchTopics(this.query, this.gradeNumber, this.examCode, pageNumber)
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.searching.set(false)))
      .subscribe({ next: result => this.results.set(result), error: () => this.error.set('Konular yüklenemedi. Filtreleri kontrol edip tekrar deneyin.') });
  }
  addTopic(id: string) {
    if (this.busy() || this.selected.length >= 500 || this.selected.some(x => x.topic.id === id)) return;
    const topic = this.results()?.items.find(x => x.id === id);
    if (!topic) return;
    const minutes = topic.estimatedMinutes;
    this.selected = [...this.selected, { topic, minutes: minutes !== null && minutes >= 1 && minutes <= 1440 ? minutes : null }];
    this.changed();
  }
  removeTopic(id: string) {
    if (this.busy()) return;
    this.selected = this.selected.filter(x => x.topic.id !== id); this.changed();
  }
  changed() { this.preview.set(null); this.dirty = true; }
  topicName(id: string) { return this.selected.find(x => x.topic.id === id)?.topic.name ?? 'Konu'; }
  generate() {
    const hours = this.availability();
    if (this.busy() || this.stale() || !hours) return;
    const date = /^\d{4}-\d{2}-\d{2}$/.test(this.startDate) ? new Date(`${this.startDate}T00:00:00Z`) : new Date(NaN);
    const lastDate = new Date(date.getTime() + (this.days - 1) * 86400000);
    if (!Number.isFinite(date.getTime()) || date.toISOString().slice(0, 10) !== this.startDate || this.startDate < '0001-01-01'
      || !Number.isInteger(this.days) || this.days < 1 || this.days > 90 || lastDate.getUTCFullYear() > 9999
      || this.selected.length < 1 || this.selected.length > 500
      || this.selected.some(x => !Number.isInteger(x.minutes) || x.minutes! < 1 || x.minutes! > 1440)) {
      this.error.set('Geçerli bir başlangıç tarihi, 1-90 gün ve her konu için 1-1440 dakika belirtin.'); return;
    }
    this.generating.set(true); this.preview.set(null); this.error.set(null);
    this.service.previewAutomatic({ startDate: this.startDate, days: this.days, expectedAvailabilityVersion: hours.version,
      topics: this.selected.map(x => ({ topicId: x.topic.id, requiredMinutes: x.minutes })) })
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.generating.set(false)))
      .subscribe({ next: preview => this.preview.set(preview), error: error => {
        if (error?.status === 409) { this.stale.set(true); this.error.set('Çalışma saatleri değişti. Saatleri yeniden yükleyip tekrar önizleyin.'); }
        else this.error.set(typeof error?.error?.message === 'string' ? error.error.message : 'Önizleme oluşturulamadı. Seçimleriniz korundu; tekrar deneyin.');
      } });
  }
  canLeavePage() {
    return !this.dirty || (typeof window !== 'undefined' && window.confirm('Kaydedilmemiş plan seçimlerinden vazgeçerek ayrılmak istiyor musunuz?'));
  }
  @HostListener('window:beforeunload', ['$event'])
  beforeUnload(event: BeforeUnloadEvent) { if (this.dirty) { event.preventDefault(); event.returnValue = ''; } }
}
