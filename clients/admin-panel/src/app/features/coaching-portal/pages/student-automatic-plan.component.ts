import { coachingFeedback, coachingToaster } from '../coaching-feedback';
import { CommonModule } from '@angular/common';
import { Component, DestroyRef, OnInit, inject, signal, Input, Injector } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { finalize, map } from 'rxjs';
import { AutomaticStudyPreview, CoachingStudyPlanningService, StudyAvailability, StudyPlan, StudyTopic, TargetPage, PublishedStudyOutcome } from '../../../core/services/coaching-study-planning.service';
import { CoachingLearningSetupService, LearningLesson } from '../../../core/services/coaching-learning-setup.service';

import { CoachingDatePipe } from '../coaching-labels';
@Component({
  selector: 'app-student-automatic-plan', standalone: true,
  imports: [CoachingDatePipe, CommonModule, FormsModule, RouterLink],
  templateUrl: './student-automatic-plan.component.html',
  styleUrl: './student-study-plans.component.scss'
})
export class StudentAutomaticPlanComponent implements OnInit {
  private readonly toaster = coachingToaster();
  @Input() learningContextVersion: number | null = null;
  private readonly injector = inject(Injector);
  private get setupService() { return this.injector.get(CoachingLearningSetupService); }
  readonly contextLessons = signal<LearningLesson[]>([]);
  contextLessonId = '';
  private readonly service = inject(CoachingStudyPlanningService);
  private readonly destroyRef = inject(DestroyRef);
  readonly availability = signal<StudyAvailability | null>(null);
  readonly results = signal<TargetPage<StudyTopic> | null>(null);
  readonly preview = signal<AutomaticStudyPreview | null>(null);
  readonly loading = signal(false);
  readonly searching = signal(false);
  readonly generating = signal(false);
  readonly saving = signal(false);
  readonly savedPlan = signal<StudyPlan | null>(null);
  readonly stale = signal(false);
  readonly error = signal<string | null>(null);
  selected: { topic: StudyTopic; minutes: number | null; outcomes?: (PublishedStudyOutcome & { included: boolean; minutes: number | null })[] }[] = [];
  readonly loadingOutcomes = signal(false);
  query = '';
  gradeNumber: number | null = null;
  examCode = '';
  startDate = '';
  days = 7;
  title = '';
  saveConfirmed = false;
  dirty = false;

