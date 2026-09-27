import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { ActivatedRoute, Router } from '@angular/router';
import { environment } from '../../../../environments/environment';
import { LearningPathService } from '../../../core/services/learning-path.service';

interface ReadingQuestion {
  id: string;
  questionText: string;
  optionA: string;
  optionB: string;
  optionC: string;
  optionD: string;
}

interface ReadingStart {
  sessionId: string;
  title: string;
  content: string;
  questions: ReadingQuestion[];
}

@Component({
  selector: 'app-learning-path-reading',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <main class="reading-page">
      <button class="back" (click)="back()">← Öğrenme Yoluna Dön</button>
      @if (loading) {
        <p>Okuma metni yükleniyor…</p>
      } @else {
        @if (error) { <div class="notice" role="alert">{{ error }}</div> }
        @if (reading) {
        <div class="eyebrow">KİŞİSEL ÖĞRENME YOLU · OKUMA ÇALIŞMASI</div>
        <h1>{{ reading.title }}</h1>
        @if (stage === 'text') {
          <article class="reading-text">{{ reading.content }}</article>
          <button class="primary" (click)="finishReading()">Okumayı Bitir, Sorulara Geç</button>
        } @else if (stage === 'questions') {
          <p>Metni okudun. Şimdi soruları yanıtla.</p>
          @for (question of reading.questions; track question.id; let i = $index) {
            <fieldset>
              <legend>{{ i + 1 }}. {{ question.questionText }}</legend>
              @for (option of options(question); track option.key) {
                <label>
                  <input type="radio" [name]="question.id" [value]="option.key"
                    [(ngModel)]="answers[question.id]" />
                  {{ option.text }}
                </label>
              }
            </fieldset>
          }
          <button class="primary" [disabled]="saving || !allAnswered()" (click)="complete()">
            {{ saving ? 'Kaydediliyor…' : 'Çalışmayı Tamamla' }}
          </button>
        } @else {
          <div class="notice">Okuma çalışman kaydedildi. Günlük programın bundan etkilenmedi.</div>
          <button class="primary" (click)="back()">Öğrenme Yoluna Dön</button>
        }
        }
      }
    </main>
  `,
  styles: [`
    .reading-page { max-width: 850px; margin: 0 auto; padding: 32px 20px 72px; color: var(--sp-text-1); }
    .back { border: 0; background: none; color: var(--sp-primary); cursor: pointer; margin-bottom: 28px; font: inherit; }
    .eyebrow { color: var(--sp-primary); font-size: .75rem; font-weight: 750; letter-spacing: .08em; }
    h1 { font-size: clamp(1.7rem, 4vw, 2.6rem); line-height: 1.15; margin: 12px 0 28px; }
    .reading-text { white-space: pre-wrap; line-height: 1.9; font-size: 1.125rem; padding: 32px;
      border: 1px solid var(--sp-border-strong); border-radius: var(--sp-radius-lg);
      background: var(--sp-surface-1); margin-bottom: 28px; }
    fieldset { border: 1px solid var(--sp-border-strong); border-radius: var(--sp-radius-lg);
      padding: 20px; margin: 20px 0; }
    legend { font-weight: 700; padding: 0 6px; }
    label { display: flex; gap: 10px; align-items: baseline; padding: 8px 0; cursor: pointer; }
    .primary { border: 0; border-radius: var(--sp-radius-sm); background: var(--sp-primary);
      color: white; padding: 13px 20px; font: inherit; font-weight: 700; cursor: pointer; }
    .primary:disabled { opacity: .5; cursor: not-allowed; }
    .notice { border: 1px solid var(--sp-border-strong); border-radius: var(--sp-radius-lg);
      padding: 20px; margin: 24px 0; background: var(--sp-surface-1); }
  `]
})
export class LearningPathReadingComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly learningPath = inject(LearningPathService);
  private readonly api = `${environment.speedReadingApiUrl}/student-reading`;

  reading: ReadingStart | null = null;
  answers: Record<string, string> = {};
  loading = true;
  saving = false;
  error = '';
  stage: 'text' | 'questions' | 'done' = 'text';

  ngOnInit(): void {
    const textId = this.route.snapshot.paramMap.get('textId');
    if (!textId) {
      this.error = 'Okuma metni bulunamadı.';
      this.loading = false;
      return;
    }
    this.http.get<ReadingStart>(`${this.api}/${textId}/start`).subscribe({
      next: reading => { this.reading = reading; this.loading = false; },
      error: () => { this.error = 'Okuma metni yüklenemedi.'; this.loading = false; }
    });
  }

  options(question: ReadingQuestion): { key: string; text: string }[] {
    return [
      { key: 'A', text: question.optionA }, { key: 'B', text: question.optionB },
      { key: 'C', text: question.optionC }, { key: 'D', text: question.optionD }
    ].filter(option => option.text);
  }

  finishReading(): void { this.stage = 'questions'; }

  allAnswered(): boolean {
    return !!this.reading && this.reading.questions.every(question => !!this.answers[question.id]);
  }

  complete(): void {
    if (!this.reading || !this.allAnswered() || this.saving) return;
    const textId = this.route.snapshot.paramMap.get('textId');
    const pathItemId = this.route.snapshot.queryParamMap.get('pathItemId');
    this.saving = true;
    this.error = '';
    this.http.post(`${this.api}/${textId}/complete`, {
      sessionId: this.reading.sessionId,
      timeSpentSeconds: 0,
      comprehensionScore: 0,
      answers: Object.entries(this.answers).map(([questionId, selectedAnswer]) => ({ questionId, selectedAnswer }))
    }).subscribe({
      next: () => {
        if (!pathItemId) { this.saving = false; this.stage = 'done'; return; }
        this.learningPath.completePersonalizedPathItem(pathItemId, this.reading!.sessionId).subscribe({
          next: () => { this.saving = false; this.stage = 'done'; },
          error: () => { this.saving = false; this.error = 'Öğrenme yolu ilerlemesi kaydedilemedi. Tekrar deneyin.'; }
        });
      },
      error: () => { this.saving = false; this.error = 'Okuma sonucu kaydedilemedi. Tekrar deneyin.'; }
    });
  }

  back(): void { this.router.navigate(['/student/learning-path']); }
}
