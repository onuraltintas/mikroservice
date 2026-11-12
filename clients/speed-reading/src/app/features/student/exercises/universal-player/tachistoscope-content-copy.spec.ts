import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../../../../core/services/auth.service';
import { ExerciseProgramService } from '../../../../core/services/exercise-program.service';
import { ExerciseService } from '../../../../core/services/exercise.service';
import { ExerciseSessionService } from '../../../../core/services/exercise-session.service';
import { LearningPathService } from '../../../../core/services/learning-path.service';
import { StudentProgramService } from '../../../../core/services/student-program.service';
import { ToasterService } from '../../../../core/services/toaster.service';
import { ReviewService } from '../../../../services/review.service';
import { ExercisePlayerComponent } from './exercise-player.component';
import { EngineCallbacks } from './engines/base-engine.interface';
import { TextStreamEngine } from './engines/text-stream.engine';
import { TACHISTOSCOPE_COPY } from './tachistoscope-copy';

const noop: EngineCallbacks = {
  onStart: () => undefined, onPause: () => undefined, onResume: () => undefined, onComplete: () => undefined,
  onError: () => undefined, onStateChange: () => undefined, onStepComplete: () => undefined, onAction: () => undefined
};

describe('Tachistoscope copy and answer input per content type', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [ExercisePlayerComponent],
      providers: [AuthService, ExerciseProgramService, ExerciseService, ExerciseSessionService,
        LearningPathService, StudentProgramService, ToasterService, ReviewService, Router, ActivatedRoute]
        .map(provide => ({ provide, useValue: {} }))
    });
    spyOn(ExercisePlayerComponent.prototype, 'ngOnInit');
  });

  function render(config: Record<string, unknown>, waiting: boolean) {
    const engine = new TextStreamEngine();
    engine.initialize({ mode: 'tachistoscope', visuals: { showFixation: false }, ...config }, noop);
    const fixture = TestBed.createComponent(ExercisePlayerComponent);
    const player = fixture.componentInstance as any;
    player.isLoading = false;
    player.exercise = { title: 'Kısa Gösterimde Tanıma', description: '', difficultyLevel: 3, exerciseTypeName: 'Tachistoscope' };
    player.parsedConfig = { engineType: 'text_stream', engineConfig: config['engineConfig'] ?? {} };
    player.engine = engine;
    player.engineState = { ...engine.state, isRunning: waiting };
    if (waiting) {
      player.isWaitingForAnswer = () => true;
      player.isShowingStimulus = () => false;
      player.isShowingFixation = () => false;
    }
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  const cases: [string, string, string][] = [
    ['number', 'Ekranda kısa süre görünen sayıyı yazın.', 'Gördüğünüz sayıyı yazın'],
    ['word', 'Ekranda kısa süre görünen kelimeyi yazın. Büyük-küçük harf fark etmez.', 'Gördüğünüz kelimeyi yazın'],
    ['phrase', 'Ekranda kısa süre görünen kelime grubunu yazın. Büyük-küçük harf fark etmez.', 'Gördüğünüz kelime grubunu yazın'],
    ['letter', 'Ekranda kısa süre görünen harfleri yazın.', 'Gördüğünüz harfleri yazın']
  ];

  for (const [type, instruction, prompt] of cases) {
    it(`explains a ${type} exercise on the start card and in the answer prompt`, () => {
      const ready = render({ content: { type, count: 3 } }, false);
      expect(ready.querySelector('.tachistoscope-ready')!.textContent).toContain(instruction);
      TestBed.resetTestingModule();
      TestBed.configureTestingModule({
        imports: [ExercisePlayerComponent],
        providers: [AuthService, ExerciseProgramService, ExerciseService, ExerciseSessionService,
          LearningPathService, StudentProgramService, ToasterService, ReviewService, Router, ActivatedRoute]
          .map(provide => ({ provide, useValue: {} }))
      });
      const running = render({ content: { type, count: 3 } }, true);
      expect(running.querySelector('.answer-prompt')!.textContent).toContain(prompt);
      const input = running.querySelector('input.answer-input') as HTMLInputElement;
      expect(input.getAttribute('placeholder')).toContain(prompt);
    });
  }

  it('keeps every student-facing tachistoscope string in one copy module', () => {
    for (const [type, instruction, prompt] of cases) {
      const kind = type as 'number' | 'word' | 'phrase' | 'letter';
      expect(TACHISTOSCOPE_COPY.instruction(kind)).toContain(instruction);
      expect(TACHISTOSCOPE_COPY.answerPrompt(kind)).toBe(prompt);
    }
    expect(TACHISTOSCOPE_COPY.contentLabel.phrase).toContain('Kelime grubu');
    // Student-facing speed-reading copy uses the formal "siz" form, like the rest of the player.
    for (const type of ['number', 'letter', 'word', 'phrase'] as const) {
      expect(TACHISTOSCOPE_COPY.instruction(type)).not.toMatch(/\byaz\.|Gördüğün /);
      expect(TACHISTOSCOPE_COPY.answerPrompt(type)).toMatch(/^Gördüğünüz .+ yazın$/);
    }
    expect(Object.values(TACHISTOSCOPE_COPY.contentLabel)).toEqual(['Sayı', 'Harf', 'Kelime', 'Kelime grubu (iki kelime)']);
  });

  it('takes the content type from the server session over the catalogue configuration', () => {
    const ready = render({ serverAuthoritative: true, content: { type: 'number', count: 3 },
      tachistoscope: { count: 3, round: 0, contentType: 'phrase', displayDurationMs: 400 } }, false);
    expect(ready.querySelector('.tachistoscope-ready')!.textContent).toContain('kelime grubunu yazın.');
  });

  it('accepts free text with Turkish letters and spaces for words and phrases', () => {
    const running = render({ content: { type: 'phrase', count: 3 } }, true);
    const input = running.querySelector('input.answer-input') as HTMLInputElement;
    expect(input.type).toBe('text');
    expect(input.getAttribute('inputmode')).toBe('text');
    expect(input.getAttribute('pattern')).toBeNull();
    expect(input.getAttribute('autocapitalize')).toBe('off');
    expect(input.getAttribute('autocorrect')).toBe('off');
    expect(input.getAttribute('spellcheck')).toBe('false');
    expect(input.getAttribute('autocomplete')).toBe('off');
    expect(input.getAttribute('lang')).toBe('tr');
  });

  it('offers the numeric keypad only for number exercises', () => {
    const running = render({ content: { type: 'number', count: 3 } }, true);
    const input = running.querySelector('input.answer-input') as HTMLInputElement;
    expect(input.getAttribute('inputmode')).toBe('numeric');
    expect(input.getAttribute('pattern')).toBeNull();
  });

  it('always uses number copy in assessments', () => {
    const ready = render({ content: { type: 'word', count: 3 }, isAssessmentMode: true,
      assessmentProtocol: { version: 2, practiceRounds: 3, roundsPerBlock: 6, blockValues: [600, 450, 300] } }, false);
    expect(ready.querySelector('.tachistoscope-ready')!.textContent).toContain('sayıyı yazın.');
  });
});