  ngOnInit() {
    this.loadHours();
    if (this.learningContextVersion !== null) this.setupService.get().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: setup => this.setupService.catalog().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: catalog => this.contextLessons.set(catalog.lessons.filter(l => setup.context?.coursePreferences.some(c => c.lessonId === l.id))),
        error: () => this.error.set('Dersler yüklenemedi. Kurulumu yeniden yükleyin.') }),
      error: () => this.error.set('Eğitim bağlamı yüklenemedi. Kurulumu yeniden yükleyin.')
    });
  }
  busy() { return this.loading() || this.searching() || this.generating() || this.saving() || this.loadingOutcomes(); }
  locked() { return this.busy() || this.savedPlan() !== null; }
  loadHours() {
    if (this.locked()) return;
    this.loading.set(true); this.availability.set(null); this.preview.set(null); this.error.set(null);
    this.service.getAvailability().pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.loading.set(false)))
      .subscribe({ next: hours => { this.availability.set(hours); this.stale.set(false); },
        error: () => this.error.set('Çalışma saatleri yüklenemedi. Önce saatlerinizi belirleyin veya yeniden yükleyin.') });
  }
  searchTopics(pageNumber = 1) {
    if (this.locked()) return;
    this.searching.set(true); this.error.set(null); this.results.set(null);
    const request = this.learningContextVersion !== null
      ? this.setupService.catalog(this.contextLessonId || null, pageNumber).pipe(map(c => c.topics))
      : this.service.searchTopics(this.query, this.gradeNumber, this.examCode, pageNumber);
    request
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.searching.set(false)))
      .subscribe({ next: result => this.results.set(result), error: () => this.error.set('Konular yüklenemedi. Filtreleri kontrol edip tekrar deneyin.') });
  }
  addTopic(id: string) {
    if (this.locked() || this.selected.length >= 500 || this.selected.some(x => x.topic.id === id)) return;
    const topic = this.results()?.items.find(x => x.id === id);
    if (!topic) return;
    const minutes = topic.estimatedMinutes;
    this.selected = [...this.selected, { topic, minutes: minutes !== null && minutes >= 1 && minutes <= 1440 ? minutes : null }];
    this.changed();
  }
  removeTopic(id: string) {
    if (this.locked()) return;
    this.selected = this.selected.filter(x => x.topic.id !== id); this.changed();
  }
  loadOutcomes(topicId: string) {
    if (this.locked()) return;
    const selection = this.selected.find(x => x.topic.id === topicId); if (!selection) return;
    this.changed(); this.loadingOutcomes.set(true); this.error.set(null);
    this.service.getTopicOutcomes(topicId).pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.loadingOutcomes.set(false))).subscribe({
      next: outcomes => {
        if (!outcomes.length) { this.error.set('Bu konu için henüz incelenmiş kazanım süresi yayınlanmadı. Konu süresini kendin belirleyebilirsin.'); return; }
        selection.outcomes = outcomes.map(x => ({ ...x, included: true, minutes: x.totalMinutes }));
      }, error: () => this.error.set('Kazanımlar yüklenemedi. Seçimlerin korundu; tekrar deneyebilirsin.')
    });
  }
  useTopicMinutes(topicId: string) { if (this.locked()) return; const selection = this.selected.find(x => x.topic.id === topicId); if (selection) { delete selection.outcomes; this.changed(); } }
  private topicRequests() { return this.selected.map(x => x.outcomes ? { topicId: x.topic.id, requiredMinutes: null,
    outcomes: x.outcomes.filter(o => o.included).map(o => ({ outcomeId: o.outcomeId, estimateId: o.estimateId, requiredMinutes: o.minutes })) }
    : { topicId: x.topic.id, requiredMinutes: x.minutes }); }
  changed() { if (this.locked()) return; this.preview.set(null); this.saveConfirmed = false; this.dirty = true; }
  topicName(id: string) { return this.selected.find(x => x.topic.id === id)?.topic.name ?? 'Konu'; }
  generate() {
    const hours = this.availability();
    if (this.locked() || this.stale() || !hours) return;
    const date = /^\d{4}-\d{2}-\d{2}$/.test(this.startDate) ? new Date(`${this.startDate}T00:00:00Z`) : new Date(NaN);
    const lastDate = new Date(date.getTime() + (this.days - 1) * 86400000);
    if (!Number.isFinite(date.getTime()) || date.toISOString().slice(0, 10) !== this.startDate || this.startDate < '0001-01-01'
      || !Number.isInteger(this.days) || this.days < 1 || this.days > 90 || lastDate.getUTCFullYear() > 9999
      || this.selected.length < 1 || this.selected.length > 500
      || this.selected.some(x => x.outcomes ? !x.outcomes.some(o => o.included)
        || x.outcomes.filter(o => o.included).some(o => !Number.isInteger(o.minutes) || o.minutes! < 1 || o.minutes! > 1440)
        || x.outcomes.filter(o => o.included).reduce((sum, o) => sum + (o.minutes ?? 0), 0) > 1440
        : !Number.isInteger(x.minutes) || x.minutes! < 1 || x.minutes! > 1440)) {
      this.error.set('Geçerli bir başlangıç tarihi, 1-90 gün ve her konu için 1-1440 dakika belirtin.'); return;
    }
    this.saveConfirmed = false;
    this.generating.set(true); this.preview.set(null); this.error.set(null);
    this.service.previewAutomatic({ startDate: this.startDate, days: this.days, expectedAvailabilityVersion: hours.version,
      topics: this.topicRequests(),
      ...(this.learningContextVersion !== null ? { expectedLearningContextVersion: this.learningContextVersion } : {}) })
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.generating.set(false)))
      .subscribe({ next: preview => this.preview.set(preview), error: error => {
        if (error?.status === 409) { this.stale.set(true); this.error.set(typeof error?.error?.message === 'string' ? error.error.message : 'Çalışma saatleri veya eğitim bağlamı değişti. Kurulumu ve saatleri yeniden yükleyin.'); }
        else this.error.set(typeof error?.error?.message === 'string' ? error.error.message : 'Önizleme oluşturulamadı. Seçimleriniz korundu; tekrar deneyin.');
      } });
  }
  saveDraft() {
    const preview = this.preview();
    if (this.locked() || this.stale() || !preview || !this.saveConfirmed || !this.hasPendingWork()) return;
    if (!this.title.trim() || this.title.trim().length > 200) { this.error.set('1-200 karakterlik bir plan başlığı girin.'); return; }
    this.saving.set(true); this.error.set(null);
    this.service.saveAutomaticDraft({ title: this.title.trim(),
      preview: { startDate: this.startDate, days: this.days, expectedAvailabilityVersion: preview.availabilityVersion,
        topics: this.topicRequests(),
        ...(this.learningContextVersion !== null ? { expectedLearningContextVersion: this.learningContextVersion } : {}) },
      expectedActiveRevisionId: preview.activeRevisionId, expectedActiveRevisionVersion: preview.activeRevisionVersion,
      ...(preview.evidenceFingerprint?{expectedEvidenceFingerprint:preview.evidenceFingerprint}:{}) })
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.saving.set(false)))
      .subscribe({ next: plan => { this.savedPlan.set(plan); this.dirty = false; this.saveConfirmed = false; },
        error: error => {
          this.saveConfirmed = false;
          if (error?.status === 409) { this.stale.set(true); this.preview.set(null); }
          this.error.set(typeof error?.error?.message === 'string' ? error.error.message : 'Taslak kaydedilemedi. Seçimleriniz korundu. Mevcut taslağı çalışma planlarından kontrol edin.');
        } });
  }
  hasPendingWork() {const preview=this.preview();return !!preview&&(preview.schedule.tasks.length>0||preview.protectedTasks.some(x=>!x.isCompleted));}
  async canLeavePage() {
    if (this.saving()) return false;
    return !this.dirty || (typeof window !== 'undefined' && await this.toaster.confirm('Kaydedilmemiş plan seçimlerinden vazgeçerek ayrılmak istiyor musunuz?'));
  }
  private readonly feedback = coachingFeedback(undefined, this.error);
}
