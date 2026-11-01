import { coachingFeedback, coachingToaster } from '../coaching-feedback';
import { practiceExamName } from '../../../core/models/practice-exam';
import { CommonModule } from '@angular/common';
import { Component, DestroyRef, EventEmitter, Input, OnChanges, Output, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subscription, finalize } from 'rxjs';
import { AdaptationReport, AdjustmentPreview, AdjustmentRequest, AnalysisExam, CoachingPracticeAdaptationService, PracticeAdvice, TopicAnalysis, TopicOption } from '../../../core/services/coaching-practice-adaptation.service';
import { StudyPlan } from '../../../core/services/coaching-study-planning.service';
import { StudyPlanCalendarComponent } from './study-plan-calendar.component';

import { CoachingDatePipe } from '../coaching-labels';
@Component({ selector: 'app-practice-adaptation', standalone: true, imports: [CoachingDatePipe, CommonModule, FormsModule, StudyPlanCalendarComponent],
  styles: [`:host{display:block} article{border-top:1px solid var(--ui-border);padding:1rem 0} p{line-height:1.6} label{display:grid;gap:.3rem;margin:.5rem 0} input,select,button{font:inherit;padding:.5rem;border:1px solid var(--ui-border);border-radius:.4rem;color:inherit;background:transparent} button{cursor:pointer;margin:.3rem}button:disabled{opacity:.5;cursor:default} .table{overflow-x:auto}table{width:100%;border-collapse:collapse}td,th{text-align:left;padding:.4rem;border-bottom:1px solid var(--ui-border)}td input{width:4rem} .error{color:var(--ui-danger)}.preview{border:2px solid var(--ui-border-strong);border-radius:.6rem;padding:1rem}.row{display:flex;flex-wrap:wrap;gap:.5rem;align-items:end}`],
  template: `
    <h3>Deneme konu analizi ve plan uyarlama</h3>
    <p>Son 90 gündeki her kapsam ve kayıt kaynağı ayrı incelenir. Öneri için son üç farklı deneme kullanılır; üç deneme bir başlangıç ürün kuralıdır. Konu eksiği yalnız toplam netten çıkarılmaz.</p>
    @if (busy()) { <p role="status">Analiz yükleniyor…</p> }
    @if (error()) { <p class="error" role="alert">{{error()}}</p><button (click)="load()" [disabled]="saving()">Analizi yeniden yükle</button> }
    @if (success()) { <p role="status">{{success()}}</p> }
    @if (data(); as report) {
      <details><summary>Denemelerin konu dağılımını incele ve düzenle</summary>
        @if (!report.exams.length) { <p>Önce kapsamı belirlenmiş bir deneme sonucu kaydet.</p> }
        @for (exam of report.exams; track exam.examId) {
          <article><strong>{{exam.title}}</strong> · {{examName(exam.input.examCode)}} · {{exam.examDate | date:'dd.MM.yyyy'}} · {{source(exam.source)}}
            <p>{{exam.analysis.topics.length}} konu satırı. Deneme kimliği: {{exam.analysis.testIdentity || 'Belirtilmemiş'}}.</p>
            <button (click)="open(exam)" [disabled]="saving()">{{exam.canEdit && !readOnly ? 'Konu analizini düzenle' : 'Konu analizini gör'}}</button>
          </article>
        }
      </details>
      @if (editing(); as exam) {
        <div class="preview" aria-label="Deneme konu analizi">
          <h4>{{exam.title}} · Konu dağılımı</h4>
          <label>Yayıncı ve deneme kodu (isteğe bağlı)<input maxlength="150" [(ngModel)]="analysis.testIdentity" (ngModelChange)="dirty=true" [disabled]="saving() || !canEdit()"></label>
          <p>Aynı denemeyi tekrar girerken aynı kodu kullan. Kod yoksa aynı gün ve aynı cevap dağılımındaki kayıtlar ihtiyatlı biçimde bir kez sayılır; öğretmen kaydı önceliklidir.</p>
          <div class="row"><label>Ders / test<select [(ngModel)]="sectionCode" (ngModelChange)="loadTopics()" [disabled]="saving() || !canEdit()"><option value="">Ders seç</option>
            @for (section of exam.input.sections; track section.sectionCode) { <option [value]="section.sectionCode">{{exam.sectionNames?.[section.sectionCode] || section.sectionCode}}</option> }
          </select></label><label>Konu ara<input [(ngModel)]="topicSearch" [disabled]="saving() || !canEdit()"></label>
          <label>Konu<select #topicSelect [disabled]="saving() || topicBusy() || !canEdit()"><option value="">Konu seç</option>@for (topic of filteredTopics(); track topic.id) { <option [value]="topic.id">{{topic.lessonName}} / {{topic.name}}</option> }</select></label>
          <button (click)="add(topicSelect.value)" [disabled]="saving() || !canEdit()">Konu ekle</button></div>
          @if (topicBusy()) { <p role="status">Konular yükleniyor…</p> }
          @if (sectionCode && !topicBusy() && !options().length) { <p>Bu ders için etkin ve eşleştirilmiş alt konu bulunamadı. Başka dersin konuları kullanılmaz.</p> }
          <div class="table"><table><thead><tr><th>Konu</th><th>Doğru</th><th>Yanlış</th><th>Boş</th><th>Neden</th><th></th></tr></thead><tbody>
            @for (row of analysis.topics; track row.sectionCode + row.topicId; let i = $index) {
              <tr><th>{{ rowName(row) }}</th><td><input type="number" min="0" max="120" [(ngModel)]="row.correct" (ngModelChange)="dirty=true" [disabled]="saving() || !canEdit()" [attr.aria-label]="rowName(row) + ' doğru'"></td>
              <td><input type="number" min="0" max="120" [(ngModel)]="row.wrong" (ngModelChange)="dirty=true" [disabled]="saving() || !canEdit()" [attr.aria-label]="rowName(row) + ' yanlış'"></td>
              <td><input type="number" min="0" max="120" [(ngModel)]="row.empty" (ngModelChange)="dirty=true" [disabled]="saving() || !canEdit()" [attr.aria-label]="rowName(row) + ' boş'"></td>
              <td><select [(ngModel)]="row.reason" [attr.aria-label]="rowName(row) + ' yanlış nedeni'" (ngModelChange)="dirty=true" [disabled]="saving() || !canEdit()"><option [ngValue]="null">Belirtilmemiş</option><option value="Knowledge">Konu bilgisi</option><option value="Calculation">İşlem / dikkat</option><option value="Interpretation">Yorumlama</option><option value="Time">Süre yetmedi</option></select></td>
              <td><button (click)="remove(i)" [disabled]="saving() || !canEdit()">Kaldır</button></td></tr>
            }
          </tbody></table></div>
          @for (section of exam.input.sections; track section.sectionCode) { <p>{{exam.sectionNames?.[section.sectionCode] || section.sectionCode}} konusu belirtilmemiş: {{remaining(section.sectionCode, 'correct')}} doğru / {{remaining(section.sectionCode, 'wrong')}} yanlış / {{remaining(section.sectionCode, 'empty')}} boş.</p> }
          <p>Konu oranları yalnız etiketlediğin sorulara dayanır. Doğru soruları da konulara dağıt; yalnız yanlışları girmek oranları yanıltabilir.</p>
          @if (!validAnalysis()) { <p class="error">Satır toplamı en az bir soru olmalı; konu dağılımları ders toplamlarını aşamaz.</p> }
          @if (canEdit()) { <button (click)="saveAnalysis()" [disabled]="saving() || !validAnalysis() || !dirty">Konu analizini kaydet</button> }
          <button (click)="close()" [disabled]="saving()">Kapat</button>
        </div>
      }
      @for (advice of report.advice; track advice.examCode + advice.source) {
        <article><h4>{{examName(advice.examCode)}} · {{source(advice.source)}}</h4>
          <p>Değerlendirilen farklı deneme: {{advice.examIds.length}} / 3.</p>
          @if (advice.examIds.length < 3) { <p>Şimdilik gözlem gösterilir; plan değişikliği için üç karşılaştırılabilir deneme bekleniyor.</p> }
          @else if (!advice.topics.length) { <p>Tekrar önerisi için yeterli konu kanıtı yok veya son denemede düzelme var. Konu dağılımlarını incele; yalnız ders netinden konu eksiği çıkarılmaz.</p> }
          @for (topic of advice.topics; track topic.evidence.sectionCode + topic.evidence.topicId) {
            <p><strong>{{topic.lessonName}} / {{topic.topicName}}</strong>: {{topic.evidence.appearances}} denemede {{topic.evidence.correct}} doğru, {{topic.evidence.wrong}} yanlış, {{topic.evidence.empty}} boş. Öneri: {{topic.evidence.suggestedMinutes}} dk {{activity(topic.evidence.reason)}}. {{topic.alreadyPlanned ? 'Bu deneme grubuna ait tekrar planda mevcut.' : ''}}</p>
          }
          @if (advice.topics.length && !readOnly) { <button (click)="choose(advice)" [disabled]="saving()">Tekrar önerisini ve takvim etkisini incele</button> }
        </article>
      }
      <p>En az iki denemede ve toplam beş soruda görülen, yanlış/boş oranı en az %40 olan ve son denemede devam eden zorlanmalar önerilir. Bu eşikler öğrenme teşhisi değildir. Bilgi eksiği ve en az %50 yanlış/boş oranında 60, diğer durumlarda 30 dakika başlangıç önerilir; süreyi değiştirebilirsin.</p>
      @if (chosen(); as advice) {
        <div class="preview" aria-label="Takvim etkisi önizlemesi"><h4>{{examName(advice.examCode)}} · Tekrar planını hazırla</h4>
          @for (topic of uniqueChoices(advice); track topic.evidence.topicId) { <label>{{topic.lessonName}} / {{topic.topicName}}<select [ngModel]="minutes[topic.evidence.topicId]" (ngModelChange)="setMinutes(topic.evidence.topicId,$event)" [disabled]="saving()"><option [ngValue]="0">Ekleme</option><option [ngValue]="30">30 dk</option><option [ngValue]="60">60 dk · iki güne böl</option></select></label> }
          <label>Tekrarları yerleştirmek için son tarih<input type="date" [(ngModel)]="endDate" (ngModelChange)="invalidate()" [disabled]="saving()"></label>
          <label><span><input type="checkbox" [(ngModel)]="extend" (ngModelChange)="invalidate()" [disabled]="saving()">Gerekirse yıllık planın bitiş tarihini bu tarihe uzatmayı kabul ediyorum.</span></label>
          <button (click)="preview()" [disabled]="saving()">Takvim etkisini önizle</button>
          @if (calendar(); as view) {
            <p>{{view.message}}</p><p>{{view.previousDeadline ? 'Plan bitişi' : 'Tekrar ufku'}}: {{view.previousDeadline || 'Belirtilmemiş'}} → {{view.proposedDeadline}}.</p>
            <p>Tamamlanan ve sabitlenen çalışmalar korunacak; mevcut çalışmaların gün ve saatleri değişmeyecek.</p>
            @if (calendarPlan(); as plan) { <p>Takvimde “Öneri” yazan kartlar eklenecek tekrarlardır. Diğer kartlar mevcut çalışmalardır.</p><app-study-plan-calendar [plan]="plan" [editable]="false" /> }
            @for (slot of view.slots; track slot.topicId + slot.date + slot.startMinute) { <p>{{ slot.date | coachingDate }} {{time(slot.startMinute)}}–{{time(slot.startMinute + slot.minutes)}} · {{slot.lessonName}} / {{slot.topicName}} · {{slot.minutes}} dk tekrar</p> }
            <div class="table"><table><thead><tr><th>Ders</th><th>Önceki son çalışma</th><th>Önerilen son çalışma</th><th>Ek süre</th></tr></thead><tbody>@for (impact of view.impact; track impact.lessonName) { <tr><th>{{impact.lessonName}}</th><td>{{impact.previousFinish || 'Planlanmamış'}}</td><td>{{impact.proposedFinish}}</td><td>{{impact.addedMinutes}} dk</td></tr> }</tbody></table></div>
            @for (item of view.unscheduled; track item.topicId) { <p class="error">{{topicName(item.topicId)}}: {{item.minutes}} dk sığmadı. Kısmi uygulama yapılmayacak.</p> }
            @if (view.canApply && !readOnly) { <label><span><input type="checkbox" [(ngModel)]="approved" [disabled]="saving()">Yukarıdaki ek çalışmaları ve tarih etkisini onaylıyorum.</span></label><button (click)="apply()" [disabled]="saving() || !approved">Onayla ve planı güncelle</button> }
          }
        </div>
      }
    }` })
