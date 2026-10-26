import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize, Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';

interface Edition { id: string; code: string; subjectName: string; academicYearStart: number; academicYearEnd: number; sourceUrl: string; documentSha256: string }
interface Outcome { id: string; code: string; name: string; officialTopicId: string; sourcePage: number | null; sourceKind?: string }
interface Estimate { id: string; outcomeId: string; number: number; learningMinutes: number; practiceMinutes: number; checkMinutes: number; method: string; confidence: string; evidence: string; isPublished: boolean }
interface Catalog { edition: Edition; officialTopics: { id: string; name: string; lessonPeriods: number | null; outcomeCount: number }[]; outcomes: Outcome[]; estimates: Estimate[]; mappings: { topicId: string; outcomeId: string; isApproved: boolean; evidence: string }[]; topics: { id: string; name: string }[] }
interface ImportReview { fingerprint: string; outcomeCount: number; matchedTopics: number; unmatchedTopics: string[]; warnings: string[] }

@Component({ selector: 'app-coaching-outcomes', standalone: true, imports: [FormsModule],
  template: `<section><h2>Kazanımlar ve çalışma süreleri</h2>
    <p>Resmî ders saati konu bütçesidir. Kazanımın net çalışma dakikaları ayrı bir tahmindir; mola ve video sürelerini içermez.</p>
    @if (error()) { <p role="alert">{{ error() }}</p> }
    @if (busy()) { <p role="status">İşlem sürüyor…</p> }
    <fieldset [disabled]="busy()"><label>İşlem gerekçesi<input [(ngModel)]="reason" maxlength="500"></label>
      <details><summary>Kazanım paketi aktar</summary><p>Önce paketi önizleyin; aktarım yalnız taslak kayıtlar oluşturur.</p>
        <label>JSON dosyası<input type="file" accept=".json,application/json" (change)="readFile($event)"></label>
        <label>Paket JSON<textarea [(ngModel)]="packageText" (ngModelChange)="review.set(null)" rows="6"></textarea></label>
        <button type="button" (click)="previewPackage()">Aktarımı önizle</button>
        @if (review(); as r) { <p>{{ r.outcomeCount }} kazanım · {{ r.matchedTopics }} eşleşen konu · {{ r.unmatchedTopics.length }} eşleşmeyen konu</p>
          @for (warning of r.warnings; track warning) { <p>{{ warning }}</p> }
          @for (id of r.unmatchedTopics; track id) { <p>Eşleşmedi: {{ id }}</p> }
          <button type="button" [disabled]="reason.trim().length < 5" (click)="importPackage()">Taslakları aktar</button> }
      </details>
      <button type="button" (click)="load()">Programları yeniden yükle</button>
      <label>Resmî program<select [(ngModel)]="editionId" (ngModelChange)="openEdition()"><option value="">Program seçin</option>
        @for (e of editions(); track e.id) { <option [value]="e.id">{{ e.code }} · {{ e.academicYearStart }}–{{ e.academicYearEnd }}</option> }</select></label>
      @if (catalog(); as c) { <p><a [href]="c.edition.sourceUrl" target="_blank" rel="noopener noreferrer">Resmî program belgesi</a></p>
        <p>Belge SHA256: {{ c.edition.documentSha256 }}</p>
        @for (group of c.officialTopics; track group.id) { <p>{{ group.name }}: {{ group.lessonPeriods }} resmî ders saati · {{ group.outcomeCount }} kazanım</p> }
        <label>Kazanım<select [(ngModel)]="outcomeId" (ngModelChange)="selectOutcome()"><option value="">Kazanım seçin</option>
          @for (o of c.outcomes; track o.id) { <option [value]="o.id">{{ o.code }} — {{ o.name }}</option> }</select></label>
        @if (selectedOutcome(); as outcome) { <h3>{{ outcome.code }} — {{ outcome.name }}</h3>
          @if (outcome.sourceKind === 'ExamSkill') { <p>MEBİ sınav hazırlık becerisi · Resmî kazanım kodu değildir.</p> }
          @else { <p>Kaynak PDF sayfası: {{ outcome.sourcePage }}</p> }
          <h4>Konu eşleştirmelerini incele</h4><label>Eşleştirme inceleme dayanağı<textarea [(ngModel)]="mappingEvidence" maxlength="500"></textarea></label>
          @for (m of mappings(); track m.topicId) { <article><p>{{ topicName(m.topicId) }} · {{ m.isApproved ? 'Onaylı' : 'İnceleme bekliyor' }}</p><p>{{ m.evidence }}</p>
            <button type="button" [disabled]="mappingEvidence.trim().length < 5" (click)="reviewMapping(m.topicId, !m.isApproved)">{{ m.isApproved ? 'Onayı kaldır' : 'Kapsamı onayla' }}</button></article> }
          <h4>Yeni süre sürümü</h4><p>İlk öğrenme için net dakika girin. Değişiklik eski sürümleri ve planları değiştirmez.</p>
          <div class="fields"><label>Öğrenme (dk)<input type="number" min="0" max="1440" [(ngModel)]="learningMinutes"></label>
            <label>Alıştırma (dk)<input type="number" min="0" max="1440" [(ngModel)]="practiceMinutes"></label>
            <label>Kısa kontrol (dk)<input type="number" min="0" max="1440" [(ngModel)]="checkMinutes"></label></div>
          <label>Güven düzeyi<select [(ngModel)]="confidence"><option value="Low">Düşük</option><option value="Medium">Orta</option><option value="High">Yüksek</option></select></label>
          <label>Süre inceleme dayanağı<textarea [(ngModel)]="durationEvidence" maxlength="500"></textarea></label>
          <button type="button" [disabled]="durationEvidence.trim().length < 5 || reason.trim().length < 5" (click)="saveEstimate()">İncelenmiş süre sürümü oluştur</button>
          @for (e of estimates(); track e.id) { <article><strong>Sürüm {{ e.number }} · {{ e.learningMinutes + e.practiceMinutes + e.checkMinutes }} dk · {{ e.isPublished ? 'Yayınlanmış' : 'Taslak' }}</strong>
            <p>{{ e.learningMinutes }} öğrenme + {{ e.practiceMinutes }} alıştırma + {{ e.checkMinutes }} kontrol · {{ e.confidence }}</p><p>{{ e.evidence }}</p>
            @if (e.method === 'ReviewedStudy' && !e.isPublished) { <button type="button" [disabled]="reason.trim().length < 5" (click)="publish(e.id)">İncelenmiş süreyi yayınla</button> }
          </article> }
        }
      }
    </fieldset></section>`,
  styles: [`fieldset{border:0;padding:0;display:grid;gap:.8rem}label{display:grid;gap:.3rem}input,select,textarea,button{font:inherit;padding:.55rem;border:1px solid #b8c5d5;border-radius:.4rem;max-width:100%;box-sizing:border-box}button{cursor:pointer;justify-self:start}button:disabled{opacity:.5}.fields{display:grid;grid-template-columns:repeat(auto-fit,minmax(160px,1fr));gap:.6rem}article{border-top:1px solid #d6deea;padding:.7rem 0}p{overflow-wrap:anywhere}[role=alert]{color:#a21825}`]
})
export class CoachingOutcomesComponent implements OnInit {
  private readonly http = inject(HttpClient); private readonly destroy = inject(DestroyRef);
  private readonly url = `${environment.apiUrl}/coaching-admin/outcomes`;
  readonly busy = signal(false); readonly error = signal<string | null>(null); readonly editions = signal<Edition[]>([]);
  readonly catalog = signal<Catalog | null>(null); readonly review = signal<ImportReview | null>(null);
  packageText = ''; reason = ''; editionId = ''; outcomeId = ''; mappingEvidence = ''; durationEvidence = ''; confidence = 'Medium';
  learningMinutes = 0; practiceMinutes = 0; checkMinutes = 0; private reviewedText = '';
  private requestNumber = 0;
  ngOnInit() { this.load(); }
  load() { if (this.busy()) return; this.read(this.http.get<{ data: Edition[] }>(this.url), r => this.editions.set(r.data)); }
  openEdition() { if (this.busy() || !this.editionId) { if (!this.editionId) this.catalog.set(null); return; }
    this.read(this.http.get<{ data: Catalog }>(`${this.url}/${this.editionId}`), r => this.catalog.set(r.data)); }
  selectedOutcome() { return this.catalog()?.outcomes.find(x => x.id === this.outcomeId); }
  mappings() { return this.catalog()?.mappings.filter(x => x.outcomeId === this.outcomeId) ?? []; }
  estimates() { return this.catalog()?.estimates.filter(x => x.outcomeId === this.outcomeId).sort((a, b) => b.number - a.number) ?? []; }
  topicName(id: string) { return this.catalog()?.topics.find(x => x.id === id)?.name ?? id; }
  selectOutcome() { const e = this.estimates()[0]; this.learningMinutes = e?.learningMinutes ?? 0; this.practiceMinutes = e?.practiceMinutes ?? 0; this.checkMinutes = e?.checkMinutes ?? 0; this.mappingEvidence = ''; this.durationEvidence = ''; }
  async readFile(event: Event) { const file = (event.target as HTMLInputElement).files?.[0]; if (!file || this.busy()) return;
    if (file.size > 1048576) { this.error.set('Paket en fazla 1 MB olabilir.'); return; }
    try { this.packageText = await file.text(); this.review.set(null); } catch { this.error.set('Dosya okunamadı.'); } }
  previewPackage() { if (this.busy()) return; this.review.set(null); let p: unknown;
    try { p = JSON.parse(this.packageText); } catch { this.error.set('Geçerli JSON paketi seçin.'); return; }
    const text = this.packageText; this.read(this.http.post<{ data: ImportReview }>(`${this.url}/import-preview`, p), r => { this.review.set(r.data); this.reviewedText = text; }); }
  importPackage() { if (this.busy() || !this.review() || this.reason.trim().length < 5) return;
    if (this.packageText !== this.reviewedText) { this.review.set(null); this.error.set('Paket değişti; yeniden önizleyin.'); return; }
    this.write(this.http.post(`${this.url}/import`, { package: JSON.parse(this.reviewedText), fingerprint: this.review()!.fingerprint, reason: this.reason.trim() }), true); }
  reviewMapping(topic: string, approved: boolean) { if (this.mappingEvidence.trim().length < 5) return;
    this.write(this.http.put(`${this.url}/mappings/${topic}/${this.outcomeId}`, { approved, evidence: this.mappingEvidence.trim() })); }
  saveEstimate() { const parts = [this.learningMinutes, this.practiceMinutes, this.checkMinutes]; const total = parts.reduce((a, b) => a + b, 0);
    if (!this.outcomeId || this.reason.trim().length < 5 || this.durationEvidence.trim().length < 5) return;
    if (parts.some(x => !Number.isInteger(x) || x < 0) || total < 1 || total > 1440) { this.error.set('Toplam süre 1–1440 dakika olmalıdır.'); return; }
    this.write(this.http.post(`${this.url}/${this.outcomeId}/estimates`, { learningMinutes: this.learningMinutes, practiceMinutes: this.practiceMinutes,
      checkMinutes: this.checkMinutes, confidence: this.confidence, evidence: this.durationEvidence.trim(), reason: this.reason.trim() })); }
  publish(id: string) { if (this.reason.trim().length >= 5) this.write(this.http.put(`${this.url}/estimates/${id}/publication`, { reason: this.reason.trim() })); }
  private write(request: Observable<unknown>, imported = false) { if (this.busy()) return;
    this.read(request, () => { this.busy.set(false); if (imported) { this.review.set(null); this.load(); } else this.openEdition(); }); }
  private read<T>(request: Observable<T>, next: (r: T) => void) { this.busy.set(true); this.error.set(null); const number = ++this.requestNumber;
    request.pipe(takeUntilDestroyed(this.destroy), finalize(() => { if (number === this.requestNumber) this.busy.set(false); })).subscribe({ next,
      error: e => this.error.set(typeof e?.error?.message === 'string' ? e.error.message : 'İşlem tamamlanamadı. Bilgilerinizi kontrol edip yeniden deneyin.') }); }
}