export class PracticeAdaptationComponent implements OnChanges {
  private readonly toaster = coachingToaster();
  readonly examName = practiceExamName;
  @Input() staffStudentId: string | null = null;
  @Input() readOnly = false;
  @Input() today = '';
  @Output() readonly planChanged = new EventEmitter<void>();
  private readonly service = inject(CoachingPracticeAdaptationService);
  private readonly destroyRef = inject(DestroyRef);
  private read?: Subscription;
  private topicRead?: Subscription;
  private initialized = false;
  private previewRequest: AdjustmentRequest | null = null;
  readonly data = signal<AdaptationReport | null>(null); readonly busy = signal(false); readonly saving = signal(false);
  readonly topicBusy = signal(false); readonly error = signal(''); readonly success = signal('');
  readonly editing = signal<AnalysisExam | null>(null); readonly options = signal<TopicOption[]>([]);
  readonly chosen = signal<PracticeAdvice | null>(null); readonly calendar = signal<AdjustmentPreview | null>(null);
  readonly calendarPlan = computed<StudyPlan | null>(() => {
    const view = this.calendar(); if (!view) return null;
    return { id: view.planId + ':preview:' + view.fingerprint, version: view.expectedVersion, title: 'Tekrar önerisi önizlemesi', status: 'Draft',
      weeklySource: { draftVersion: 0, learningContextVersion: null, startDate: this.today, endDate: view.proposedDeadline, reuseFreedSlots: false, fingerprint: view.fingerprint, activeRevisionId: null, activeRevisionVersion: null, timeZoneId: view.timeZoneId },
      tasks: [...(view.existingTasks || []), ...view.slots.map((slot, i) => ({ id: 'preview-' + i, title: 'Öneri · ' + slot.topicName, lessonName: 'Öneri · ' + slot.lessonName,
        topicId: slot.topicId, plannedDate: slot.date, plannedStartMinute: slot.startMinute, plannedMinutes: slot.minutes, isPinned: false, isCompleted: false, actualMinutes: null, completedAt: null }))] };
  });
  private names = new Map<string, string>();
  analysis: TopicAnalysis = { topics: [], testIdentity: null }; sectionCode = ''; topicSearch = ''; dirty = false;
  minutes: Record<string, number> = {}; endDate = ''; extend = false; approved = false;
  async ngOnInit() { this.initialized = true; await this.load(); }
  async ngOnChanges() { if (this.initialized) { this.dirty = false; this.editing.set(null); await this.load(); } }
  async load() {
    if (this.saving() || !await this.discard()) return;
    this.dirty = false; this.editing.set(null);
    this.read?.unsubscribe(); this.topicRead?.unsubscribe(); this.options.set([]); this.topicBusy.set(false);
    this.chosen.set(null); this.invalidate(); this.data.set(null); this.error.set(''); this.busy.set(true);
    this.read = this.service.get(this.staffStudentId).pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.busy.set(false))).subscribe({
      next: data => { this.data.set(data); for (const group of data.advice) for (const topic of group.topics) this.names.set(topic.evidence.topicId, topic.topicName); },
      error: err => this.error.set(err.error?.message || 'Konu analizi yüklenemedi. Yeniden dene.')
    });
  }
  async open(exam: AnalysisExam) {
    if (this.saving() || !await this.discard()) return;
    this.topicRead?.unsubscribe(); this.options.set([]); this.sectionCode = ''; this.editing.set(exam); this.analysis = structuredClone(exam.analysis);
    this.dirty = false; this.error.set('');
    // Load names as well as choices; existing rows remain readable across sections.
    this.sectionCode = exam.input.sections[0]?.sectionCode || ''; this.loadTopics();
  }
  canEdit() { return !this.readOnly && !!this.editing()?.canEdit; }
  loadTopics() {
    this.topicRead?.unsubscribe(); this.options.set([]); const exam = this.editing(); if (!exam || !this.sectionCode) return;
    this.topicBusy.set(true);
    this.topicRead = this.service.topics(exam.input.examCode, this.sectionCode).pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.topicBusy.set(false))).subscribe({
      next: topics => { this.options.set(topics); for (const topic of topics) this.names.set(topic.id, topic.name); },
      error: () => this.error.set('Konular yüklenemedi. Dersi yeniden seç.')
    });
  }
  filteredTopics() { const search = this.topicSearch.toLocaleLowerCase('tr'); return this.options().filter(x => (x.lessonName + ' ' + x.name).toLocaleLowerCase('tr').includes(search)); }
  topicName(id: string) { return this.names.get(id) || 'Konu adını görmek için ilgili dersi seç'; }
  /** Roadmap D4: each row's inputs are named after its lesson and topic, not a generic "Konu". */
  rowName(row: { lessonName?: string | null; sectionCode: string; topicName?: string | null; topicId: string }): string {
    return `${row.lessonName || this.editing()?.sectionNames?.[row.sectionCode] || row.sectionCode} / ${row.topicName || this.topicName(row.topicId)}`;
  }
  add(topicId: string) {
    if (!this.canEdit() || this.saving() || !this.options().some(x => x.id === topicId) || this.analysis.topics.length >= 120
      || this.analysis.topics.some(x => x.topicId === topicId && x.sectionCode === this.sectionCode)) return;
    this.analysis.topics.push({ sectionCode: this.sectionCode, topicId, correct: 0, wrong: 0, empty: 0, reason: null }); this.dirty = true;
  }
  remove(index: number) { if (!this.canEdit() || this.saving()) return; this.analysis.topics.splice(index, 1); this.dirty = true; }
  remaining(code: string, field: 'correct' | 'wrong' | 'empty') { return (this.editing()?.input.sections.find(x => x.sectionCode === code)?.[field] || 0) - this.analysis.topics.filter(x => x.sectionCode === code).reduce((sum, row) => sum + (row[field] || 0), 0); }
  validAnalysis() {
    return this.analysis.topics.every(x => [x.correct, x.wrong, x.empty].every(n => Number.isInteger(n) && n >= 0) && x.correct + x.wrong + x.empty > 0)
      && !!this.editing()?.input.sections.every(s => ['correct', 'wrong', 'empty'].every(f => this.remaining(s.sectionCode, f as 'correct' | 'wrong' | 'empty') >= 0));
  }
  saveAnalysis() {
    const exam = this.editing(); if (!exam || this.saving() || !this.canEdit() || !this.validAnalysis()) return;
    this.saving.set(true); this.error.set('');
    this.service.save(this.staffStudentId, exam, structuredClone(this.analysis)).pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.saving.set(false))).subscribe({
      next: async () => { this.saving.set(false); this.dirty = false; this.editing.set(null); this.success.set('Konu analizi kaydedildi; öneriler güncellendi.'); await this.load(); },
      error: err => this.error.set(err.status === 409 ? 'Sonuç değişti. Formunu kontrol edip analizi yeniden yükle.' : err.error?.message || 'Analiz kaydedilemedi; girişlerin korundu.')
    });
  }
  async close() { if (!this.saving() && await this.discard()) { this.dirty = false; this.editing.set(null); this.topicRead?.unsubscribe(); } }
  uniqueChoices(advice: PracticeAdvice) { return [...new Map(advice.topics.filter(x => !x.alreadyPlanned).map(x => [x.evidence.topicId, x])).values()].slice(0, 10); }
  async choose(advice: PracticeAdvice) {
    if (this.saving() || this.readOnly || !await this.discard()) return;
    this.dirty = false; this.editing.set(null); this.chosen.set(advice); this.minutes = {};
    for (const topic of this.uniqueChoices(advice)) this.minutes[topic.evidence.topicId] = topic.evidence.suggestedMinutes;
    const day = new Date(this.today + 'T12:00:00Z'); day.setUTCDate(day.getUTCDate() + 14); this.endDate = day.toISOString().slice(0, 10);
    this.extend = false; this.invalidate(); this.error.set('');
  }
  setMinutes(topic: string, minutes: number) { this.minutes[topic] = minutes; this.invalidate(); }
  invalidate() { this.calendar.set(null); this.previewRequest = null; this.approved = false; }
  preview() {
    const advice = this.chosen(); if (!advice || this.saving() || this.readOnly) return;
    const request: AdjustmentRequest = { examCode: advice.examCode, source: advice.source, endDate: this.endDate, allowDeadlineExtension: this.extend,
      topics: Object.entries(this.minutes).filter(([, m]) => m > 0).map(([topicId, minutes]) => ({ topicId, minutes })) };
    if (!request.topics.length) { this.error.set('En az bir konu seç.'); return; }
    this.invalidate(); this.saving.set(true); this.error.set('');
    this.service.preview(this.staffStudentId, request).pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.saving.set(false))).subscribe({
      next: view => { this.previewRequest = structuredClone(request); this.calendar.set(view); },
      error: err => this.error.set(err.error?.message || 'Önizleme hazırlanamadı. Saatleri ve tarihi kontrol et.')
    });
  }
  apply() {
    const view = this.calendar(); if (!view?.canApply || !this.previewRequest || !this.approved || this.saving() || this.readOnly) return;
    this.saving.set(true); this.error.set('');
    this.service.apply(this.staffStudentId, view.fingerprint, this.previewRequest, true).pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.saving.set(false))).subscribe({
      next: async () => { this.saving.set(false); this.success.set('Tekrarlar plana eklendi. Mevcut çalışma kayıtları korundu.'); this.planChanged.emit(); await this.load(); },
      error: err => { this.invalidate(); this.error.set(err.status === 409 ? 'Plan veya analiz değişti. Güncel verilerle yeniden önizle.' : err.error?.message || 'Değişiklik uygulanamadı; yeniden önizle.'); }
    });
  }
  private async discard() { return !this.dirty || await this.toaster.confirm('Kaydedilmemiş konu analizini bırakmak istiyor musun?'); }
  async canLeavePage() { return !this.saving() && await this.discard(); }
  canRefresh() { return !this.saving() && !this.dirty; }
  source(value: string) { return value === 'StudentReported' ? 'Öğrenci beyanı' : 'Öğretmen kaydı'; }
  time(minute: number) { return `${Math.floor(minute / 60).toString().padStart(2, '0')}:${(minute % 60).toString().padStart(2, '0')}`; }
  activity(reason: string | null) { return reason === 'Time' ? 'süreli soru çözümü' : reason === 'Calculation' ? 'işlem ve dikkat çalışması' : reason === 'Interpretation' ? 'yorumlama çalışması' : 'konu tekrarı ve kontrol soruları'; }
  private readonly feedback = coachingFeedback(this.success, this.error);
}
